import { NextResponse } from "next/server";
import { authSessionFromRequest, requireWebConsoleSession } from "@/lib/auth";
import { getDevChatMessages } from "@/lib/store";

export const dynamic = "force-dynamic";

/**
 * GET /api/dev-chat/messages?playerEmail=...
 * Get all messages in a conversation
 * 
 * - If playerEmail param is provided + user is admin: get messages for that player
 * - If no playerEmail param: get messages for currently authenticated user
 * - Regular users can only see their own messages
 */
export async function GET(request: Request) {
  try {
    const url = new URL(request.url);
    const playerEmail = url.searchParams.get("playerEmail")?.trim().toLowerCase();

    // Check if requesting as admin
    if (playerEmail) {
      const adminSession = await requireWebConsoleSession(request);
      if (!adminSession) {
        return NextResponse.json({ error: "Forbidden." }, { status: 403 });
      }

      // Admin can view any player's messages
      const messages = await getDevChatMessages(playerEmail);
      return NextResponse.json({ ok: true, messages });
    }

    // Regular user - get their own messages
    const session = await authSessionFromRequest(request);
    if (!session) {
      return NextResponse.json({ error: "Unauthorized." }, { status: 401 });
    }

    const messages = await getDevChatMessages(session.email);
    return NextResponse.json({ ok: true, messages });
  } catch (error) {
    console.error("[dev-chat/messages]", error);
    return NextResponse.json({ error: "Failed to load messages." }, { status: 500 });
  }
}
