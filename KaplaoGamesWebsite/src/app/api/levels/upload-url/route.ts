import { NextResponse } from "next/server";
import { verifyGameServerSecret } from "@/lib/auth";
import { toStoragePath } from "@/lib/level-storage";
import {
  createTimewebUploadUrl,
  isTimewebS3Configured,
  newLevelObjectKey,
  sanitizeObjectKey,
} from "@/lib/timeweb-s3";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    if (!verifyGameServerSecret(request)) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    if (!isTimewebS3Configured()) {
      return NextResponse.json({ error: "Timeweb S3 is not configured on the website." }, { status: 503 });
    }

    const body = await request.json().catch(() => ({}));
    const requestedKey = String(body.objectKey || "").trim();
    const objectKey = requestedKey ? sanitizeObjectKey(requestedKey) : newLevelObjectKey();
    if (!objectKey) {
      return NextResponse.json({ error: "Invalid object key." }, { status: 400 });
    }

    const uploadUrl = await createTimewebUploadUrl(objectKey);
    return NextResponse.json({
      ok: true,
      storage_path: toStoragePath(objectKey),
      upload_url: uploadUrl,
      object_key: objectKey,
    });
  } catch (error) {
    console.error("[levels/upload-url]", error);
    const message = error instanceof Error ? error.message : "Failed to create upload URL.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
