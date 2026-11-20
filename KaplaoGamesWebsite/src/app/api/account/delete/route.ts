import { NextResponse } from "next/server";
import { sessionFromAuthToken } from "@/lib/auth";
import { deletePlayerAccount, findUserByEmail } from "@/lib/store";
import { appendPlayerLog } from "@/lib/player-logs";

export const dynamic = "force-dynamic";

export async function DELETE(request: Request) {
  try {
    const authHeader = request.headers.get("authorization") || "";
    const token = authHeader.replace(/^Bearer\s+/i, "").trim();

    if (!token) {
      console.warn("[delete-account] Request without auth token");
      return NextResponse.json({ error: "Not authenticated." }, { status: 401 });
    }

    const session = await sessionFromAuthToken(token);
    if (!session || !session.email) {
      console.warn("[delete-account] Invalid session for token");
      return NextResponse.json({ error: "Invalid session." }, { status: 401 });
    }

    const email = session.email;
    console.log(`[delete-account] Attempting to delete account for: ${email}`);

    // Look up user before deletion so we can log the nickname
    const user = await findUserByEmail(email);
    const nickname = user?.nickname || "Unknown";

    // Delete the user account
    const deleted = await deletePlayerAccount(email);

    if (!deleted) {
      console.warn(`[delete-account] User not found for email: ${email}`);
      return NextResponse.json({ error: "Account not found." }, { status: 404 });
    }

    // Log the account deletion
    try {
      await appendPlayerLog({
        action: "account_deleted",
        nickname,
        email,
        metadata: { deletedAt: new Date().toISOString() },
      });
    } catch (logErr) {
      // Don't fail the deletion if logging fails
      console.error("[delete-account] Failed to write log entry:", logErr);
    }

    console.log(`[delete-account] Successfully deleted account for: ${email} (${nickname})`);

    return NextResponse.json({
      ok: true,
      message: "Account deleted successfully.",
    });
  } catch (error) {
    console.error("[delete-account] Error:", error);
    return NextResponse.json({ error: "Failed to delete account." }, { status: 500 });
  }
}
