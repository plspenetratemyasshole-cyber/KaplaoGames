import { resolvePlayerRole } from "./auth";
import { appendPlayerLog } from "./player-logs";
import { getLevelsSupabase, isLevelsSupabaseConfigured } from "./levels-supabase";
import { canActorDeletePublicLevel } from "./level-ownership";
import { parseLevelStoragePath } from "./level-storage";
import { deleteTimewebObject, isTimewebS3Configured } from "./timeweb-s3";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function normalizeTitle(value: string): string {
  return (value || "").trim().replace(/\s+/g, " ").toLowerCase();
}

export class DeletePublicLevelError extends Error {
  status: number;

  constructor(message: string, status = 400) {
    super(message);
    this.status = status;
  }
}

export async function deletePublicLevel(params: {
  email: string;
  nickname: string;
  levelId: string;
  confirmTitle: string;
}): Promise<{ deletedId: string }> {
  const levelId = (params.levelId || "").trim();
  const confirmTitle = (params.confirmTitle || "").trim();

  if (!UUID_RE.test(levelId)) {
    throw new DeletePublicLevelError("Invalid level id.", 400);
  }

  if (!confirmTitle || confirmTitle.length > 120) {
    throw new DeletePublicLevelError("Confirm the exact level title.", 400);
  }

  const role = await resolvePlayerRole(params.email);

  if (!isLevelsSupabaseConfigured()) {
    throw new DeletePublicLevelError("Level catalog is not configured on the website.", 503);
  }

  const levelsSb = getLevelsSupabase();
  if (!levelsSb) {
    throw new DeletePublicLevelError("Level catalog is not configured on the website.", 503);
  }

  const rowResult = await levelsSb
    .from("levels")
    .select("id,title,storage_path,author_email,author_nick")
    .eq("id", levelId)
    .maybeSingle();

  if (rowResult.error) {
    console.error("[delete-public-level] select", rowResult.error);
    throw new DeletePublicLevelError("Failed to load level.", 500);
  }

  const row = rowResult.data as {
    id?: string;
    title?: string;
    storage_path?: string;
    author_email?: string;
    author_nick?: string;
  } | null;
  if (!row?.id) {
    throw new DeletePublicLevelError("Level not found.", 404);
  }

  if (normalizeTitle(String(row.title || "")) !== normalizeTitle(confirmTitle)) {
    throw new DeletePublicLevelError("Level title confirmation does not match.", 400);
  }

  if (!canActorDeletePublicLevel({
    rights: role.rights,
    permission: role.permission,
    actorEmail: params.email,
    actorNickname: params.nickname,
    authorEmail: row.author_email,
    authorNick: row.author_nick,
  })) {
    throw new DeletePublicLevelError("You can only delete your own levels.", 403);
  }

  const deleteResult = await levelsSb.from("levels").delete().eq("id", levelId);
  if (deleteResult.error) {
    console.error("[delete-public-level] delete row", deleteResult.error);
    throw new DeletePublicLevelError("Failed to delete level metadata.", 500);
  }

  const parsed = parseLevelStoragePath(String(row.storage_path || ""));
  if (parsed.kind === "s3" && isTimewebS3Configured()) {
    try {
      await deleteTimewebObject(parsed.value);
    } catch (storageError) {
      console.error("[delete-public-level] s3 delete failed", storageError);
    }
  }

  console.log(
    `[delete-public-level] deleted levelId=${levelId} title="${row.title}" by ${params.email} (${role.permission})`
  );

  void appendPlayerLog({
    action: "level_deleted",
    nickname: params.nickname,
    email: params.email,
    levelTitle: row.title,
    actorNickname: params.nickname,
    actorEmail: params.email,
    metadata: {
      source: "levels-delete",
      authorEmail: row.author_email || "",
      authorNickname: row.author_nick || "",
      levelId,
    },
  }).catch((error) => console.error("[delete-public-level] log", error));

  return { deletedId: levelId };
}
