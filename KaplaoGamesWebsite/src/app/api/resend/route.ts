import { NextResponse } from "next/server";
import { isValidEmail } from "@/lib/auth";
import { issueAndSendCode } from "@/lib/mail";
import { findUserByEmail } from "@/lib/store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const email = String(body.email || "").trim().toLowerCase();
    if (!isValidEmail(email)) {
      return NextResponse.json({ error: "Invalid email." }, { status: 400 });
    }

    const user = await findUserByEmail(email);
    if (!user) {
      return NextResponse.json({ error: "Account not found." }, { status: 404 });
    }
    if (user.verified) {
      return NextResponse.json({ error: "Account is already verified. You can log in." }, { status: 400 });
    }

    const mail = await issueAndSendCode({
      email,
      purpose: "verify_email",
      nickname: user.nickname,
    });

    return NextResponse.json({
      ok: true,
      emailed: mail.emailed,
      previewCode: mail.previewCode,
      message: mail.emailed ? "Code sent." : "Email not configured — use preview code.",
    });
  } catch (error) {
    console.error("[resend]", error);
    const message = error instanceof Error ? error.message : "Failed to resend code.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
