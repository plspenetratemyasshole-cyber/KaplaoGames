import { S3Client, PutObjectCommand } from "npm:@aws-sdk/client-s3@3.709.0";
import { getSignedUrl } from "npm:@aws-sdk/s3-request-presigner@3.709.0";

const corsHeaders: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type, x-kaplao-game-secret",
};

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...corsHeaders, "Content-Type": "application/json" },
  });
}

function gameServerSecret(): string {
  return (Deno.env.get("GAME_SERVER_SECRET") || Deno.env.get("JWT_SECRET") || "").trim();
}

function verifyGameServerSecret(req: Request): boolean {
  const header = (req.headers.get("x-kaplao-game-secret") || "").trim();
  const secret = gameServerSecret();
  return header.length > 0 && header === secret;
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

function newLevelObjectKey(): string {
  return `${crypto.randomUUID().replace(/-/g, "")}.zip`;
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { headers: corsHeaders });
  }

  if (req.method !== "POST") {
    return json({ error: "Method not allowed." }, 405);
  }

  if (!verifyGameServerSecret(req)) {
    return json({ error: "Forbidden." }, 403);
  }

  if (!isTimewebConfigured()) {
    return json({ error: "Timeweb S3 is not configured in Supabase secrets." }, 503);
  }

  try {
    const body = await req.json().catch(() => ({}));
    const requestedKey = String(body?.objectKey || "").trim();
    const objectKey = requestedKey ? sanitizeObjectKey(requestedKey) : newLevelObjectKey();
    if (!objectKey) {
      return json({ error: "Invalid object key." }, 400);
    }

    const bucket = (Deno.env.get("TIMEWEB_S3_BUCKET") || "").trim();
    const uploadUrl = await getSignedUrl(
      getS3Client(),
      new PutObjectCommand({
        Bucket: bucket,
        Key: objectKey,
        ContentType: "application/zip",
      }),
      { expiresIn: 3600 }
    );

    return json({
      ok: true,
      storage_path: `s3:${objectKey}`,
      upload_url: uploadUrl,
      object_key: objectKey,
    });
  } catch (error) {
    console.error("[levels-upload-url]", error);
    const message = error instanceof Error ? error.message : "Failed to create upload URL.";
    return json({ error: message }, 500);
  }
});
