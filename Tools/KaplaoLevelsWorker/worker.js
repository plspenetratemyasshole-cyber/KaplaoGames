/**
 * Kaplao public levels — Cloudflare Worker + R2 (free tier).
 *
 * Free limits (approx): R2 10GB storage, no egress fee; Workers 100k req/day.
 * Upload goes straight to R2 via presigned URL (large zips OK — not limited by Worker body size).
 *
 * Setup:
 * 1. Cloudflare Dashboard → R2 → Create bucket "kaplao-levels"
 * 2. R2 → Manage R2 API Tokens → Create API token (Object Read & Write) for that bucket
 *    Save: Access Key ID, Secret Access Key, and note your Account ID
 * 3. Workers & Pages → Create Worker → paste this file
 * 4. Worker Settings → Variables:
 *      UPLOAD_TOKEN          = long random string (same as Unity cloudflareUploadToken)
 *      R2_ACCOUNT_ID        = your Cloudflare account id
 *      R2_ACCESS_KEY_ID     = from API token
 *      R2_SECRET_ACCESS_KEY = from API token
 *      R2_BUCKET_NAME       = kaplao-levels
 * 5. wrangler.toml (or Dashboard → Settings → Bindings): bind R2 bucket as LEVELS_BUCKET
 *    For Dashboard: Settings → Bindings → R2 Bucket → variable name LEVELS_BUCKET → bucket kaplao-levels
 * 6. Deploy, copy workers.dev URL into Unity PublicLevelCatalogConfig.cloudflareWorkerUrl
 *
 * Endpoints:
 *   POST /upload-url   header X-Upload-Token → { storage_path, upload_url }
 *   GET  /download/:key → streams the zip from R2
 */

const encoder = new TextEncoder();

export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);

      if (request.method === "OPTIONS") {
        return cors(new Response(null, { status: 204 }));
      }

      if (url.pathname === "/upload-url" && request.method === "POST") {
        if (!checkToken(request, env)) {
          return cors(json({ error: "Unauthorized" }, 401));
        }

        const storagePath = `${crypto.randomUUID().replaceAll("-", "")}.zip`;
        const uploadUrl = await presignR2Put(env, storagePath, 60 * 15);
        return cors(json({ storage_path: storagePath, upload_url: uploadUrl }));
      }

      if (url.pathname.startsWith("/download/") && request.method === "GET") {
        const key = decodeURIComponent(url.pathname.slice("/download/".length));
        if (!key || key.includes("..")) {
          return cors(json({ error: "Bad path" }, 400));
        }

        const object = await env.LEVELS_BUCKET.get(key);
        if (object == null) {
          return cors(json({ error: "Not found" }, 404));
        }

        const headers = new Headers();
        object.writeHttpMetadata(headers);
        headers.set("etag", object.httpEtag);
        headers.set("Content-Type", object.httpMetadata?.contentType || "application/zip");
        headers.set("Cache-Control", "public, max-age=3600");
        applyCors(headers);
        return new Response(object.body, { headers });
      }

      return cors(json({ ok: true, service: "kaplao-levels" }));
    } catch (err) {
      return cors(json({ error: String(err && err.message ? err.message : err) }, 500));
    }
  },
};

function checkToken(request, env) {
  const expected = env.UPLOAD_TOKEN || "";
  if (!expected) return true;
  return request.headers.get("X-Upload-Token") === expected;
}

function json(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function cors(response) {
  applyCors(response.headers);
  return response;
}

function applyCors(headers) {
  headers.set("Access-Control-Allow-Origin", "*");
  headers.set("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS");
  headers.set("Access-Control-Allow-Headers", "Content-Type, X-Upload-Token");
}

async function presignR2Put(env, key, expiresSeconds) {
  const accountId = must(env.R2_ACCOUNT_ID, "R2_ACCOUNT_ID");
  const accessKeyId = must(env.R2_ACCESS_KEY_ID, "R2_ACCESS_KEY_ID");
  const secretAccessKey = must(env.R2_SECRET_ACCESS_KEY, "R2_SECRET_ACCESS_KEY");
  const bucket = must(env.R2_BUCKET_NAME, "R2_BUCKET_NAME");

  const host = `${accountId}.r2.cloudflarestorage.com`;
  const path = `/${bucket}/${key}`;

  return buildPresignedPutUrl({
    host,
    path,
    accessKeyId,
    secretAccessKey,
    region: "auto",
    service: "s3",
    expiresSeconds,
    contentType: "application/zip",
  });
}

async function buildPresignedPutUrl({
  host,
  path,
  accessKeyId,
  secretAccessKey,
  region,
  service,
  expiresSeconds,
  contentType,
}) {
  const now = new Date();
  const amzDate = toAmzDate(now);
  const dateStamp = amzDate.slice(0, 8);
  const credentialScope = `${dateStamp}/${region}/${service}/aws4_request`;
  const credential = `${accessKeyId}/${credentialScope}`;

  const params = new URLSearchParams({
    "X-Amz-Algorithm": "AWS4-HMAC-SHA256",
    "X-Amz-Credential": credential,
    "X-Amz-Date": amzDate,
    "X-Amz-Expires": String(expiresSeconds),
    "X-Amz-SignedHeaders": "content-type;host",
  });

  // Canonical query string must be sorted.
  const sorted = new URLSearchParams([...params.entries()].sort((a, b) => a[0].localeCompare(b[0])));
  const canonicalQuerystring = sorted.toString();

  const canonicalHeaders = `content-type:${contentType}\nhost:${host}\n`;
  const signedHeaders = "content-type;host";
  const canonicalRequest = [
    "PUT",
    path,
    canonicalQuerystring,
    canonicalHeaders,
    signedHeaders,
    "UNSIGNED-PAYLOAD",
  ].join("\n");

  const stringToSign = [
    "AWS4-HMAC-SHA256",
    amzDate,
    credentialScope,
    await sha256Hex(canonicalRequest),
  ].join("\n");

  const signingKey = await getSignatureKey(secretAccessKey, dateStamp, region, service);
  const signature = await hmacHex(signingKey, stringToSign);
  sorted.set("X-Amz-Signature", signature);

  return `https://${host}${path}?${sorted.toString()}`;
}

function must(value, name) {
  if (!value) throw new Error(`Missing env ${name}`);
  return value;
}

function toAmzDate(date) {
  return date.toISOString().replace(/[:-]|\.\d{3}/g, "");
}

async function sha256Hex(content) {
  const data = typeof content === "string" ? encoder.encode(content) : content;
  const hash = await crypto.subtle.digest("SHA-256", data);
  return hex(hash);
}

async function hmac(key, content) {
  const cryptoKey = await crypto.subtle.importKey(
    "raw",
    typeof key === "string" ? encoder.encode(key) : key,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  return crypto.subtle.sign("HMAC", cryptoKey, encoder.encode(content));
}

async function hmacHex(key, content) {
  return hex(await hmac(key, content));
}

async function getSignatureKey(secretKey, dateStamp, regionName, serviceName) {
  const kDate = await hmac(`AWS4${secretKey}`, dateStamp);
  const kRegion = await hmac(kDate, regionName);
  const kService = await hmac(kRegion, serviceName);
  return hmac(kService, "aws4_request");
}

function hex(buffer) {
  return [...new Uint8Array(buffer)].map((b) => b.toString(16).padStart(2, "0")).join("");
}
