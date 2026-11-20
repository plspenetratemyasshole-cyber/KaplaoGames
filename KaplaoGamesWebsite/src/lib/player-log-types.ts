export type PlayerLogAction =
  | "account_login"
  | "account_logout"
  | "account_created"
  | "account_deleted"
  | "singleplayer_world_enter"
  | "server_join"
  | "level_published"
  | "level_deleted";

export type LogAutoDeletePolicy =
  | "none"
  | "week"
  | "month"
  | "six_months"
  | "year";

export type PlayerLogRecord = {
  id: string;
  action: PlayerLogAction;
  createdAt: string;
  nickname: string;
  email: string;
  levelTitle?: string;
  actorNickname?: string;
  actorEmail?: string;
  metadata?: Record<string, unknown>;
};

export type PlayerLogSettings = {
  autoDelete: LogAutoDeletePolicy;
  updatedAt: string;
  updatedBy?: string;
};

export const DEFAULT_LOG_SETTINGS: PlayerLogSettings = {
  autoDelete: "none",
  updatedAt: new Date(0).toISOString(),
};

export const PLAYER_LOG_ACTIONS: PlayerLogAction[] = [
  "account_login",
  "account_logout",
  "account_created",
  "account_deleted",
  "singleplayer_world_enter",
  "server_join",
  "level_published",
  "level_deleted",
];

export function isPlayerLogAction(value: string): value is PlayerLogAction {
  return (PLAYER_LOG_ACTIONS as string[]).includes(value);
}

export const LOG_AUTO_DELETE_OPTIONS: { value: LogAutoDeletePolicy; label: string }[] = [
  { value: "none", label: "No auto-delete" },
  { value: "week", label: "1 week" },
  { value: "month", label: "1 month" },
  { value: "six_months", label: "6 months" },
  { value: "year", label: "1 year" },
];

export function autoDeleteCutoffIso(policy: LogAutoDeletePolicy, now = Date.now()): string | null {
  if (policy === "none") return null;
  const dayMs = 24 * 60 * 60 * 1000;
  const map: Record<Exclude<LogAutoDeletePolicy, "none">, number> = {
    week: 7 * dayMs,
    month: 30 * dayMs,
    six_months: 183 * dayMs,
    year: 365 * dayMs,
  };
  return new Date(now - map[policy as Exclude<LogAutoDeletePolicy, "none">]).toISOString();
}
