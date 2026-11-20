import { createClient } from "npm:@supabase/supabase-js@2.49.1";
import { jwtVerify } from "npm:jose@5.9.6";

const corsHeaders: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type, x-kaplao-game-secret",
};

const SUPER_ADMIN_EMAIL = "asmoloff55@gmail.com";
const DEFAULT_PERMISSION = "none";
const DEFAULT_NICKNAME_PRESET = "none";
const ALL_RIGHTS = ["level_purge", "game_host", "web_console"] as const;

type PlayerPermission = "admin" | "level_mod" | "none";
type PlayerRight = (typeof ALL_RIGHTS)[number];

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

function sanitizeNickname(raw: string): string {
  const trimmed = (raw || "").trim();
  if (!trimmed) return "";
  return trimmed.length > 20 ? trimmed.slice(0, 20) : trimmed;
}

function normalizePermission(value: string): PlayerPermission {
  const key = value.trim().toLowerCase();
  if (key === "admin" || key === "level_mod" || key === "none") return key;
  return DEFAULT_PERMISSION;
}

function normalizePreset(value: string): string {
  const key = (value || DEFAULT_NICKNAME_PRESET).trim().toLowerCase().replace(/\s+/g, "_");
  if (!key || key === "own") return "none";
  return key;
}

function normalizeRights(value: unknown): PlayerRight[] {
  if (!Array.isArray(value)) return [];
  const set = new Set<PlayerRight>();
  for (const item of value) {
    const key = String(item || "").trim().toLowerCase();
    if ((ALL_RIGHTS as readonly string[]).includes(key)) set.add(key as PlayerRight);
  }
  return ALL_RIGHTS.filter((r) => set.has(r));
}

function rightsFromPermission(permission: PlayerPermission): PlayerRight[] {
  if (permission === "admin") return ["level_purge", "game_host"];
  if (permission === "level_mod") return ["level_purge"];
  return [];
}

function derivePermission(rights: PlayerRight[]): PlayerPermission {
  if (rights.includes("game_host")) return "admin";
  if (rights.includes("level_purge")) return "level_mod";
  return "none";
}

function escapeIlike(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/%/g, "\\%").replace(/_/g, "\\_");
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
  console.error("[verify-player] jwt: no secret accepted the token");
  return null;
}

function supabaseAdmin() {
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

async function lookupByEmail(sb: ReturnType<typeof createClient>, table: string, columns: string, email: string) {
  const key = email.trim().toLowerCase();
  const exact = await sb.from(table).select(columns).eq("email", key).maybeSingle();
  if (exact.error) {
    console.error(`[verify-player] ${table} eq`, exact.error);
  }
  if (exact.data) return exact.data;

  const fuzzy = await sb.from(table).select(columns).ilike("email", escapeIlike(key)).maybeSingle();
  if (fuzzy.error) {
    console.error(`[verify-player] ${table} ilike`, fuzzy.error);
  }
  return fuzzy.data ?? null;
}

async function resolvePlayerSkins(email: string): Promise<string[]> {
  const key = email.trim().toLowerCase();
  if (!key) return [];

  const sb = supabaseAdmin();
  if (sb) {
    const result = await sb.from("kaplao_player_skins").select("skin_id").eq("email", key);
    if (result.error) {
      const msg = (result.error.message || "").toLowerCase();
      if (
        result.error.code === "42P01" ||
        result.error.code === "PGRST205" ||
        msg.includes("does not exist")
      ) {
        return [];
      }
      console.error("[verify-player] kaplao_player_skins", result.error);
      return [];
    }
    return (result.data ?? [])
      .map((row) => String((row as { skin_id?: string }).skin_id || "").toLowerCase())
      .filter(Boolean);
  }

  return [];
}

async function resolvePlayerRole(email: string) {
  const key = email.trim().toLowerCase();
  const isSuperAdmin = key === SUPER_ADMIN_EMAIL.toLowerCase();

  const sb = supabaseAdmin();
  if (sb) {
    const roleRow = (await lookupByEmail(
      sb,
      "kaplao_player_roles",
      "permission,nickname_preset,rights",
      key
    )) as { permission?: string; nickname_preset?: string; rights?: unknown } | null;
    const legacyRow = await lookupByEmail(sb, "kaplao_admins", "email", key);
    const nicknamePreset = normalizePreset(roleRow?.nickname_preset ?? DEFAULT_NICKNAME_PRESET);

    if (isSuperAdmin) {
      return {
        permission: "admin" as PlayerPermission,
        rights: [...ALL_RIGHTS] as PlayerRight[],
        nicknamePreset,
        isAdmin: true,
        canDeleteLevels: true,
      };
    }

    let rights = normalizeRights(roleRow?.rights);
    if (rights.length === 0) {
      let permission = normalizePermission(String(roleRow?.permission ?? DEFAULT_PERMISSION));
      if (permission === "none" && legacyRow) permission = "admin";
      else if (!roleRow && legacyRow) permission = "admin";
      rights = rightsFromPermission(permission);
    }

    return {
      rights,
      permission: derivePermission(rights),
      nicknamePreset,
      isAdmin: rights.includes("game_host"),
      canDeleteLevels: rights.includes("level_purge"),
    };
  }

  if (isSuperAdmin) {
    return {
      permission: "admin" as PlayerPermission,
      rights: [...ALL_RIGHTS] as PlayerRight[],
      nicknamePreset: DEFAULT_NICKNAME_PRESET,
      isAdmin: true,
      canDeleteLevels: true,
    };
  }

  return {
    permission: DEFAULT_PERMISSION as PlayerPermission,
    rights: [] as PlayerRight[],
    nicknamePreset: DEFAULT_NICKNAME_PRESET,
    isAdmin: false,
    canDeleteLevels: false,
  };
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
    if (!token) {
      return json({
        valid: false,
        email: "",
        nickname: "",
        isAdmin: false,
        permission: DEFAULT_PERMISSION,
        rights: [] as string[],
        canDeleteLevels: false,
        nicknamePreset: DEFAULT_NICKNAME_PRESET,
        ownedSkinIds: [] as string[],
      });
    }

    const session = await sessionFromAuthToken(token);
    if (!session) {
      return json({
        valid: false,
        email: "",
        nickname: "",
        isAdmin: false,
        permission: DEFAULT_PERMISSION,
        rights: [] as string[],
        canDeleteLevels: false,
        nicknamePreset: DEFAULT_NICKNAME_PRESET,
        ownedSkinIds: [] as string[],
      });
    }

    const role = await resolvePlayerRole(session.email);
    const ownedSkinIds = await resolvePlayerSkins(session.email);
    return json({
      valid: true,
      email: session.email,
      nickname: session.nickname,
      isAdmin: role.isAdmin,
      permission: role.permission,
      rights: role.rights,
      canDeleteLevels: role.canDeleteLevels,
      nicknamePreset: role.nicknamePreset,
      ownedSkinIds,
    });
  } catch (error) {
    console.error("[verify-player]", error);
    return json({ error: "Verification failed." }, 500);
  }
});
