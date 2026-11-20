import { randomUUID } from "crypto";
import { getSupabase, isSupabaseConfigured } from "./supabase";
import {
  DEFAULT_LOG_SETTINGS,
  LogAutoDeletePolicy,
  PlayerLogAction,
  PlayerLogRecord,
  PlayerLogSettings,
  autoDeleteCutoffIso,
} from "./player-log-types";

type FileFs = typeof import("fs/promises");
type FilePath = typeof import("path");

type LogsFileShape = {
  logs: PlayerLogRecord[];
  settings: PlayerLogSettings;
};

let memoryLogs: LogsFileShape | null = null;

function newId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return randomUUID();
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

function logsFilePath(pathMod: FilePath) {
  const dir = process.env.DATA_DIR || pathMod.join(process.cwd(), "data");
  return { dir, file: pathMod.join(dir, "player-logs.json") };
}

async function readFileLogs(): Promise<LogsFileShape> {
  if (memoryLogs) return memoryLogs;
  const loaded = await loadFs();
  if (!loaded) {
    memoryLogs = { logs: [], settings: { ...DEFAULT_LOG_SETTINGS } };
    return memoryLogs;
  }

  const { fs, path } = loaded;
  const { dir, file } = logsFilePath(path);
  try {
    await fs.mkdir(dir, { recursive: true });
    const raw = await fs.readFile(file, "utf8");
    const parsed = JSON.parse(raw) as LogsFileShape;
    memoryLogs = {
      logs: Array.isArray(parsed.logs) ? parsed.logs : [],
      settings: parsed.settings || { ...DEFAULT_LOG_SETTINGS },
    };
    return memoryLogs;
  } catch {
    memoryLogs = { logs: [], settings: { ...DEFAULT_LOG_SETTINGS } };
    return memoryLogs;
  }
}

async function writeFileLogs(data: LogsFileShape): Promise<void> {
  memoryLogs = data;
  const loaded = await loadFs();
  if (!loaded) return;
  const { fs, path } = loaded;
  const { dir, file } = logsFilePath(path);
  await fs.mkdir(dir, { recursive: true });
  await fs.writeFile(file, JSON.stringify(data, null, 2), "utf8");
}

function rowToLog(row: {
  id: string;
  action: string;
  created_at: string;
  nickname: string;
  email: string;
  level_title?: string | null;
  actor_nickname?: string | null;
  actor_email?: string | null;
  metadata?: Record<string, unknown> | null;
}): PlayerLogRecord {
  return {
    id: row.id,
    action: row.action as PlayerLogAction,
    createdAt: row.created_at,
    nickname: row.nickname || "",
    email: row.email || "",
    levelTitle: row.level_title || undefined,
    actorNickname: row.actor_nickname || undefined,
    actorEmail: row.actor_email || undefined,
    metadata: row.metadata || undefined,
  };
}

function supabaseClient() {
  return isSupabaseConfigured() ? getSupabase() : null;
}

export async function getPlayerLogSettings(): Promise<PlayerLogSettings> {
  const sb = supabaseClient();
  if (sb) {
    const result = await sb
      .from("kaplao_player_log_settings")
      .select("auto_delete,updated_at,updated_by")
      .eq("id", "default")
      .maybeSingle();

    if (result.error && !isMissingRelationError(result.error)) {
      throw new Error(result.error.message);
    }

    if (result.data) {
      return {
        autoDelete: (result.data.auto_delete as LogAutoDeletePolicy) || "none",
        updatedAt: result.data.updated_at,
        updatedBy: result.data.updated_by || undefined,
      };
    }
  }

  const file = await readFileLogs();
  return file.settings;
}

export async function setPlayerLogSettings(
  autoDelete: LogAutoDeletePolicy,
  updatedBy?: string
): Promise<PlayerLogSettings> {
  const settings: PlayerLogSettings = {
    autoDelete,
    updatedAt: new Date().toISOString(),
    updatedBy,
  };

  const sb = supabaseClient();
  if (sb) {
    const result = await sb.from("kaplao_player_log_settings").upsert({
      id: "default",
      auto_delete: autoDelete,
      updated_at: settings.updatedAt,
      updated_by: updatedBy || null,
    });
    if (result.error && !isMissingRelationError(result.error)) {
      throw new Error(result.error.message);
    }
  } else {
    const file = await readFileLogs();
    file.settings = settings;
    await writeFileLogs(file);
  }

  await purgeExpiredPlayerLogs();
  return settings;
}

