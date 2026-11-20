import { createClient, SupabaseClient } from "@supabase/supabase-js";

let client: SupabaseClient | null | undefined;

export function isSupabaseConfigured(): boolean {
  return Boolean(
    (process.env.SUPABASE_URL || "").trim() &&
      (process.env.SUPABASE_SERVICE_ROLE_KEY || "").trim()
  );
}

/** Next.js App Router caches fetch() GETs by default — that broke device poll after complete. */
const noStoreFetch: typeof fetch = (input, init) =>
  fetch(input, { ...init, cache: "no-store" });

export function getSupabase(): SupabaseClient | null {
  if (client !== undefined) return client;
  const url = (process.env.SUPABASE_URL || "").trim();
  const key = (process.env.SUPABASE_SERVICE_ROLE_KEY || "").trim();
  if (!url || !key) {
    client = null;
    return null;
  }
  client = createClient(url, key, {
    auth: { persistSession: false, autoRefreshToken: false },
    global: { fetch: noStoreFetch },
  });
  return client;
}

/** Quick connectivity check for /api/health */
export async function pingSupabase(): Promise<{ ok: boolean; detail: string }> {
  if (!isSupabaseConfigured()) {
    return { ok: false, detail: "SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY missing" };
  }
  const sb = getSupabase();
  if (!sb) return { ok: false, detail: "client not created" };

  const { error } = await sb.from("kaplao_users").select("id").limit(1);
  if (error) {
    return {
      ok: false,
      detail: `${error.code || "error"}: ${error.message}${
        error.hint ? ` (${error.hint})` : ""
      }`,
    };
  }

  // Probe device-session write path (the Unity login failure mode).
  const probeCode = `__HEALTH_${Date.now().toString(36).toUpperCase()}`;
  const expires = new Date(Date.now() + 60_000).toISOString();
  const upsert = await sb
    .from("kaplao_device_sessions")
    .upsert(
      {
        code: probeCode,
        status: "ready",
        expires_at: expires,
        token: "health-probe",
        nickname: null,
        email: null,
      },
      { onConflict: "code" }
    )
    .select("status,token")
    .single();
  if (upsert.error) {
    return {
      ok: false,
      detail: `users ok; device upsert failed: ${upsert.error.code || ""} ${upsert.error.message}`,
    };
  }
  if (upsert.data?.status !== "ready" || upsert.data?.token !== "health-probe") {
    return {
      ok: false,
      detail: `users ok; device upsert did not stick (got ${JSON.stringify(upsert.data)})`,
    };
  }

  // Separate read must also see ready (guards against Next.js fetch cache).
  const read = await sb
    .from("kaplao_device_sessions")
    .select("status,token")
    .eq("code", probeCode)
    .maybeSingle();
  if (read.error || read.data?.status !== "ready") {
    return {
      ok: false,
      detail: `users ok; device re-read failed (got ${JSON.stringify(read.data)} err=${read.error?.message})`,
    };
  }

  await sb.from("kaplao_device_sessions").delete().eq("code", probeCode);
  return { ok: true, detail: "kaplao_users + device write/read ok" };
}
