import { NextResponse } from "next/server";
import { randomDeviceCode } from "@/lib/auth";
import { createDeviceSession } from "@/lib/store";

export const dynamic = "force-dynamic";
export const revalidate = 0;

export async function POST() {
  const code = randomDeviceCode();
  const expiresAt = new Date(Date.now() + 10 * 60 * 1000).toISOString();
  await createDeviceSession({
    code,
    status: "pending",
    expiresAt,
  });

  return NextResponse.json({
    ok: true,
    deviceCode: code,
    expiresAt,
    pollIntervalMs: 1000,
  });
}
