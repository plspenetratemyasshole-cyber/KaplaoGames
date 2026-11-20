import { createClient, SupabaseClient } from "@supabase/supabase-js";

let client: SupabaseClient | null | undefined;

export function isLevelsSupabaseConfigured(): boolean {
  return Boolean(
    (process.env.LEVELS_SUPABASE_URL || "").trim() &&
      (process.env.LEVELS_SUPABASE_SERVICE_ROLE_KEY || "").trim()
  );
}

export function getLevelsSupabase(): SupabaseClient | null {
  if (client !== undefined) return client;

  const url = (process.env.LEVELS_SUPABASE_URL || "").trim();
  const key = (process.env.LEVELS_SUPABASE_SERVICE_ROLE_KEY || "").trim();
  if (!url || !key) {
    client = null;
    return null;
  }

  client = createClient(url, key, {
    auth: { persistSession: false, autoRefreshToken: false },
  });
  return client;
}
