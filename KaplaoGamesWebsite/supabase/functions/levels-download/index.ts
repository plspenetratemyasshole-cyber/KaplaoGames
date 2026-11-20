import { S3Client, GetObjectCommand } from "npm:@aws-sdk/client-s3@3.709.0";
import { getSignedUrl } from "npm:@aws-sdk/s3-request-presigner@3.709.0";

const corsHeaders: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
};

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...corsHeaders, "Content-Type": "application/json" },
  });
}

function isTimewebConfigured(): boolean {
  return Boolean(
    (Deno.env.get("TIMEWEB_S3_ACCESS_KEY") || "").trim() &&
      (Deno.env.get("TIMEWEB_S3_SECRET_KEY") || "").trim() &&
      (Deno.env.get("TIMEWEB_S3_BUCKET") || "").trim()
  );
}

function getS3Client(): S3Client {
  return new S3Client({
    region: (Deno.env.get("TIMEWEB_S3_REGION") || "ru-1").trim(),
    endpoint: (Deno.env.get("TIMEWEB_S3_ENDPOINT") || "https://s3.twcstorage.ru").trim(),
    credentials: {
      accessKeyId: (Deno.env.get("TIMEWEB_S3_ACCESS_KEY") || "").trim(),
      secretAccessKey: (Deno.env.get("TIMEWEB_S3_SECRET_KEY") || "").trim(),
    },
    forcePathStyle: true,
  });
}

function sanitizeObjectKey(raw: string): string | null {
  const key = (raw || "").trim().replace(/\\/g, "/").replace(/^\/+/, "");
  if (!key || key.includes("..")) return null;
  if (!/^[a-zA-Z0-9._/-]+$/.test(key)) return null;
  return key;
}

function parseStoragePath(storagePath: string): { kind: "s3" | "legacy_pixeldrain" | "unknown"; value: string } {
  const path = (storagePath || "").trim();
  if (!path) return { kind: "unknown", value: "" };

  const lower = path.toLowerCase();
  if (lower.startsWith("s3:")) {
    const value = sanitizeObjectKey(path.slice(3));
    return value ? { kind: "s3", value } : { kind: "unknown", value: "" };
  }

  if (lower.startsWith("pixeldrain:")) {
    return { kind: "legacy_pixeldrain", value: path.slice("pixeldrain:".length).trim() };
  }

  const plain = sanitizeObjectKey(path);
  if (plain && plain.endsWith(".zip")) {
    return { kind: "s3", value: plain };
  }

  return { kind: "unknown", value: path };
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { headers: corsHeaders });
  }

  if (req.method !== "GET") {
    return json({ error: "Method not allowed." }, 405);
  }

  if (!isTimewebConfigured()) {
    return json({ error: "Timeweb S3 is not configured in Supabase secrets." }, 503);
  }

  try {
    const url = new URL(req.url);
    const storagePath = String(url.searchParams.get("path") || "").trim();
    const parsed = parseStoragePath(storagePath);

    if (parsed.kind === "legacy_pixeldrain") {
      return json(
        {
          error: "This level uses legacy Pixeldrain storage.",
          hint: "Re-publish the level from the game to store it on Timeweb S3.",
        },
        410
      );
    }

    if (parsed.kind !== "s3") {
      return json({ error: "Invalid storage path." }, 400);
    }

    const bucket = (Deno.env.get("TIMEWEB_S3_BUCKET") || "").trim();
    const signedUrl = await getSignedUrl(
      getS3Client(),
      new GetObjectCommand({
        Bucket: bucket,
        Key: parsed.value,
      }),
      { expiresIn: 3600 }
    );

    // JSON (not redirect): UnityWebRequest sends auth headers on redirect and breaks S3 signatures.
    return json({ ok: true, download_url: signedUrl });
  } catch (error) {
    console.error("[levels-download]", error);
    const message = error instanceof Error ? error.message : "Download failed.";
    return json({ error: message }, 500);
  }
});
