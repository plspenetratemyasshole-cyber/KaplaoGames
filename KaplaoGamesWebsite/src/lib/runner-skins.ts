/** Stable runner skin ids — must match Unity RunnerSkinCatalog. */
export const PUBLIC_SKIN_COUNT = 6;

export const ADMIN_SKIN_IDS = [
  "admin_dude_monster",
  "admin_owlet_monster",
  "admin_pink_monster",
] as const;

export type AdminSkinId = (typeof ADMIN_SKIN_IDS)[number];

const ADMIN_SKIN_ID_SET = new Set<string>(ADMIN_SKIN_IDS);

export function isAdminSkinId(value: string): value is AdminSkinId {
  return ADMIN_SKIN_ID_SET.has(value.trim().toLowerCase());
}

export function normalizeAdminSkinIds(values: unknown): AdminSkinId[] {
  if (!Array.isArray(values)) return [];
  const seen = new Set<AdminSkinId>();
  for (const raw of values) {
    const key = String(raw || "")
      .trim()
      .toLowerCase();
    if (!isAdminSkinId(key)) continue;
    seen.add(key);
  }
  return ADMIN_SKIN_IDS.filter((id) => seen.has(id));
}

export function adminSkinIdToIndex(skinId: string): number | null {
  const key = skinId.trim().toLowerCase();
  const index = ADMIN_SKIN_IDS.indexOf(key as AdminSkinId);
  if (index < 0) return null;
  return PUBLIC_SKIN_COUNT + index;
}

export const ADMIN_SKIN_LABELS: Record<AdminSkinId, string> = {
  admin_dude_monster: "Dude Monster",
  admin_owlet_monster: "Owlet Monster",
  admin_pink_monster: "Pink Monster",
};
