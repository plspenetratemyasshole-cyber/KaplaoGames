import { createClient } from "npm:@supabase/supabase-js@2.49.1";
import { jwtVerify } from "npm:jose@5.9.6";
import { DeleteObjectCommand, S3Client } from "npm:@aws-sdk/client-s3@3.709.0";

const corsHeaders: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type, x-kaplao-game-secret",
};

const SUPER_ADMIN_EMAIL = "asmoloff55@gmail.com";
const DEFAULT_PERMISSION = "none";

type PlayerPermission = "admin" | "level_mod" | "none";

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...corsHeaders, "Content-Type": "application/json" },
  });
}

function gameServerSecret(): string {
  return (Deno.env.get("GAME_SERVER_SECRET") || Deno.env.get("JWT_SECRET") || "").trim();
}

function verifyGameServerSecret(req: Request): boolean {
  const header = (req.headers.get("x-kaplao-game-secret") || "").trim();
  const secret = gameServerSecret();
  return header.length > 0 && secret.length > 0 && header === secret;
}

function normalizePermission(value: string): PlayerPermission {
  const key = value.trim().toLowerCase();
  if (key === "admin" || key === "level_mod" || key === "none") return key;
  return DEFAULT_PERMISSION;
}

function permissionCanDeleteLevels(permission: PlayerPermission): boolean {
  return permission === "admin" || permission === "level_mod";
}

function canActorDeletePublicLevel(params: {
  permission: PlayerPermission;
  actorEmail: string;
  actorNickname: string;
  authorEmail?: string | null;
  authorNick?: string | null;
}): boolean {
  if (permissionCanDeleteLevels(params.permission)) return true;

  const actorEmail = (params.actorEmail || "").trim().toLowerCase();
  const authorEmail = (params.authorEmail || "").trim().toLowerCase();
  if (actorEmail && authorEmail && actorEmail === authorEmail) return true;

  const authorNick = (params.authorNick || "").trim();
  const actorNick = (params.actorNickname || "").trim();
  if (!authorEmail && authorNick && actorNick &&
      authorNick.localeCompare(actorNick, undefined, { sensitivity: "accent" }) === 0) {
    return true;
  }

  return false;
}

function sanitizeNickname(raw: string): string {
  const trimmed = (raw || "").trim();
  if (!trimmed) return "";
  return trimmed.length > 20 ? trimmed.slice(0, 20) : trimmed;
}

function escapeIlike(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/%/g, "\\%").replace(/_/g, "\\_");
}

function isUuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

function jwtSecretCandidates(): string[] {
  const values = [
    Deno.env.get("JWT_SECRET"),
    Deno.env.get("GAME_SERVER_SECRET"),
    Deno.env.get("ACCOUNTS_JWT_SECRET"),
  ]
    .map((v) => (v || "").trim())
    .filter((v) => v.length > 0);
  values.push("dev-only-kaplao-secret-change-me");
  return [...new Set(values)];
}

async function sessionFromAuthToken(token: string) {
  for (const secret of jwtSecretCandidates()) {
    try {
      const { payload } = await jwtVerify(token, new TextEncoder().encode(secret));
      const email = String(payload.email || "")
        .trim()
        .toLowerCase();
      const nickname = sanitizeNickname(String(payload.nickname || ""));
      const userId = String(payload.sub || "");
      if (!email || !nickname || !userId) continue;
      return { userId, email, nickname };
    } catch {
      // try next secret
    }
  }
  return null;
}

async function sessionFromWebsiteVerify(token: string) {
  const websiteUrl = (Deno.env.get("KAPLAO_WEBSITE_URL") || "https://kaplaogames.vercel.app").trim().replace(/\/+$/, "");
  const secret = gameServerSecret();
  if (!secret) return null;

  try {
    const response = await fetch(`${websiteUrl}/api/game/verify-player`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Kaplao-Game-Secret": secret,
      },
      body: JSON.stringify({ token }),
    });

    if (!response.ok) return null;
    const data = await response.json().catch(() => null) as {
      valid?: boolean;
      email?: string;
      nickname?: string;
    } | null;
    if (!data?.valid) return null;

    const email = String(data.email || "").trim().toLowerCase();
    const nickname = sanitizeNickname(String(data.nickname || ""));
    if (!email || !nickname) return null;
    return { userId: email, email, nickname };
  } catch (error) {
    console.error("[levels-delete] website verify fallback failed", error);
    return null;
  }
}

