import { NextResponse } from "next/server";
import { sessionFromAuthToken } from "@/lib/auth";

export const dynamic = "force-dynamic";

/**
 * Quick check if the token in Authorization header is valid.
 * Returns { ok: true, email, nickname } or { ok: false }.
 * Used by /device-signin to show "Sign in as email?" prompt.
 */
export async function GET(request: Request) {
  try {
    const authHeader = request.headers.get("authorization") || "";
    const token = authHeader.replace(/^Bearer\s+/i, "").trim();

    if (!token) {
      return NextResponse.json({ ok: false });
    }

    const session = await sessionFromAuthToken(token);
    if (!session || !session.email) {
      return NextResponse.json({ ok: false });
    }

    return NextResponse.json({
      ok: true,
      email: session.email,
      nickname: session.nickname,
    });
  } catch (error) {
    console.error("[me/quick]", error);
    return NextResponse.json({ ok: false });
  }
}
