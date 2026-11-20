import { SignJWT, jwtVerify } from "jose";
import { getPlayerRole, isAdminEmail } from "./store";
import {
  DEFAULT_NICKNAME_PRESET,
  DEFAULT_PLAYER_PERMISSION,
  DEFAULT_PLAYER_RIGHTS,
  deriveLegacyPermission,
  normalizePlayerRights,
  rightsCanDeletePublicLevels,
  rightsFromLegacyPermission,
  rightsGrantGameAdmin,
  rightsGrantWebConsole,
  type PlayerPermission,
  type PlayerRight,
} from "./player-roles";

const NICK_MAX = 20;

/** Root owner — always has every right and cannot be locked out of Web Console. */
export const SUPER_ADMIN_EMAIL = "asmoloff55@gmail.com";

export const ALL_PLAYER_RIGHTS: PlayerRight[] = ["level_purge", "game_host", "web_console"];

export function sanitizeNickname(nickname: string): string {
  const trimmed = (nickname || "").trim();
  if (!trimmed) return "";
  return trimmed.length > NICK_MAX ? trimmed.slice(0, NICK_MAX) : trimmed;
}

export function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}

async function bcrypt() {
  const mod = await import("bcryptjs");
  return mod.default ?? mod;
}

export async function hashPassword(password: string): Promise<string> {
  const b = await bcrypt();
  return b.hash(password, 10);
}

export async function verifyPassword(password: string, hash: string): Promise<boolean> {
  const b = await bcrypt();
  return b.compare(password, hash);
}

function jwtSecret() {
  const secret = process.env.JWT_SECRET || "dev-only-kaplao-secret-change-me";
  return new TextEncoder().encode(secret);
}

export async function signAuthToken(payload: {
  sub: string;
  nickname: string;
  email: string;
}): Promise<string> {
  return new SignJWT({ nickname: payload.nickname, email: payload.email })
    .setProtectedHeader({ alg: "HS256" })
    .setSubject(payload.sub)
    .setIssuedAt()
    .setExpirationTime("30d")
    .sign(jwtSecret());
}

export async function verifyAuthToken(token: string) {
  const { payload } = await jwtVerify(token, jwtSecret());
  return payload;
}

export type AuthSession = {
  userId: string;
  email: string;
  nickname: string;
};

export async function sessionFromAuthToken(token: string): Promise<AuthSession | null> {
  try {
    const payload = await verifyAuthToken(token);
    const email = String(payload.email || "")
      .trim()
      .toLowerCase();
    const nickname = sanitizeNickname(String(payload.nickname || ""));
    const userId = String(payload.sub || "");
    if (!email || !nickname || !userId) return null;
    return { userId, email, nickname };
  } catch {
    return null;
  }
}

export function bearerTokenFromRequest(request: Request): string | null {
  const header = request.headers.get("authorization") || "";
  const match = header.match(/^Bearer\s+(.+)$/i);
  return match ? match[1].trim() : null;
}

export async function authSessionFromRequest(request: Request): Promise<AuthSession | null> {
  const token = bearerTokenFromRequest(request);
  if (!token) return null;
  return sessionFromAuthToken(token);
}

export function isSuperAdminEmail(email: string): boolean {
  return email.trim().toLowerCase() === SUPER_ADMIN_EMAIL.toLowerCase();
}

export async function canAccessWebConsole(email: string): Promise<boolean> {
  const normalized = email.trim().toLowerCase();
  if (isSuperAdminEmail(normalized)) return true;
  try {
    const role = await getPlayerRole(normalized);
    return rightsGrantWebConsole(role?.rights ?? []);
  } catch {
    return false;
  }
}

export async function requireWebConsoleSession(request: Request): Promise<AuthSession | null> {
  const session = await authSessionFromRequest(request);
  if (!session) return null;
  if (!(await canAccessWebConsole(session.email))) return null;
  return session;
}

export async function resolveAdminFlags(email: string) {
  const role = await resolvePlayerRole(email);
  return {
    isAdmin: role.isAdmin,
    isSuperAdmin: role.isSuperAdmin,
    permission: role.permission,
    rights: role.rights,
    nicknamePreset: role.nicknamePreset,
  };
}

export type ResolvedPlayerRole = {
  permission: PlayerPermission;
  rights: PlayerRight[];
  nicknamePreset: string;
  isAdmin: boolean;
  canDeleteLevels: boolean;
  isLevelMod: boolean;
  isSuperAdmin: boolean;
};

export async function resolvePlayerRole(email: string): Promise<ResolvedPlayerRole> {
  const normalized = email.trim().toLowerCase();
  const isRoot = isSuperAdminEmail(normalized);

  if (isRoot) {
    const stored = await getPlayerRole(normalized);
    return {
      permission: "admin",
      rights: [...ALL_PLAYER_RIGHTS],
      nicknamePreset: stored?.nicknamePreset ?? DEFAULT_NICKNAME_PRESET,
      isAdmin: true,
      canDeleteLevels: true,
      isLevelMod: true,
      isSuperAdmin: true,
    };
  }

  const stored = await getPlayerRole(normalized);
  const legacyAdmin = await isAdminEmail(normalized);

  if (stored) {
    let rights = normalizePlayerRights(stored.rights);
    if (rights.length === 0 && stored.permission !== "none") {
      rights = rightsFromLegacyPermission(stored.permission);
    }
    if (rights.length === 0 && legacyAdmin) {
      rights = rightsFromLegacyPermission("admin");
    }
    return {
      rights,
      permission: deriveLegacyPermission(rights),
      nicknamePreset: stored.nicknamePreset,
      isAdmin: rightsGrantGameAdmin(rights),
      canDeleteLevels: rightsCanDeletePublicLevels(rights),
      isLevelMod: rightsCanDeletePublicLevels(rights) && !rightsGrantGameAdmin(rights),
      isSuperAdmin: rightsGrantWebConsole(rights),
    };
  }

  if (legacyAdmin) {
    const rights = rightsFromLegacyPermission("admin");
    return {
      permission: "admin",
      rights,
      nicknamePreset: DEFAULT_NICKNAME_PRESET,
      isAdmin: true,
      canDeleteLevels: true,
      isLevelMod: false,
      isSuperAdmin: false,
    };
  }

  return {
    permission: DEFAULT_PLAYER_PERMISSION,
    rights: DEFAULT_PLAYER_RIGHTS,
    nicknamePreset: DEFAULT_NICKNAME_PRESET,
    isAdmin: false,
    canDeleteLevels: false,
    isLevelMod: false,
    isSuperAdmin: false,
  };
}

export function gameServerSecret(): string {
  return process.env.GAME_SERVER_SECRET || process.env.JWT_SECRET || "dev-only-kaplao-secret-change-me";
}

export function verifyGameServerSecret(request: Request): boolean {
  const header = request.headers.get("x-kaplao-game-secret") || "";
  const secret = gameServerSecret();
  return header.length > 0 && header === secret;
}

export function randomCode(length = 6): string {
  let out = "";
  for (let i = 0; i < length; i++) out += Math.floor(Math.random() * 10).toString();
  return out;
}

export function randomDeviceCode(): string {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let out = "";
  for (let i = 0; i < 8; i++) out += alphabet[Math.floor(Math.random() * alphabet.length)];
  return out;
}
