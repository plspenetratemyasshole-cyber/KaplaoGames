import { NextResponse } from "next/server";
import { sessionFromAuthToken } from "@/lib/auth";
import { appendPlayerLog } from "@/lib/player-logs";
import { getDeviceSession, updateDeviceSession } from "@/lib/store";

export const dynamic = "force-dynamic";
export const revalidate = 0;

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const code = String(body.deviceCode || "").trim().toUpperCase();
    const token = String(body.token || "").trim();

    if (!code || !token) {
      return NextResponse.json({ error: "Missing device code or token." }, { status: 400 });
    }

    const session = await sessionFromAuthToken(token);
    if (!session) {
      return NextResponse.json({ error: "Invalid or expired token." }, { status: 401 });
    }

    const existing = await getDeviceSession(code);
    if (!existing) {
      return NextResponse.json({ error: "Unknown device code." }, { status: 404 });
    }

    if (Date.parse(existing.expiresAt) < Date.now()) {
      await updateDeviceSession({ ...existing, status: "expired" });
      return NextResponse.json({ error: "Device code expired." }, { status: 410 });
    }

    const expiresAt = existing.expiresAt;

    await updateDeviceSession({
      code,
      status: "ready",
      expiresAt,
      token,
      nickname: session.nickname,
      email: session.email,
    });

    const saved = await getDeviceSession(code);
    if (!saved || saved.status !== "ready" || !saved.token) {
      console.error("[device/complete] read-after-write failed", saved);
      return NextResponse.json(
        { error: "Device session did not save. Run supabase/schema.sql (disable RLS) and redeploy." },
        { status: 500 }
      );
    }

    void appendPlayerLog({
      action: "account_login",
      nickname: session.nickname,
      email: session.email,
      metadata: { source: "device-login", deviceCode: code },
    }).catch((error) => console.error("[device/complete] log", error));

    return NextResponse.json({ ok: true, status: "ready", code });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "Failed to complete device login.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
