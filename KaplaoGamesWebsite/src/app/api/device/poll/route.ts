import { NextResponse } from "next/server";
import { getDeviceSession, updateDeviceSession } from "@/lib/store";

export const dynamic = "force-dynamic";
export const revalidate = 0;

const noStoreHeaders = {
  "Cache-Control": "no-store, no-cache, must-revalidate, max-age=0",
  Pragma: "no-cache",
  "CDN-Cache-Control": "no-store",
  "Vercel-CDN-Cache-Control": "no-store",
};

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const code = String(searchParams.get("code") || "").trim().toUpperCase();
  if (!code) {
    return NextResponse.json({ error: "Missing code." }, { status: 400, headers: noStoreHeaders });
  }

  const session = await getDeviceSession(code);
  if (!session) {
    return NextResponse.json({ status: "unknown" }, { headers: noStoreHeaders });
  }

  if (Date.parse(session.expiresAt) < Date.now()) {
    session.status = "expired";
    await updateDeviceSession(session);
    return NextResponse.json({ status: "expired" }, { headers: noStoreHeaders });
  }

  if (session.status !== "ready") {
    return NextResponse.json({ status: session.status }, { headers: noStoreHeaders });
  }

  return NextResponse.json(
    {
      status: "ready",
      token: session.token,
      nickname: session.nickname,
      email: session.email,
    },
    { headers: noStoreHeaders }
  );
}
