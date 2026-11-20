import { DatabaseShape, AdminRecord, DeviceSession, UserRecord, VerificationCode, PlayerRoleRecord, NicknamePresetRecord, PlayerWithRole, DevChatMessage, DevChatConversation } from "./types";
import { getSupabase, isSupabaseConfigured } from "./supabase";
import {
  BUILTIN_NICKNAME_PRESETS,
  DEFAULT_NICKNAME_PRESET,
  DEFAULT_PLAYER_PERMISSION,
  DEFAULT_PLAYER_RIGHTS,
  deriveLegacyPermission,
  isBuiltinNicknamePreset,
  isValidNicknamePresetName,
  normalizeNicknamePresetName,
  normalizePermission,
  normalizePlayerRights,
  rightsFromLegacyPermission,
  rightsGrantGameAdmin,
  type PlayerPermission,
  type PlayerRight,
} from "./player-roles";
import {
  EMPTY_NICKNAME_STYLE,
  normalizeNicknamePresetStyle,
  type NicknamePresetStyle,
} from "./nickname-preset-style";

const emptyDb = (): DatabaseShape => ({
  users: [],
  codes: [],
  devices: [],
  admins: [],
  playerRoles: [],
  nicknamePresets: [],
  devMessages: [],
});

type FileFs = typeof import("fs/promises");
type FilePath = typeof import("path");

let memoryDb: DatabaseShape | null = null;

type SbUserRow = {
  id: string;
  nickname: string;
  email: string;
  password_hash: string;
  verified: boolean;
  created_at: string;
};

function rowToUser(row: SbUserRow): UserRecord {
  return {
    id: row.id,
    nickname: row.nickname,
    email: row.email,
    passwordHash: row.password_hash,
    verified: row.verified,
    createdAt: row.created_at,
  };
}

function throwIfSbError(error: { message?: string; code?: string; hint?: string } | null, action: string) {
  if (!error) return;
  const parts = [action, error.code, error.message, error.hint].filter(Boolean);
  throw new Error(parts.join(" | "));
}

/** New tables may not exist yet — treat as empty instead of blocking login. */
function isMissingRelationError(error: { message?: string; code?: string } | null): boolean {
  if (!error) return false;
  if (error.code === "42P01" || error.code === "PGRST205" || error.code === "PGRST116") return true;
  const msg = (error.message || "").toLowerCase();
  return msg.includes("does not exist") || msg.includes("could not find the table");
}

/** Escape % and _ for PostgREST ilike exact match */
function ilikeExact(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/%/g, "\\%").replace(/_/g, "\\_");
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
  return { dir, file: pathMod.join(dir, "db.json") };
}

async function readFileDb(): Promise<DatabaseShape> {
  const mods = await loadFs();
  if (!mods) {
    if (!memoryDb) memoryDb = emptyDb();
    return memoryDb;
  }

  const { fs, path } = mods;
  const { dir, file } = dataFilePath(path);
  await fs.mkdir(dir, { recursive: true });
  try {
    const raw = await fs.readFile(file, "utf8");
    const parsed = JSON.parse(raw) as DatabaseShape;
    return {
      users: parsed.users ?? [],
      codes: parsed.codes ?? [],
      devices: parsed.devices ?? [],
      admins: parsed.admins ?? [],
      playerRoles: parsed.playerRoles ?? [],
      nicknamePresets: (parsed.nicknamePresets ?? []).map((preset) => ({
        name: normalizeNicknamePresetName(preset.name || ""),
        createdAt: preset.createdAt || new Date().toISOString(),
        createdBy: preset.createdBy,
        style: normalizeNicknamePresetStyle(preset.style ?? EMPTY_NICKNAME_STYLE),
      })),
    };
  } catch {
    const empty = emptyDb();
    await fs.writeFile(file, JSON.stringify(empty, null, 2), "utf8");
    return empty;
  }
}

async function writeFileDb(db: DatabaseShape): Promise<void> {
  const mods = await loadFs();
  if (!mods) {
    memoryDb = db;
    return;
  }

  const { fs, path } = mods;
  const { dir, file } = dataFilePath(path);
  await fs.mkdir(dir, { recursive: true });
  await fs.writeFile(file, JSON.stringify(db, null, 2), "utf8");
}

export async function listUsers(): Promise<UserRecord[]> {
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb.from("kaplao_users").select("*");
    throwIfSbError(error, "listUsers");
    return (data as SbUserRow[]).map(rowToUser);
  }
  return (await readFileDb()).users;
}

export async function findUserByEmailOrNickname(login: string): Promise<UserRecord | undefined> {
  const key = login.trim().toLowerCase();
  const byEmail = await findUserByEmail(key);
  if (byEmail) return byEmail;
  return findUserByNickname(login.trim());
}

