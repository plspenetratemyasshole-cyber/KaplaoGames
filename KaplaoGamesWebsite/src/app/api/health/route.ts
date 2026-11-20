import { NextResponse } from "next/server";
import {
  createDeviceSession,
  getDeviceSession,
  updateDeviceSession,
} from "@/lib/store";
import { isSupabaseConfigured, pingSupabase } from "@/lib/supabase";

export const dynamic = "force-dynamic";
export const revalidate = 0;

export async function GET() {
  const supabaseConfigured = isSupabaseConfigured();
  const ping = supabaseConfigured
    ? await pingSupabase()
    : { ok: false, detail: "not configured" };

  // Reproduces Unity flow: poll(pending) → complete(ready) → poll again.
  let deviceFlowOk = false;
  let deviceFlowDetail = "skipped";
  if (ping.ok) {
    const code = `__FLOW_${Date.now().toString(36).toUpperCase()}`;
    const expiresAt = new Date(Date.now() + 60_000).toISOString();
    try {
      await createDeviceSession({ code, status: "pending", expiresAt });
      const before = await getDeviceSession(code);
      await updateDeviceSession({
        code,
        status: "ready",
        expiresAt,
        token: "flow-probe",
        nickname: "Flow",
        email: "flow@test.local",
      });
      const after = await getDeviceSession(code);
      deviceFlowOk = before?.status === "pending" && after?.status === "ready" && !!after.token;
      deviceFlowDetail = deviceFlowOk
        ? "poll→complete→poll ok"
        : `before=${before?.status} after=${after?.status}`;
    } catch (e) {
      deviceFlowDetail = e instanceof Error ? e.message : "flow failed";
    }
  }

  return NextResponse.json({
    ok: ping.ok && deviceFlowOk,
    vercel: process.env.VERCEL === "1",
    supabaseConfigured,
    supabaseReachable: ping.ok,
    supabaseDetail: ping.detail,
    deviceFlowOk,
    deviceFlowDetail,
    hasJwtSecret: Boolean((process.env.JWT_SECRET || "").trim()),
  });
}
