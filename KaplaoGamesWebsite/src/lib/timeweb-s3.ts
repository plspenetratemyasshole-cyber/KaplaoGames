import { GetObjectCommand, PutObjectCommand, DeleteObjectCommand, S3Client } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";

let client: S3Client | null | undefined;

export function isTimewebS3Configured(): boolean {
  return Boolean(
    (process.env.TIMEWEB_S3_ACCESS_KEY || "").trim() &&
      (process.env.TIMEWEB_S3_SECRET_KEY || "").trim() &&
      (process.env.TIMEWEB_S3_BUCKET || "").trim()
  );
}

export function timewebS3Bucket(): string {
  return (process.env.TIMEWEB_S3_BUCKET || "").trim();
}

export function getTimewebS3Client(): S3Client {
  if (client !== undefined) return client!;

  const accessKeyId = (process.env.TIMEWEB_S3_ACCESS_KEY || "").trim();
  const secretAccessKey = (process.env.TIMEWEB_S3_SECRET_KEY || "").trim();
  if (!accessKeyId || !secretAccessKey) {
    throw new Error("Timeweb S3 credentials are not configured.");
  }

  client = new S3Client({
    region: (process.env.TIMEWEB_S3_REGION || "ru-1").trim(),
    endpoint: (process.env.TIMEWEB_S3_ENDPOINT || "https://s3.twcstorage.ru").trim(),
    credentials: { accessKeyId, secretAccessKey },
    forcePathStyle: true,
  });

  return client;
}

export async function createTimewebUploadUrl(objectKey: string, expiresSeconds = 3600): Promise<string> {
  const s3 = getTimewebS3Client();
  const command = new PutObjectCommand({
    Bucket: timewebS3Bucket(),
    Key: objectKey,
    ContentType: "application/zip",
  });
  return getSignedUrl(s3, command, { expiresIn: expiresSeconds });
}

export async function createTimewebDownloadUrl(objectKey: string, expiresSeconds = 3600): Promise<string> {
  const s3 = getTimewebS3Client();
  const command = new GetObjectCommand({
    Bucket: timewebS3Bucket(),
    Key: objectKey,
  });
  return getSignedUrl(s3, command, { expiresIn: expiresSeconds });
}

export async function deleteTimewebObject(objectKey: string): Promise<void> {
  if (!isTimewebS3Configured()) {
    throw new Error("Timeweb S3 is not configured.");
  }

  const s3 = getTimewebS3Client();
  await s3.send(
    new DeleteObjectCommand({
      Bucket: timewebS3Bucket(),
      Key: objectKey,
    })
  );
}

export function sanitizeObjectKey(raw: string): string | null {
  const key = (raw || "").trim().replace(/\\/g, "/").replace(/^\/+/, "");
  if (!key || key.includes("..")) return null;
  if (!/^[a-zA-Z0-9._/-]+$/.test(key)) return null;
  return key;
}

export function newLevelObjectKey(): string {
  const id =
    typeof crypto !== "undefined" && typeof crypto.randomUUID === "function"
      ? crypto.randomUUID().replace(/-/g, "")
      : `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`;
  return `${id}.zip`;
}