export async function findUserByNickname(nickname: string): Promise<UserRecord | undefined> {
  const raw = nickname.trim();
  if (!raw) return undefined;
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_users")
      .select("*")
      .ilike("nickname", ilikeExact(raw))
      .maybeSingle();
    throwIfSbError(error, "findUserByNickname");
    return data ? rowToUser(data as SbUserRow) : undefined;
  }
  const key = raw.toLowerCase();
  return (await readFileDb()).users.find((u) => u.nickname.toLowerCase() === key);
}

export async function findUserByEmail(email: string): Promise<UserRecord | undefined> {
  const key = email.trim().toLowerCase();
  if (!key) return undefined;
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_users")
      .select("*")
      .eq("email", key)
      .maybeSingle();
    throwIfSbError(error, "findUserByEmail");
    return data ? rowToUser(data as SbUserRow) : undefined;
  }
  return (await readFileDb()).users.find((u) => u.email.toLowerCase() === key);
}

export async function upsertUser(user: UserRecord): Promise<void> {
  const sb = getSupabase();
  if (sb) {
    const row = {
      id: user.id,
      nickname: user.nickname,
      email: user.email.toLowerCase(),
      password_hash: user.passwordHash,
      verified: user.verified,
      created_at: user.createdAt,
    };

    // Prefer update-then-insert to avoid onConflict quirks with expression indexes.
    const existing = await sb.from("kaplao_users").select("id").eq("id", user.id).maybeSingle();
    throwIfSbError(existing.error, "upsertUser.lookup");

    if (existing.data) {
      const { error } = await sb.from("kaplao_users").update(row).eq("id", user.id);
      throwIfSbError(error, "upsertUser.update");
    } else {
      const { error } = await sb.from("kaplao_users").insert(row);
      throwIfSbError(error, "upsertUser.insert");
    }
    return;
  }

  const db = await readFileDb();
  const index = db.users.findIndex((u) => u.id === user.id);
  if (index >= 0) db.users[index] = user;
  else db.users.push(user);
  await writeFileDb(db);
}

export async function saveVerificationCode(entry: VerificationCode): Promise<void> {
  const sb = getSupabase();
  if (sb) {
    const row = {
      email: entry.email.toLowerCase(),
      code: entry.code,
      expires_at: entry.expiresAt,
      purpose: entry.purpose || "verify_email",
    };
    const existing = await sb
      .from("kaplao_verification_codes")
      .select("email")
      .eq("email", row.email)
      .maybeSingle();
    throwIfSbError(existing.error, "saveVerificationCode.lookup");

    if (existing.data) {
      const { error } = await sb
        .from("kaplao_verification_codes")
        .update(row)
        .eq("email", row.email);
      // Older DBs may not have purpose yet — retry without it.
      if (error && String(error.message || "").toLowerCase().includes("purpose")) {
        const { purpose: _purpose, ...legacyRow } = row;
        const retry = await sb
          .from("kaplao_verification_codes")
          .update(legacyRow)
          .eq("email", row.email);
        throwIfSbError(retry.error, "saveVerificationCode.update.legacy");
      } else {
        throwIfSbError(error, "saveVerificationCode.update");
      }
    } else {
      const { error } = await sb.from("kaplao_verification_codes").insert(row);
      if (error && String(error.message || "").toLowerCase().includes("purpose")) {
        const { purpose: _purpose, ...legacyRow } = row;
        const retry = await sb.from("kaplao_verification_codes").insert(legacyRow);
        throwIfSbError(retry.error, "saveVerificationCode.insert.legacy");
      } else {
        throwIfSbError(error, "saveVerificationCode.insert");
      }
    }
    return;
  }

  const db = await readFileDb();
  db.codes = db.codes.filter((c) => c.email.toLowerCase() !== entry.email.toLowerCase());
  db.codes.push(entry);
  await writeFileDb(db);
}

export async function consumeVerificationCode(
  email: string,
  code: string,
  purpose: import("./types").VerificationPurpose = "verify_email"
): Promise<boolean> {
  const key = email.trim().toLowerCase();
  const expectedPurpose = purpose || "verify_email";
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_verification_codes")
      .select("*")
      .eq("email", key)
      .maybeSingle();
    throwIfSbError(error, "consumeVerificationCode.lookup");
    if (!data || data.code !== code.trim() || Date.parse(data.expires_at as string) < Date.now()) {
      return false;
    }
    const rowPurpose = String((data as { purpose?: string }).purpose || "verify_email");
    if (rowPurpose !== expectedPurpose) {
      return false;
    }
    const del = await sb.from("kaplao_verification_codes").delete().eq("email", key);
    throwIfSbError(del.error, "consumeVerificationCode.delete");
    return true;
  }

  const db = await readFileDb();
  const now = Date.now();
  const match = db.codes.find(
    (c) =>
      c.email.toLowerCase() === key &&
      c.code === code.trim() &&
      Date.parse(c.expiresAt) >= now &&
      (c.purpose || "verify_email") === expectedPurpose
  );
  if (!match) return false;
  db.codes = db.codes.filter((c) => c.email.toLowerCase() !== key);
  await writeFileDb(db);
  return true;
}