export async function appendPlayerLog(input: {
  action: PlayerLogAction;
  nickname?: string;
  email?: string;
  levelTitle?: string;
  actorNickname?: string;
  actorEmail?: string;
  metadata?: Record<string, unknown>;
}): Promise<PlayerLogRecord | null> {
  const record: PlayerLogRecord = {
    id: newId(),
    action: input.action,
    createdAt: new Date().toISOString(),
    nickname: (input.nickname || "Player").trim(),
    email: (input.email || "").trim().toLowerCase(),
    levelTitle: input.levelTitle?.trim() || undefined,
    actorNickname: input.actorNickname?.trim() || undefined,
    actorEmail: input.actorEmail?.trim().toLowerCase() || undefined,
    metadata: input.metadata,
  };

  const sb = supabaseClient();
  if (sb) {
    const result = await sb.from("kaplao_player_logs").insert({
      id: record.id,
      action: record.action,
      created_at: record.createdAt,
      nickname: record.nickname,
      email: record.email,
      level_title: record.levelTitle || null,
      actor_nickname: record.actorNickname || null,
      actor_email: record.actorEmail || null,
      metadata: record.metadata || null,
    });

    if (result.error) {
      console.error("[player-logs] insert", result.error);
      const file = await readFileLogs();
      file.logs.unshift(record);
      await writeFileLogs(file);
    }
  } else {
    const file = await readFileLogs();
    file.logs.unshift(record);
    await writeFileLogs(file);
  }

  await purgeExpiredPlayerLogs();
  return record;
}

export async function listPlayerLogs(limit = 500): Promise<PlayerLogRecord[]> {
  await purgeExpiredPlayerLogs();

  const cap = Math.min(Math.max(limit, 1), 2000);
  const file = await readFileLogs();
  const merged = new Map<string, PlayerLogRecord>();
  for (const log of file.logs) merged.set(log.id, log);

  const sb = supabaseClient();
  if (sb) {
    const result = await sb
      .from("kaplao_player_logs")
      .select("*")
      .order("created_at", { ascending: false })
      .limit(cap);

    if (result.error) {
      if (!isMissingRelationError(result.error)) {
        console.error("[player-logs] list", result.error);
      }
    } else {
      for (const row of result.data || []) {
        const log = rowToLog(row);
        merged.set(log.id, log);
      }
    }
  }

  return Array.from(merged.values())
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .slice(0, cap);
}

export async function deletePlayerLog(id: string): Promise<boolean> {
  if (!id) return false;

  const sb = supabaseClient();
  if (sb) {
    const result = await sb.from("kaplao_player_logs").delete().eq("id", id);
    if (result.error && !isMissingRelationError(result.error)) {
      throw new Error(result.error.message);
    }
  }

  const file = await readFileLogs();
  const before = file.logs.length;
  file.logs = file.logs.filter((log) => log.id !== id);
  if (file.logs.length !== before) {
    await writeFileLogs(file);
    return true;
  }

  return isSupabaseConfigured();
}

export async function purgeExpiredPlayerLogs(): Promise<number> {
  const settings = await getPlayerLogSettings();
  const cutoff = autoDeleteCutoffIso(settings.autoDelete);
  if (!cutoff) return 0;

  const sb = supabaseClient();
  if (sb) {
    const result = await sb.from("kaplao_player_logs").delete().lt("created_at", cutoff);
    if (result.error && !isMissingRelationError(result.error)) {
      console.error("[player-logs] purge", result.error);
    }
  }

  const file = await readFileLogs();
  const before = file.logs.length;
  file.logs = file.logs.filter((log) => log.createdAt >= cutoff);
  if (file.logs.length !== before) {
    await writeFileLogs(file);
    return before - file.logs.length;
  }

  return 0;
}
