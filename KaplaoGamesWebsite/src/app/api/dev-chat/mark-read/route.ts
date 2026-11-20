import { NextResponse } from "next/server";
import { authSessionFromRequest, requireWebConsoleSession } from "@/lib/auth";
import { markDevChatMessagesAsRead } from "@/lib/store";

export const dynamic = "force-dynamic";

/**
 * PUT /api/dev-chat/mark-read
 * Mark messages as read
 * 
 * Body:
 * {
 *   playerEmail?: string,  // required if marking as admin
 *   readByAdmin?: boolean  // if true, marks player messages as read by admin; if false, marks admin messages as read by player
 * }
 */
export async function PUT(request: Request) {
  try {
    const body = await request.json();
    const playerEmail = body.playerEmail ? String(body.playerEmail).trim().toLowerCase() : undefined;
    const readByAdmin = Boolean(body.readByAdmin);

    if (readByAdmin) {
      // Admin marking player messages as read
      const adminSession = await requireWebConsoleSession(request);
      if (!adminSession) {
        return NextResponse.json({ error: "Forbidden." }, { status: 403 });
      }

      if (!playerEmail) {
        return NextResponse.json({ error: "playerEmail is required when marking as admin." }, { status: 400 });
      }

      await markDevChatMessagesAsRead(playerEmail, true);
      return NextResponse.json({ ok: true });
    }

    // Player marking admin messages as read
    const session = await authSessionFromRequest(request);
    if (!session) {
      return NextResponse.json({ error: "Unauthorized." }, { status: 401 });
    }

    await markDevChatMessagesAsRead(session.email, false);
    return NextResponse.json({ ok: true });
  } catch (error) {
    console.error("[dev-chat/mark-read]", error);
    return NextResponse.json({ error: "Failed to mark messages as read." }, { status: 500 });
  }
}