export async function updateUserNickname(userId: string, nickname: string): Promise<UserRecord | null> {
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_users")
      .update({ nickname })
      .eq("id", userId)
      .select("*")
      .single();
    throwIfSbError(error, "updateUserNickname");
    return data ? rowToUser(data as SbUserRow) : null;
  }

  const db = await readFileDb();
  const index = db.users.findIndex((u) => u.id === userId);
  if (index < 0) return null;
  db.users[index] = { ...db.users[index], nickname };
  await writeFileDb(db);
  return db.users[index];
}

export async function updateUserPassword(userId: string, passwordHash: string): Promise<boolean> {
  const sb = getSupabase();
  if (sb) {
    const { error } = await sb
      .from("kaplao_users")
      .update({ password_hash: passwordHash })
      .eq("id", userId);
    throwIfSbError(error, "updateUserPassword");
    return true;
  }

  const db = await readFileDb();
  const index = db.users.findIndex((u) => u.id === userId);
  if (index < 0) return false;
  db.users[index] = { ...db.users[index], passwordHash };
  await writeFileDb(db);
  return true;
}

export async function createDeviceSession(session: DeviceSession): Promise<void> {
  const sb = getSupabase();
  if (sb) {
    const row = {
      code: session.code.toUpperCase(),
      status: session.status,
      expires_at: session.expiresAt,
      token: session.token ?? null,
      nickname: session.nickname ?? null,
      email: session.email ?? null,
    };

    // Upsert + select: plain update() can report success with 0 rows under RLS.
    const { data, error } = await sb
      .from("kaplao_device_sessions")
      .upsert(row, { onConflict: "code" })
      .select("code,status,token,nickname,email,expires_at")
      .single();
    throwIfSbError(error, "createDeviceSession.upsert");

    if (!data || data.status !== row.status) {
      throw new Error(
        `createDeviceSession.upsert did not stick (got status=${data?.status ?? "null"})`
      );
    }
    return;
  }

  const db = await readFileDb();
  db.devices = db.devices.filter((d) => Date.parse(d.expiresAt) >= Date.now());
  const index = db.devices.findIndex((d) => d.code.toUpperCase() === session.code.toUpperCase());
  if (index >= 0) db.devices[index] = { ...session, code: session.code.toUpperCase() };
  else db.devices.push({ ...session, code: session.code.toUpperCase() });
  await writeFileDb(db);
}

export async function getDeviceSession(code: string): Promise<DeviceSession | undefined> {
  const key = code.trim().toUpperCase();
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_device_sessions")
      .select("*")
      .eq("code", key)
      .maybeSingle();
    throwIfSbError(error, "getDeviceSession");
    if (!data) return undefined;
    return {
      code: data.code as string,
      status: data.status as DeviceSession["status"],
      expiresAt: data.expires_at as string,
      token: (data.token as string | null) ?? undefined,
      nickname: (data.nickname as string | null) ?? undefined,
      email: (data.email as string | null) ?? undefined,
    };
  }

  const db = await readFileDb();
  return db.devices.find((d) => d.code.toUpperCase() === key);
}

export async function updateDeviceSession(session: DeviceSession): Promise<void> {
  const normalized: DeviceSession = {
    ...session,
    code: session.code.toUpperCase(),
  };
  await createDeviceSession(normalized);
}

export function usingSupabase(): boolean {
  return isSupabaseConfigured();
}

type SbAdminRow = {
  email: string;
  created_at: string;
  created_by: string | null;
};

function rowToAdmin(row: SbAdminRow): AdminRecord {
  return {
    email: row.email,
    createdAt: row.created_at,
    createdBy: row.created_by ?? undefined,
  };
}

export async function listAdmins(): Promise<AdminRecord[]> {
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_admins")
      .select("*")
      .order("created_at", { ascending: true });
    throwIfSbError(error, "listAdmins");
    return (data as SbAdminRow[]).map(rowToAdmin);
  }
  return (await readFileDb()).admins;
}

export async function isAdminEmail(email: string): Promise<boolean> {
  const key = email.trim().toLowerCase();
  if (!key) return false;

  try {
    const role = await getPlayerRole(key);
    if (role && rightsGrantGameAdmin(role.rights)) return true;
  } catch {
    // Fall through to legacy kaplao_admins table.
  }

  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_admins")
      .select("email")
      .eq("email", key)
      .maybeSingle();
    throwIfSbError(error, "isAdminEmail");
    return !!data;
  }
  const db = await readFileDb();
  return db.admins.some((a) => a.email.toLowerCase() === key);
}