function accountsSupabaseAdmin() {
  const url = (
    Deno.env.get("ACCOUNTS_SUPABASE_URL") ||
    Deno.env.get("SUPABASE_URL") ||
    ""
  ).trim();
  const serviceKey = (
    Deno.env.get("ACCOUNTS_SUPABASE_SERVICE_ROLE_KEY") ||
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ||
    ""
  ).trim();
  if (!url || !serviceKey) return null;
  return createClient(url, serviceKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });
}

function levelsSupabaseAdmin() {
  const url = (Deno.env.get("SUPABASE_URL") || "").trim();
  const serviceKey = (Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") || "").trim();
  if (!url || !serviceKey) return null;
  return createClient(url, serviceKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });
}

async function lookupByEmail(
  sb: ReturnType<typeof createClient>,
  table: string,
  columns: string,
  email: string
) {
  const key = email.trim().toLowerCase();
  const exact = await sb.from(table).select(columns).eq("email", key).maybeSingle();
  if (exact.data) return exact.data;

  const fuzzy = await sb.from(table).select(columns).ilike("email", escapeIlike(key)).maybeSingle();
  return fuzzy.data ?? null;
}

async function resolvePlayerPermission(email: string): Promise<PlayerPermission> {
  const key = email.trim().toLowerCase();
  if (key === SUPER_ADMIN_EMAIL.toLowerCase()) return "admin";

  const sb = accountsSupabaseAdmin();
  if (!sb) return DEFAULT_PERMISSION;

  const roleRow = (await lookupByEmail(
    sb,
    "kaplao_player_roles",
    "permission",
    key
  )) as { permission?: string } | null;
  const legacyRow = await lookupByEmail(sb, "kaplao_admins", "email", key);

  let permission = normalizePermission(String(roleRow?.permission ?? DEFAULT_PERMISSION));
  if (permission === "none" && legacyRow) permission = "admin";
  else if (!roleRow && legacyRow) permission = "admin";

  return permission;
}

function sanitizeObjectKey(raw: string): string | null {
  const key = (raw || "").trim().replace(/\\/g, "/").replace(/^\/+/, "");
  if (!key || key.includes("..")) return null;
  if (!/^[a-zA-Z0-9._/-]+$/.test(key)) return null;
  return key;
}

function parseStoragePath(storagePath: string): { kind: "s3" | "legacy_pixeldrain" | "unknown"; value: string } {
  const path = (storagePath || "").trim();
  if (!path) return { kind: "unknown", value: "" };

  const lower = path.toLowerCase();
  if (lower.startsWith("s3:")) {
    const value = sanitizeObjectKey(path.slice(3));
    return value ? { kind: "s3", value } : { kind: "unknown", value: "" };
  }

  if (lower.startsWith("pixeldrain:")) {
    return { kind: "legacy_pixeldrain", value: path.slice("pixeldrain:".length).trim() };
  }

  const plain = sanitizeObjectKey(path);
  if (plain && plain.endsWith(".zip")) {
    return { kind: "s3", value: plain };
  }

  return { kind: "unknown", value: path };
}

function isTimewebConfigured(): boolean {
  return Boolean(
    (Deno.env.get("TIMEWEB_S3_ACCESS_KEY") || "").trim() &&
      (Deno.env.get("TIMEWEB_S3_SECRET_KEY") || "").trim() &&
      (Deno.env.get("TIMEWEB_S3_BUCKET") || "").trim()
  );
}

