import { getSupabase, isSupabaseConfigured } from "./supabase";
import { ADMIN_SKIN_IDS, normalizeAdminSkinIds, type AdminSkinId } from "./runner-skins";

export type PlayerSkinGrant = {
  email: string;
  skinId: AdminSkinId;
  grantedAt: string;
  grantedBy?: string;
};

type FileFs = typeof import("fs/promises");
type FilePath = typeof import("path");

type PlayerSkinsFileShape = {
  grants: PlayerSkinGrant[];
};

let memoryGrants: PlayerSkinGrant[] | null = null;

function throwIfSbError(error: { message?: string; code?: string; hint?: string } | null, action: string) {
  if (!error) return;
  const parts = [action, error.code, error.message, error.hint].filter(Boolean);
  throw new Error(parts.join(" | "));
}

function isMissingRelationError(error: { message?: string; code?: string } | null): boolean {
  if (!error) return false;
  if (error.code === "42P01" || error.code === "PGRST205" || error.code === "PGRST116") return true;
  const msg = (error.message || "").toLowerCase();
  return msg.includes("does not exist") || msg.includes("could not find the table");
}

async function loadFs(): Promise<{ fs: FileFs; path: FilePath } | null> {
  try {
    const [fs, path] = await Promise.all([import("fs/promises"), import("path")]);
    return { fs, path };
  } catch {
    return null;
  }
}

function dataFilePath(pathMod: FilePath) {
  const dir = process.env.DATA_DIR || pathMod.join(process.cwd(), "data");
  return { dir, file: pathMod.join(dir, "player-skins.json") };
}

async function readFileGrants(): Promise<PlayerSkinGrant[]> {
  const mods = await loadFs();
  if (!mods) {
    if (!memoryGrants) memoryGrants = [];
    return memoryGrants;
  }

  const { fs, path } = mods;
  const { dir, file } = dataFilePath(path);
  await fs.mkdir(dir, { recursive: true });
  try {
    const raw = await fs.readFile(file, "utf8");
    const parsed = JSON.parse(raw) as PlayerSkinsFileShape;
    return parsed.grants ?? [];
  } catch {
    const empty: PlayerSkinsFileShape = { grants: [] };
    await fs.writeFile(file, JSON.stringify(empty, null, 2), "utf8");
    return [];
  }
}

async function writeFileGrants(grants: PlayerSkinGrant[]): Promise<void> {
  const mods = await loadFs();
  if (!mods) {
    memoryGrants = grants;
    return;
  }

  const { fs, path } = mods;
  const { dir, file } = dataFilePath(path);
  await fs.mkdir(dir, { recursive: true });
  await fs.writeFile(file, JSON.stringify({ grants }, null, 2), "utf8");
}

type SbSkinRow = {
  email: string;
  skin_id: string;
  granted_at: string;
  granted_by: string | null;
};

function rowToGrant(row: SbSkinRow): PlayerSkinGrant {
  return {
    email: row.email.toLowerCase(),
    skinId: row.skin_id as AdminSkinId,
    grantedAt: row.granted_at,
    grantedBy: row.granted_by ?? undefined,
  };
}

export async function getPlayerSkinIds(email: string): Promise<AdminSkinId[]> {
  const key = email.trim().toLowerCase();
  if (!key) return [];

  const sb = getSupabase();
  if (sb) {
    const result = await sb
      .from("kaplao_player_skins")
      .select("skin_id")
      .eq("email", key);
    if (isMissingRelationError(result.error)) return [];
    throwIfSbError(result.error, "getPlayerSkinIds");

    const ids = (result.data ?? [])
      .map((row) => String((row as { skin_id?: string }).skin_id || "").toLowerCase())
      .filter((id): id is AdminSkinId => ADMIN_SKIN_IDS.includes(id as AdminSkinId));
    return ADMIN_SKIN_IDS.filter((id) => ids.includes(id));
  }

  const grants = await readFileGrants();
  const owned = new Set(
    grants.filter((g) => g.email === key).map((g) => g.skinId)
  );
  return ADMIN_SKIN_IDS.filter((id) => owned.has(id));
}

export async function setPlayerSkinIds(
  email: string,
  skinIds: unknown,
  grantedBy?: string
): Promise<AdminSkinId[]> {
  const key = email.trim().toLowerCase();
  if (!key) throw new Error("Email required.");

  const normalized = normalizeAdminSkinIds(skinIds);
  const now = new Date().toISOString();
  const by = grantedBy?.trim().toLowerCase();

  const sb = getSupabase();
  if (sb) {
    const del = await sb.from("kaplao_player_skins").delete().eq("email", key);
    if (!isMissingRelationError(del.error)) throwIfSbError(del.error, "setPlayerSkinIds.delete");

    if (normalized.length > 0) {
      const rows = normalized.map((skinId) => ({
        email: key,
        skin_id: skinId,
        granted_at: now,
        granted_by: by ?? null,
      }));
      const ins = await sb.from("kaplao_player_skins").insert(rows);
      if (!isMissingRelationError(ins.error)) throwIfSbError(ins.error, "setPlayerSkinIds.insert");
    }

    return normalized;
  }

  const grants = (await readFileGrants()).filter((g) => g.email !== key);
  for (const skinId of normalized) {
    grants.push({ email: key, skinId, grantedAt: now, grantedBy: by });
  }
  await writeFileGrants(grants);
  return normalized;
}

export async function deletePlayerSkins(email: string): Promise<void> {
  const key = email.trim().toLowerCase();
  if (!key) return;

  const sb = getSupabase();
  if (sb) {
    const result = await sb.from("kaplao_player_skins").delete().eq("email", key);
    if (!isMissingRelationError(result.error)) throwIfSbError(result.error, "deletePlayerSkins");
    return;
  }

  const grants = (await readFileGrants()).filter((g) => g.email !== key);
  await writeFileGrants(grants);
}

export async function listPlayerSkinsByEmail(): Promise<Map<string, AdminSkinId[]>> {
  const map = new Map<string, AdminSkinId[]>();

  const sb = getSupabase();
  if (sb) {
    const result = await sb.from("kaplao_player_skins").select("email,skin_id");
    if (isMissingRelationError(result.error)) return map;
    throwIfSbError(result.error, "listPlayerSkinsByEmail");

    for (const row of result.data ?? []) {
      const email = String((row as { email?: string }).email || "").toLowerCase();
      const skinId = String((row as { skin_id?: string }).skin_id || "").toLowerCase();
      if (!email || !ADMIN_SKIN_IDS.includes(skinId as AdminSkinId)) continue;
      const list = map.get(email) ?? [];
      if (!list.includes(skinId as AdminSkinId)) list.push(skinId as AdminSkinId);
      map.set(email, list);
    }

    for (const [email, ids] of map.entries()) {
      map.set(email, ADMIN_SKIN_IDS.filter((id) => ids.includes(id)));
    }
    return map;
  }

  const grants = await readFileGrants();
  for (const grant of grants) {
    const list = map.get(grant.email) ?? [];
    if (!list.includes(grant.skinId)) list.push(grant.skinId);
    map.set(grant.email, list);
  }
  for (const [email, ids] of map.entries()) {
    map.set(email, ADMIN_SKIN_IDS.filter((id) => ids.includes(id)));
  }
  return map;
}

export function isPlayerSkinsStorageConfigured(): boolean {
  return isSupabaseConfigured() || true;
}