export async function addAdminEmail(email: string, createdBy: string): Promise<AdminRecord> {
  const key = email.trim().toLowerCase();
  if (!key) throw new Error("Email required.");

  const record: AdminRecord = {
    email: key,
    createdAt: new Date().toISOString(),
    createdBy: createdBy.trim().toLowerCase(),
  };

  const sb = getSupabase();
  if (sb) {
    const row = {
      email: key,
      created_at: record.createdAt,
      created_by: record.createdBy ?? null,
    };
    const existing = await sb.from("kaplao_admins").select("email").eq("email", key).maybeSingle();
    throwIfSbError(existing.error, "addAdminEmail.lookup");
    if (existing.data) {
      await syncLegacyAdminRole(key, "admin", record.createdBy);
      return rowToAdmin(existing.data as SbAdminRow);
    }

    const { data, error } = await sb.from("kaplao_admins").insert(row).select("*").single();
    throwIfSbError(error, "addAdminEmail.insert");
    await syncLegacyAdminRole(key, "admin", record.createdBy);
    return rowToAdmin(data as SbAdminRow);
  }

  const db = await readFileDb();
  if (!db.admins.some((a) => a.email.toLowerCase() === key)) db.admins.push(record);
  await syncLegacyAdminRole(key, "admin", record.createdBy);
  await writeFileDb(db);
  return record;
}

export async function removeAdminEmail(email: string): Promise<boolean> {
  const key = email.trim().toLowerCase();
  if (!key) return false;

  const sb = getSupabase();
  if (sb) {
    const { error } = await sb.from("kaplao_admins").delete().eq("email", key);
    throwIfSbError(error, "removeAdminEmail");
    await syncLegacyAdminRole(key, "none");
    return true;
  }

  const db = await readFileDb();
  const before = db.admins.length;
  db.admins = db.admins.filter((a) => a.email.toLowerCase() !== key);
  if (db.admins.length === before) return false;
  await syncLegacyAdminRole(key, "none");
  await writeFileDb(db);
  return true;
}

type SbPlayerRoleRow = {
  email: string;
  permission: string;
  rights?: unknown;
  nickname_preset: string;
  updated_at: string;
  updated_by: string | null;
};

function resolveRightsFromRow(row: SbPlayerRoleRow | PlayerRoleRecord): PlayerRight[] {
  const fromColumn = normalizePlayerRights(
    "rights" in row ? (row as SbPlayerRoleRow).rights ?? (row as PlayerRoleRecord).rights : []
  );
  if (fromColumn.length > 0) return fromColumn;

  const permission =
    normalizePermission(
      "permission" in row ? String((row as SbPlayerRoleRow).permission || "") : ""
    ) ?? DEFAULT_PLAYER_PERMISSION;
  if (permission !== "none") return rightsFromLegacyPermission(permission);
  return DEFAULT_PLAYER_RIGHTS;
}

function rowToPlayerRole(row: SbPlayerRoleRow): PlayerRoleRecord {
  const rights = resolveRightsFromRow(row);
  return {
    email: row.email,
    rights,
    permission: deriveLegacyPermission(rights),
    nicknamePreset: normalizeNicknamePresetName(row.nickname_preset || DEFAULT_NICKNAME_PRESET),
    updatedAt: row.updated_at,
    updatedBy: row.updated_by ?? undefined,
  };
}

function rowToNicknamePreset(row: SbNicknamePresetRow): NicknamePresetRecord {
  return {
    name: normalizeNicknamePresetName(row.name),
    createdAt: row.created_at,
    createdBy: row.created_by ?? undefined,
    style: normalizeNicknamePresetStyle(row.style ?? EMPTY_NICKNAME_STYLE),
  };
}

type SbNicknamePresetRow = {
  name: string;
  created_at: string;
  created_by: string | null;
  style?: unknown;
};

async function syncLegacyAdminRole(
  email: string,
  permission: PlayerPermission,
  updatedBy?: string
): Promise<void> {
  const key = email.trim().toLowerCase();
  if (!key) return;

  if (permission === "admin") {
    const existing = await getPlayerRole(key);
    const merged = new Set<PlayerRight>([...(existing?.rights ?? []), "level_purge", "game_host"]);
    const rights = normalizePlayerRights(Array.from(merged));
    await upsertPlayerRole(
      key,
      rights,
      existing?.nicknamePreset ?? DEFAULT_NICKNAME_PRESET,
      updatedBy
    );
    return;
  }

  const existing = await getPlayerRole(key);
  if (existing && rightsGrantGameAdmin(existing.rights)) {
    const rights = existing.rights.filter((r) => r !== "game_host" && r !== "level_purge");
    await upsertPlayerRole(key, rights, existing.nicknamePreset, updatedBy);
  }
}

export async function getPlayerRole(email: string): Promise<PlayerRoleRecord | undefined> {
  const key = email.trim().toLowerCase();
  if (!key) return undefined;

  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_player_roles")
      .select("*")
      .eq("email", key)
      .maybeSingle();
    if (isMissingRelationError(error)) return undefined;
    throwIfSbError(error, "getPlayerRole");
    return data ? rowToPlayerRole(data as SbPlayerRoleRow) : undefined;
  }

  const db = await readFileDb();
  const found = db.playerRoles.find((r) => r.email.toLowerCase() === key);
  if (!found) return undefined;
  const rights =
    found.rights && found.rights.length > 0
      ? normalizePlayerRights(found.rights)
      : rightsFromLegacyPermission(found.permission);
  return {
    ...found,
    rights,
    permission: deriveLegacyPermission(rights),
  };
}