function getS3Client(): S3Client {
  return new S3Client({
    region: (Deno.env.get("TIMEWEB_S3_REGION") || "ru-1").trim(),
    endpoint: (Deno.env.get("TIMEWEB_S3_ENDPOINT") || "https://s3.twcstorage.ru").trim(),
    credentials: {
      accessKeyId: (Deno.env.get("TIMEWEB_S3_ACCESS_KEY") || "").trim(),
      secretAccessKey: (Deno.env.get("TIMEWEB_S3_SECRET_KEY") || "").trim(),
    },
    forcePathStyle: true,
  });
}

function normalizeTitle(value: string): string {
  return (value || "").trim().replace(/\s+/g, " ").toLowerCase();
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { headers: corsHeaders });
  }

  if (req.method !== "POST") {
    return json({ error: "Method not allowed." }, 405);
  }

  if (!verifyGameServerSecret(req)) {
    return json({ error: "Forbidden." }, 403);
  }

  try {
    const body = await req.json().catch(() => ({}));
    const token = String(body?.token || "").trim();
    const levelId = String(body?.levelId || "").trim();
    const confirmTitle = String(body?.confirmTitle || "").trim();

    if (!token) {
      return json({ error: "Sign in required." }, 401);
    }

    if (!isUuid(levelId)) {
      return json({ error: "Invalid level id." }, 400);
    }

    if (!confirmTitle || confirmTitle.length > 120) {
      return json({ error: "Confirm the exact level title." }, 400);
    }

    const session = (await sessionFromAuthToken(token)) ?? (await sessionFromWebsiteVerify(token));
    if (!session) {
      return json({ error: "Invalid or expired session. Sign in again from the main menu." }, 401);
    }

    const permission = await resolvePlayerPermission(session.email);

    const levelsSb = levelsSupabaseAdmin();
    if (!levelsSb) {
      return json({ error: "Level catalog is not configured." }, 503);
    }

    const rowResult = await levelsSb
      .from("levels")
      .select("id,title,storage_path,author_email,author_nick")
      .eq("id", levelId)
      .maybeSingle();

    if (rowResult.error) {
      console.error("[levels-delete] select", rowResult.error);
      return json({ error: "Failed to load level." }, 500);
    }

    const row = rowResult.data as {
      id?: string;
      title?: string;
      storage_path?: string;
      author_email?: string;
      author_nick?: string;
    } | null;
    if (!row?.id) {
      return json({ error: "Level not found." }, 404);
    }

    if (normalizeTitle(String(row.title || "")) !== normalizeTitle(confirmTitle)) {
      return json({ error: "Level title confirmation does not match." }, 400);
    }

    if (!canActorDeletePublicLevel({
      permission,
      actorEmail: session.email,
      actorNickname: session.nickname,
      authorEmail: row.author_email,
      authorNick: row.author_nick,
    })) {
      console.warn(
        `[levels-delete] denied ${session.email} permission=${permission} levelId=${levelId} author=${row.author_email || row.author_nick}`
      );
      return json({ error: "You can only delete your own levels." }, 403);
    }

    const deleteResult = await levelsSb.from("levels").delete().eq("id", levelId);
    if (deleteResult.error) {
      console.error("[levels-delete] delete row", deleteResult.error);
      return json({ error: "Failed to delete level metadata." }, 500);
    }

    const storagePath = String(row.storage_path || "").trim();
    const parsed = parseStoragePath(storagePath);
    if (parsed.kind === "s3" && isTimewebConfigured()) {
      try {
        const bucket = (Deno.env.get("TIMEWEB_S3_BUCKET") || "").trim();
        await getS3Client().send(
          new DeleteObjectCommand({
            Bucket: bucket,
            Key: parsed.value,
          })
        );
      } catch (storageError) {
        console.error("[levels-delete] s3 delete failed", storageError);
        // Metadata already removed — do not fail the request for orphaned zip cleanup.
      }
    }

    console.log(
      `[levels-delete] deleted levelId=${levelId} title="${row.title}" by ${session.email} (${permission})`
    );

    return json({ ok: true, deletedId: levelId });
  } catch (error) {
    console.error("[levels-delete]", error);
    const message = error instanceof Error ? error.message : "Delete failed.";
    return json({ error: message }, 500);
  }
});
