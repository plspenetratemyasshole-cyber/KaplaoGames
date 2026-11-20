import { NextResponse } from "next/server";
import { authSessionFromRequest, requireWebConsoleSession } from "@/lib/auth";
import { createDevChatMessage } from "@/lib/store";

export const dynamic = "force-dynamic";

/**
 * POST /api/dev-chat/send
 * Send a message in dev chat (from player or from admin replying)
 * 
 * Body:
 * {
 *   message: string,
 *   isFromAdmin?: boolean,  // if true, requires admin rights
 *   playerEmail?: string    // required if isFromAdmin=true (admin replying to specific player)
 * }
 */
export async function POST(request: Request) {
  try {
    const body = await request.json();
    const message = String(body.message || "").trim();
    const isFromAdmin = Boolean(body.isFromAdmin);
    const targetPlayerEmail = body.playerEmail ? String(body.playerEmail).trim().toLowerCase() : undefined;

    if (!message) {
      return NextResponse.json({ error: "Message cannot be empty." }, { status: 400 });
    }

    if (message.length > 5000) {
      return NextResponse.json({ error: "Message too long (max 5000 chars)." }, { status: 400 });
    }

    // If sending as admin, require admin rights
    if (isFromAdmin) {
      const adminSession = await requireWebConsoleSession(request);
      if (!adminSession) {
        return NextResponse.json({ error: "Forbidden." }, { status: 403 });
      }

      if (!targetPlayerEmail) {
        return NextResponse.json({ error: "playerEmail is required when sending as admin." }, { status: 400 });
      }

      // Admin replying to player - store with player's email as sender_email
      // but mark is_from_admin=true and include admin info
      const msg = await createDevChatMessage(
        targetPlayerEmail,
        "", // player nickname will be from existing conversation
        message,
        true, // is from admin
        adminSession.email,
        adminSession.nickname
      );

      if (!msg) {
        return NextResponse.json({ error: "Failed to send message." }, { status: 500 });
      }

      return NextResponse.json({ ok: true, message: msg });
    }

    // Regular player sending message
    const session = await authSessionFromRequest(request);
    if (!session) {
      return NextResponse.json({ error: "Unauthorized." }, { status: 401 });
    }

    const msg = await createDevChatMessage(
      session.email,
      session.nickname,
      message,
      false, // not from admin
      undefined,
      undefined
    );

    if (!msg) {
      return NextResponse.json({ error: "Failed to send message." }, { status: 500 });
    }

    return NextResponse.json({ ok: true, message: msg });
  } catch (error) {
    console.error("[dev-chat/send]", error);
    return NextResponse.json({ error: "Failed to send message." }, { status: 500 });
  }
}