export async function listPlayerRoles(): Promise<PlayerRoleRecord[]> {
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_player_roles")
      .select("*")
      .order("email", { ascending: true });
    if (isMissingRelationError(error)) return [];
    throwIfSbError(error, "listPlayerRoles");
    return (data as SbPlayerRoleRow[]).map(rowToPlayerRole);
  }
  return (await readFileDb()).playerRoles;
}

export async function listPlayersWithRoles(): Promise<PlayerWithRole[]> {
  const [users, roles, admins] = await Promise.all([
    listUsers(),
    listPlayerRoles(),
    listAdmins().catch(() => [] as AdminRecord[]),
  ]);
  const roleByEmail = new Map(roles.map((r) => [r.email.toLowerCase(), r]));
  const adminEmailList = admins.map((a) => a.email.toLowerCase());
  const adminEmailSet = new Set(adminEmailList);

  // Legacy kaplao_admins rows must keep Game Host in the UI.
  for (let i = 0; i < adminEmailList.length; i++) {
    const adminEmail = adminEmailList[i];
    if (roleByEmail.has(adminEmail)) continue;
    try {
      const saved = await upsertPlayerRole(
        adminEmail,
        ["level_purge", "game_host"],
        DEFAULT_NICKNAME_PRESET,
        "legacy_admins"
      );
      roleByEmail.set(adminEmail, saved);
    } catch (error) {
      console.error("ensureLegacyAdminRole", adminEmail, error);
      roleByEmail.set(adminEmail, {
        email: adminEmail,
        permission: "admin",
        rights: ["level_purge", "game_host"],
        nicknamePreset: DEFAULT_NICKNAME_PRESET,
        updatedAt: new Date().toISOString(),
        updatedBy: "legacy_admins",
      });
    }
  }

  return users
    .map((user) => {
      const key = user.email.toLowerCase();
      const role = roleByEmail.get(key);
      const isLegacyAdmin = adminEmailSet.has(key);
      let rights = role?.rights ? normalizePlayerRights(role.rights) : DEFAULT_PLAYER_RIGHTS;
      if (rights.length === 0 && isLegacyAdmin) {
        rights = ["level_purge", "game_host"];
      }
      return {
        ...user,
        rights,
        permission: deriveLegacyPermission(rights),
        nicknamePreset: role?.nicknamePreset ?? DEFAULT_NICKNAME_PRESET,
      };
    })
    .sort((a, b) => a.email.localeCompare(b.email));
}

export async function upsertPlayerRole(
  email: string,
  rightsInput: PlayerRight[] | PlayerPermission,
  nicknamePreset: string,
  updatedBy?: string
): Promise<PlayerRoleRecord> {
  const key = email.trim().toLowerCase();

  let rights: PlayerRight[];
  if (typeof rightsInput === "string") {
    const permission = normalizePermission(rightsInput);
    if (!permission) throw new Error("Invalid permission.");
    rights = rightsFromLegacyPermission(permission);
  } else {
    rights = normalizePlayerRights(rightsInput);
  }

  const permission = deriveLegacyPermission(rights);
  const preset = normalizeNicknamePresetName(nicknamePreset || DEFAULT_NICKNAME_PRESET);
  if (!isBuiltinNicknamePreset(preset)) {
    const presets = await listNicknamePresets();
    if (!presets.some((p) => p.name === preset)) {
      throw new Error(`Unknown nickname preset "${preset}". Add it on the Presets tab first.`);
    }
  }

  const record: PlayerRoleRecord = {
    email: key,
    rights,
    permission,
    nicknamePreset: preset,
    updatedAt: new Date().toISOString(),
    updatedBy: updatedBy?.trim().toLowerCase(),
  };

  const sb = getSupabase();
  if (sb) {
    const row = {
      email: key,
      permission: record.permission,
      rights: record.rights,
      nickname_preset: record.nicknamePreset,
      updated_at: record.updatedAt,
      updated_by: record.updatedBy ?? null,
    };
    const existing = await sb.from("kaplao_player_roles").select("email").eq("email", key).maybeSingle();
    throwIfSbError(existing.error, "upsertPlayerRole.lookup");

    if (existing.data) {
      const { data, error } = await sb
        .from("kaplao_player_roles")
        .update(row)
        .eq("email", key)
        .select("*")
        .single();
      throwIfSbError(error, "upsertPlayerRole.update");
      const saved = rowToPlayerRole(data as SbPlayerRoleRow);
      await syncKaplaoAdminsTable(key, saved.rights);
      return saved;
    }

    const { data, error } = await sb.from("kaplao_player_roles").insert(row).select("*").single();
    throwIfSbError(error, "upsertPlayerRole.insert");
    const saved = rowToPlayerRole(data as SbPlayerRoleRow);
    await syncKaplaoAdminsTable(key, saved.rights);
    return saved;
  }

  const db = await readFileDb();
  const index = db.playerRoles.findIndex((r) => r.email.toLowerCase() === key);
  if (index >= 0) db.playerRoles[index] = record;
  else db.playerRoles.push(record);
  await syncKaplaoAdminsTable(key, record.rights, db);
  await writeFileDb(db);
  return record;
}

