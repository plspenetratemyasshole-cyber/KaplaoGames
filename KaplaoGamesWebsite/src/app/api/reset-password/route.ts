import { NextResponse } from "next/server";
import { hashPassword, isValidEmail } from "@/lib/auth";
import {
  consumeVerificationCode,
  findUserByEmail,
  updateUserPassword,
} from "@/lib/store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const email = String(body.email || "").trim().toLowerCase();
    const code = String(body.code || "").trim();
    const password = String(body.password || "");

    if (!isValidEmail(email) || !code) {
      return NextResponse.json({ error: "Email and code required." }, { status: 400 });
    }
    if (password.length < 6) {
      return NextResponse.json(
        { error: "Password must be at least 6 characters." },
        { status: 400 }
      );
    }

    const user = await findUserByEmail(email);
    if (!user) {
      return NextResponse.json({ error: "Invalid or expired code." }, { status: 400 });
    }

    const ok = await consumeVerificationCode(email, code, "reset_password");
    if (!ok) {
      return NextResponse.json({ error: "Invalid or expired code." }, { status: 400 });
    }

    const passwordHash = await hashPassword(password);
    await updateUserPassword(user.id, passwordHash);

    return NextResponse.json({
      ok: true,
      message: "Password updated. You can log in.",
    });
  } catch (error) {
    console.error("[reset-password]", error);
    return NextResponse.json({ error: "Failed to reset password." }, { status: 500 });
  }
}
