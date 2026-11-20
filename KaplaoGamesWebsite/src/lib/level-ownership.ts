import { rightsCanDeletePublicLevels, type PlayerRight } from "./player-roles";

function normalizeEmail(value: string): string {
  return (value || "").trim().toLowerCase();
}

function normalizeNickname(value: string): string {
  return (value || "").trim();
}

export function canActorDeletePublicLevel(params: {
  rights?: PlayerRight[];
  /** @deprecated Prefer rights. */
  permission?: string;
  actorEmail: string;
  actorNickname: string;
  authorEmail?: string | null;
  authorNick?: string | null;
}): boolean {
  if (rightsCanDeletePublicLevels(params.rights ?? [])) return true;

  // Legacy permission fallback while clients/DB migrate.
  const permission = (params.permission || "").trim().toLowerCase();
  if (permission === "admin" || permission === "level_mod") return true;

  const actorEmail = normalizeEmail(params.actorEmail);
  const authorEmail = normalizeEmail(params.authorEmail || "");
  if (actorEmail && authorEmail && actorEmail === authorEmail) return true;

  const authorNick = normalizeNickname(params.authorNick || "");
  const actorNick = normalizeNickname(params.actorNickname);
  if (
    !authorEmail &&
    authorNick &&
    actorNick &&
    authorNick.localeCompare(actorNick, undefined, { sensitivity: "accent" }) === 0
  ) {
    return true;
  }

  return false;
}