async function syncKaplaoAdminsTable(
  email: string,
  rights: PlayerRight[],
  db?: DatabaseShape
): Promise<void> {
  const key = email.trim().toLowerCase();
  const sb = getSupabase();
  const isGameHost = rightsGrantGameAdmin(rights);

  if (isGameHost) {
    if (sb) {
      const existing = await sb.from("kaplao_admins").select("email").eq("email", key).maybeSingle();
      throwIfSbError(existing.error, "syncKaplaoAdminsTable.lookup");
      if (!existing.data) {
        const { error } = await sb.from("kaplao_admins").insert({
          email: key,
          created_at: new Date().toISOString(),
          created_by: "player_roles",
        });
        throwIfSbError(error, "syncKaplaoAdminsTable.insert");
      }
      return;
    }

    const fileDb = db ?? (await readFileDb());
    if (!fileDb.admins.some((a) => a.email.toLowerCase() === key)) {
      fileDb.admins.push({
        email: key,
        createdAt: new Date().toISOString(),
        createdBy: "player_roles",
      });
      if (!db) await writeFileDb(fileDb);
    }
    return;
  }

  if (sb) {
    const { error } = await sb.from("kaplao_admins").delete().eq("email", key);
    throwIfSbError(error, "syncKaplaoAdminsTable.delete");
    return;
  }

  const fileDb = db ?? (await readFileDb());
  fileDb.admins = fileDb.admins.filter((a) => a.email.toLowerCase() !== key);
  if (!db) await writeFileDb(fileDb);
}

export async function listNicknamePresets(): Promise<NicknamePresetRecord[]> {
  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_nickname_presets")
      .select("*")
      .order("name", { ascending: true });
    if (isMissingRelationError(error)) return [];
    throwIfSbError(error, "listNicknamePresets");
    return (data as SbNicknamePresetRow[]).map(rowToNicknamePreset);
  }
  return (await readFileDb()).nicknamePresets;
}

export async function listAllNicknamePresetNames(): Promise<string[]> {
  const custom = await listNicknamePresets();
  const names = new Set<string>(BUILTIN_NICKNAME_PRESETS);
  for (const preset of custom) names.add(preset.name);
  return Array.from(names).sort((a, b) => a.localeCompare(b));
}

export async function addNicknamePreset(
  name: string,
  createdBy: string,
  style?: NicknamePresetStyle
): Promise<NicknamePresetRecord> {
  const key = normalizeNicknamePresetName(name);
  if (!isValidNicknamePresetName(key)) throw new Error("Invalid preset name.");
  if (isBuiltinNicknamePreset(key)) throw new Error("Built-in preset names cannot be re-added.");

  const normalizedStyle = normalizeNicknamePresetStyle(style ?? EMPTY_NICKNAME_STYLE);
  const record: NicknamePresetRecord = {
    name: key,
    createdAt: new Date().toISOString(),
    createdBy: createdBy.trim().toLowerCase(),
    style: normalizedStyle,
  };

  const sb = getSupabase();
  if (sb) {
    const existing = await sb.from("kaplao_nickname_presets").select("*").eq("name", key).maybeSingle();
    throwIfSbError(existing.error, "addNicknamePreset.lookup");
    if (existing.data) return rowToNicknamePreset(existing.data as SbNicknamePresetRow);

    const { data, error } = await sb
      .from("kaplao_nickname_presets")
      .insert({
        name: key,
        created_at: record.createdAt,
        created_by: record.createdBy ?? null,
        style: normalizedStyle,
      })
      .select("*")
      .single();
    throwIfSbError(error, "addNicknamePreset.insert");
    return rowToNicknamePreset(data as SbNicknamePresetRow);
  }

  const db = await readFileDb();
  if (!db.nicknamePresets.some((p) => p.name === key)) db.nicknamePresets.push(record);
  await writeFileDb(db);
  return record;
}

