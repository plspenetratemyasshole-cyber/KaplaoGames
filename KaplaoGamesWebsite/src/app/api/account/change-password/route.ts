import { NextResponse } from "next/server";
import { hashPassword, sessionFromAuthToken, verifyPassword } from "@/lib/auth";
import { findUserByEmail, upsertUser } from "@/lib/store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    const authHeader = request.headers.get("authorization") || "";
    const token = authHeader.replace(/^Bearer\s+/i, "").trim();

    if (!token) {
      return NextResponse.json({ error: "Not authenticated." }, { status: 401 });
    }

    const session = await sessionFromAuthToken(token);
    if (!session || !session.email) {
      return NextResponse.json({ error: "Invalid session." }, { status: 401 });
    }

    const body = await request.json();
    const oldPassword = String(body.oldPassword || "");
    const newPassword = String(body.newPassword || "");

    if (!oldPassword || !newPassword) {
      return NextResponse.json(
        { error: "Old password and new password are required." },
        { status: 400 }
      );
    }

    if (newPassword.length < 6) {
      return NextResponse.json(
        { error: "New password must be at least 6 characters." },
        { status: 400 }
      );
    }

    const user = await findUserByEmail(session.email);
    if (!user) {
      return NextResponse.json({ error: "User not found." }, { status: 404 });
    }

    // Verify old password
    const oldPasswordValid = await verifyPassword(oldPassword, user.passwordHash);
    if (!oldPasswordValid) {
      return NextResponse.json({ error: "Current password is incorrect." }, { status: 400 });
    }

    // Hash new password
    const newPasswordHash = await hashPassword(newPassword);

    // Update user
    await upsertUser({
      ...user,
      passwordHash: newPasswordHash,
    });

    return NextResponse.json({
      ok: true,
      message: "Password changed successfully.",
    });
  } catch (error) {
    console.error("[change-password]", error);
    return NextResponse.json({ error: "Failed to change password." }, { status: 500 });
  }
}
