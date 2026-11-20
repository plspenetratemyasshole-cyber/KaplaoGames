/** Independent player rights — assign any combination (like skins). */
export const PLAYER_RIGHTS = ["level_purge", "game_host", "web_console"] as const;
export type PlayerRight = (typeof PLAYER_RIGHTS)[number];

export const PLAYER_RIGHT_OPTIONS: {
  value: PlayerRight;
  label: string;
  description: string;
}[] = [
  {
    value: "level_purge",
    label: "Level Purge",
    description: "Delete levels from public servers",
  },
  {
    value: "game_host",
    label: "Game Host",
    description: "Permanent in-game admin powers (like the host)",
  },
  {
    value: "web_console",
    label: "Web Console",
    description: "Access the website admin panel",
  },
];

/** @deprecated Prefer rights[]; kept for DB/API compatibility. */
export const PLAYER_PERMISSIONS = ["admin", "level_mod", "none"] as const;
export type PlayerPermission = (typeof PLAYER_PERMISSIONS)[number];

export const BUILTIN_NICKNAME_PRESETS = ["none"] as const;
export type BuiltinNicknamePreset = (typeof BUILTIN_NICKNAME_PRESETS)[number];

export const DEFAULT_PLAYER_PERMISSION: PlayerPermission = "none";
export const DEFAULT_PLAYER_RIGHTS: PlayerRight[] = [];
export const DEFAULT_NICKNAME_PRESET = "none";

export function normalizePermission(value: string): PlayerPermission | null {
  const key = value.trim().toLowerCase();
  return (PLAYER_PERMISSIONS as readonly string[]).includes(key)
    ? (key as PlayerPermission)
    : null;
}

export function normalizeNicknamePresetName(value: string): string {
  const key = value.trim().toLowerCase().replace(/\s+/g, "_");
  if (!key || key === "own") return "none";
  return key;
}

export function isValidNicknamePresetName(value: string): boolean {
  const key = normalizeNicknamePresetName(value);
  if (!key) return false;
  return /^[a-z0-9_]+$/.test(key);
}

export function isBuiltinNicknamePreset(name: string): boolean {
  const key = normalizeNicknamePresetName(name);
  return (BUILTIN_NICKNAME_PRESETS as readonly string[]).includes(key);
}

export function isPlayerRight(value: string): value is PlayerRight {
  return (PLAYER_RIGHTS as readonly string[]).includes(value.trim().toLowerCase());
}

export function normalizePlayerRights(value: unknown): PlayerRight[] {
  if (!Array.isArray(value)) return [];
  const set = new Set<PlayerRight>();
  for (const item of value) {
    const key = String(item || "")
      .trim()
      .toLowerCase();
    if (isPlayerRight(key)) set.add(key);
  }
  return PLAYER_RIGHTS.filter((right) => set.has(right));
}

export function rightsFromLegacyPermission(permission: PlayerPermission | null | undefined): PlayerRight[] {
  if (permission === "admin") return ["level_purge", "game_host"];
  if (permission === "level_mod") return ["level_purge"];
  return [];
}

export function deriveLegacyPermission(rights: PlayerRight[]): PlayerPermission {
  if (rights.includes("game_host")) return "admin";
  if (rights.includes("level_purge")) return "level_mod";
  return "none";
}

export function roleHasRight(
  rights: PlayerRight[] | null | undefined,
  right: PlayerRight
): boolean {
  return Array.isArray(rights) && rights.includes(right);
}

export function permissionGrantsAdmin(permission: PlayerPermission): boolean {
  return permission === "admin";
}

export function permissionCanDeletePublicLevels(permission: PlayerPermission): boolean {
  return permission === "admin" || permission === "level_mod";
}

export function rightsGrantGameAdmin(rights: PlayerRight[]): boolean {
  return roleHasRight(rights, "game_host");
}

export function rightsCanDeletePublicLevels(rights: PlayerRight[]): boolean {
  return roleHasRight(rights, "level_purge");
}

export function rightsGrantWebConsole(rights: PlayerRight[]): boolean {
  return roleHasRight(rights, "web_console");
}