export async function updateNicknamePresetStyle(
  name: string,
  style: NicknamePresetStyle,
  updatedBy?: string
): Promise<NicknamePresetRecord> {
  const key = normalizeNicknamePresetName(name);
  if (!isValidNicknamePresetName(key)) throw new Error("Invalid preset name.");
  if (isBuiltinNicknamePreset(key)) throw new Error("Built-in preset cannot be edited.");

  const normalizedStyle = normalizeNicknamePresetStyle(style);
  const sb = getSupabase();
  if (sb) {
    const existing = await sb.from("kaplao_nickname_presets").select("*").eq("name", key).maybeSingle();
    throwIfSbError(existing.error, "updateNicknamePresetStyle.lookup");
    if (!existing.data) throw new Error(`Preset "${key}" not found.`);

    const { data, error } = await sb
      .from("kaplao_nickname_presets")
      .update({
        style: normalizedStyle,
        created_by: updatedBy?.trim().toLowerCase() || (existing.data as SbNicknamePresetRow).created_by,
      })
      .eq("name", key)
      .select("*")
      .single();
    throwIfSbError(error, "updateNicknamePresetStyle.update");
    return rowToNicknamePreset(data as SbNicknamePresetRow);
  }

  const db = await readFileDb();
  const index = db.nicknamePresets.findIndex((p) => p.name === key);
  if (index < 0) throw new Error(`Preset "${key}" not found.`);
  db.nicknamePresets[index] = {
    ...db.nicknamePresets[index],
    style: normalizedStyle,
  };
  await writeFileDb(db);
  return db.nicknamePresets[index];
}

export async function removeNicknamePreset(name: string): Promise<boolean> {
  const key = normalizeNicknamePresetName(name);
  if (!key) return false;
  if (isBuiltinNicknamePreset(key)) throw new Error("Built-in presets cannot be removed.");

  const sb = getSupabase();
  if (sb) {
    const { error } = await sb.from("kaplao_nickname_presets").delete().eq("name", key);
    throwIfSbError(error, "removeNicknamePreset");
    return true;
  }

  const db = await readFileDb();
  const before = db.nicknamePresets.length;
  db.nicknamePresets = db.nicknamePresets.filter((p) => p.name !== key);
  if (db.nicknamePresets.length === before) return false;
  await writeFileDb(db);
  return true;
}

export async function deletePlayerAccount(email: string): Promise<boolean> {
  const key = email.trim().toLowerCase();
  if (!key) return false;

  const user = await findUserByEmail(key);
  if (!user) return false;

  const sb = getSupabase();
  if (sb) {
    const verificationDelete = await sb.from("kaplao_verification_codes").delete().eq("email", key);
    throwIfSbError(verificationDelete.error, "deletePlayerAccount.verificationCodes");

    const deviceDelete = await sb.from("kaplao_device_sessions").delete().eq("email", key);
    if (!isMissingRelationError(deviceDelete.error)) {
      throwIfSbError(deviceDelete.error, "deletePlayerAccount.deviceSessions");
    }

    const roleDelete = await sb.from("kaplao_player_roles").delete().eq("email", key);
    if (!isMissingRelationError(roleDelete.error)) {
      throwIfSbError(roleDelete.error, "deletePlayerAccount.playerRoles");
    }

    const adminDelete = await sb.from("kaplao_admins").delete().eq("email", key);
    if (!isMissingRelationError(adminDelete.error)) {
      throwIfSbError(adminDelete.error, "deletePlayerAccount.admins");
    }

    const userDelete = await sb.from("kaplao_users").delete().eq("id", user.id);
    throwIfSbError(userDelete.error, "deletePlayerAccount.user");
    return true;
  }

  const db = await readFileDb();
  const before = db.users.length;
  db.users = db.users.filter((u) => u.id !== user.id);
  db.codes = db.codes.filter((c) => c.email.toLowerCase() !== key);
  db.devices = db.devices.filter((d) => (d.email || "").toLowerCase() !== key);
  db.playerRoles = db.playerRoles.filter((r) => r.email.toLowerCase() !== key);
  db.admins = db.admins.filter((a) => a.email.toLowerCase() !== key);
  if (db.users.length === before) return false;
  await writeFileDb(db);
  return true;
}


// ========== Dev Chat Messages ==========

type SbDevChatMessageRow = {
  id: string;
  sender_email: string;
  sender_nickname: string;
  message: string;
  is_from_admin: boolean;
  admin_email: string | null;
  admin_nickname: string | null;
  created_at: string;
  read_by_admin: boolean;
  read_by_player: boolean;
};

function rowToDevChatMessage(row: SbDevChatMessageRow): DevChatMessage {
  return {
    id: row.id,
    senderEmail: row.sender_email,
    senderNickname: row.sender_nickname,
    message: row.message,
    isFromAdmin: row.is_from_admin,
    adminEmail: row.admin_email || undefined,
    adminNickname: row.admin_nickname || undefined,
    createdAt: row.created_at,
    readByAdmin: row.read_by_admin,
    readByPlayer: row.read_by_player,
  };
}

