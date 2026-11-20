import { NextResponse } from "next/server";
import { requireWebConsoleSession } from "@/lib/auth";
import { getDevChatConversations } from "@/lib/store";

export const dynamic = "force-dynamic";

/**
 * GET /api/dev-chat/conversations
 * Get list of all player conversations (admin only)
 * Returns array of conversations sorted by latest message
 */
export async function GET(request: Request) {
  try {
    const session = await requireWebConsoleSession(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const conversations = await getDevChatConversations();
    return NextResponse.json({ ok: true, conversations });
  } catch (error) {
    console.error("[dev-chat/conversations]", error);
    return NextResponse.json({ error: "Failed to load conversations." }, { status: 500 });
  }
}
