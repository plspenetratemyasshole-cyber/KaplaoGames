import { createTimewebDownloadUrl, sanitizeObjectKey } from "./timeweb-s3";

export const S3_STORAGE_PREFIX = "s3:";

export function parseLevelStoragePath(storagePath: string): {
  kind: "s3" | "legacy_pixeldrain" | "unknown";
  value: string;
} {
  const path = (storagePath || "").trim();
  if (!path) return { kind: "unknown", value: "" };

  const lower = path.toLowerCase();
  if (lower.startsWith(S3_STORAGE_PREFIX)) {
    const value = sanitizeObjectKey(path.slice(S3_STORAGE_PREFIX.length));
    return value ? { kind: "s3", value } : { kind: "unknown", value: "" };
  }

  if (lower.startsWith("pixeldrain:")) {
    return { kind: "legacy_pixeldrain", value: path.slice("pixeldrain:".length).trim() };
  }

  if (path.startsWith("http://") || path.startsWith("https://")) {
    return { kind: "unknown", value: path };
  }

  const plain = sanitizeObjectKey(path);
  if (plain && plain.endsWith(".zip")) {
    return { kind: "s3", value: plain };
  }

  return { kind: "unknown", value: path };
}

export function toStoragePath(objectKey: string): string {
  return `${S3_STORAGE_PREFIX}${objectKey}`;
}

export async function createLevelDownloadSignedUrl(objectKey: string): Promise<string> {
  return createTimewebDownloadUrl(objectKey, 60 * 60);
}
