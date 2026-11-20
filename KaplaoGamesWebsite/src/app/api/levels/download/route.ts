import { NextResponse } from "next/server";
import { createLevelDownloadSignedUrl, parseLevelStoragePath } from "@/lib/level-storage";
import { isTimewebS3Configured } from "@/lib/timeweb-s3";

export const dynamic = "force-dynamic";
export const revalidate = 0;

export async function GET(request: Request) {
  try {
    if (!isTimewebS3Configured()) {
      return NextResponse.json({ error: "Timeweb S3 is not configured on the website." }, { status: 503 });
    }

    const { searchParams } = new URL(request.url);
    const storagePath = String(searchParams.get("path") || "").trim();
    const parsed = parseLevelStoragePath(storagePath);

    if (parsed.kind === "legacy_pixeldrain") {
      return NextResponse.json(
        {
          error: "This level uses legacy Pixeldrain storage.",
          hint: "Re-publish the level from the game to store it on Timeweb S3.",
        },
        { status: 410 }
      );
    }

    if (parsed.kind !== "s3") {
      return NextResponse.json({ error: "Invalid storage path." }, { status: 400 });
    }

    const signedUrl = await createLevelDownloadSignedUrl(parsed.value);
    return NextResponse.redirect(signedUrl, 302);
  } catch (error) {
    console.error("[levels/download]", error);
    const message = error instanceof Error ? error.message : "Download failed.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