export async function createDevChatMessage(
  senderEmail: string,
  senderNickname: string,
  message: string,
  isFromAdmin: boolean,
  adminEmail?: string,
  adminNickname?: string
): Promise<DevChatMessage | null> {
  const key = senderEmail.trim().toLowerCase();
  if (!key || !message.trim()) return null;

  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_dev_messages")
      .insert({
        sender_email: key,
        sender_nickname: senderNickname,
        message: message.trim(),
        is_from_admin: isFromAdmin,
        admin_email: adminEmail?.trim().toLowerCase() || null,
        admin_nickname: adminNickname || null,
        read_by_admin: isFromAdmin, // if from admin, it's already "read" by admin
        read_by_player: !isFromAdmin, // if from player, it's already "read" by player
      })
      .select()
      .single();
    throwIfSbError(error, "createDevChatMessage");
    return data ? rowToDevChatMessage(data as SbDevChatMessageRow) : null;
  }

  // File-based fallback
  const db = await readFileDb();
  const newMessage: DevChatMessage = {
    id: `msg_${Date.now()}_${Math.random().toString(36).slice(2, 9)}`,
    senderEmail: key,
    senderNickname,
    message: message.trim(),
    isFromAdmin,
    adminEmail: adminEmail?.trim().toLowerCase(),
    adminNickname,
    createdAt: new Date().toISOString(),
    readByAdmin: isFromAdmin,
    readByPlayer: !isFromAdmin,
  };
  db.devMessages.push(newMessage);
  await writeFileDb(db);
  return newMessage;
}

export async function getDevChatMessages(playerEmail: string): Promise<DevChatMessage[]> {
  const key = playerEmail.trim().toLowerCase();
  if (!key) return [];

  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_dev_messages")
      .select("*")
      .eq("sender_email", key)
      .order("created_at", { ascending: true });
    if (isMissingRelationError(error)) return [];
    throwIfSbError(error, "getDevChatMessages");
    return (data || []).map((row) => rowToDevChatMessage(row as SbDevChatMessageRow));
  }

  const db = await readFileDb();
  return db.devMessages
    .filter((m) => m.senderEmail.toLowerCase() === key)
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

export async function getDevChatConversations(): Promise<DevChatConversation[]> {
  const sb = getSupabase();
  if (sb) {
    // Get all messages grouped by sender_email, with latest message info
    const { data, error } = await sb
      .from("kaplao_dev_messages")
      .select("*")
      .order("created_at", { ascending: false });
    if (isMissingRelationError(error)) return [];
    throwIfSbError(error, "getDevChatConversations");

    const messages = (data || []).map((row) => rowToDevChatMessage(row as SbDevChatMessageRow));
    
    // Group by sender email
    const conversationMap = new Map<string, {
      playerEmail: string;
      playerNickname: string;
      lastMessage: string;
      lastMessageAt: string;
      unreadCount: number;
      isLastFromAdmin: boolean;
    }>();

    for (const msg of messages) {
      const key = msg.senderEmail.toLowerCase();
      if (!conversationMap.has(key)) {
        conversationMap.set(key, {
          playerEmail: msg.senderEmail,
          playerNickname: msg.senderNickname,
          lastMessage: msg.message,
          lastMessageAt: msg.createdAt,
          unreadCount: 0,
          isLastFromAdmin: msg.isFromAdmin,
        });
      }
      // Count unread messages from player
      const conv = conversationMap.get(key)!;
      if (!msg.isFromAdmin && !msg.readByAdmin) {
        conv.unreadCount++;
      }
    }

    return Array.from(conversationMap.values()).sort(
      (a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime()
    );
  }

  // File-based fallback
  const db = await readFileDb();
  const conversationMap = new Map<string, DevChatConversation>();

  const sorted = [...db.devMessages].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  );

  for (const msg of sorted) {
    const key = msg.senderEmail.toLowerCase();
    if (!conversationMap.has(key)) {
      conversationMap.set(key, {
        playerEmail: msg.senderEmail,
        playerNickname: msg.senderNickname,
        lastMessage: msg.message,
        lastMessageAt: msg.createdAt,
        unreadCount: 0,
        isLastFromAdmin: msg.isFromAdmin,
      });
    }
    const conv = conversationMap.get(key)!;
    if (!msg.isFromAdmin && !msg.readByAdmin) {
      conv.unreadCount++;
    }
  }

  return Array.from(conversationMap.values());
}

export async function markDevChatMessagesAsRead(
  playerEmail: string,
  readByAdmin: boolean
): Promise<boolean> {
  const key = playerEmail.trim().toLowerCase();
  if (!key) return false;

  const sb = getSupabase();
  if (sb) {
    const { error } = await sb
      .from("kaplao_dev_messages")
      .update(readByAdmin ? { read_by_admin: true } : { read_by_player: true })
      .eq("sender_email", key)
      .eq(readByAdmin ? "is_from_admin" : "is_from_admin", readByAdmin ? false : true);
    if (isMissingRelationError(error)) return false;
    throwIfSbError(error, "markDevChatMessagesAsRead");
    return true;
  }

  // File-based fallback
  const db = await readFileDb();
  let updated = false;
  for (const msg of db.devMessages) {
    if (msg.senderEmail.toLowerCase() === key) {
      if (readByAdmin && !msg.isFromAdmin && !msg.readByAdmin) {
        msg.readByAdmin = true;
        updated = true;
      } else if (!readByAdmin && msg.isFromAdmin && !msg.readByPlayer) {
        msg.readByPlayer = true;
        updated = true;
      }
    }
  }
  if (updated) {
    await writeFileDb(db);
  }
  return updated;
}
