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
    // Always return ok to avoid email enumeration.
    if (!user) {
      return NextResponse.json({
        ok: true,
        message: "If that email is registered, a reset code was sent.",
      });
    }

    const mail = await issueAndSendCode({
      email,
      purpose: "reset_password",
      nickname: user.nickname,
    });

    return NextResponse.json({
      ok: true,
      emailed: mail.emailed,
      previewCode: mail.previewCode,
      message: mail.emailed
        ? "If that email is registered, a reset code was sent."
        : "Email not configured — use the preview code on the reset page.",
    });
  } catch (error) {
    console.error("[forgot-password]", error);
    const message = error instanceof Error ? error.message : "Failed to start password reset.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
