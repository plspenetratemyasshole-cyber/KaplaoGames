import { Resend } from "resend";
import { randomCode } from "@/lib/auth";
import { saveVerificationCode } from "@/lib/store";
import type { VerificationPurpose } from "@/lib/types";

const CODE_TTL_MS = 15 * 60 * 1000;

export function emailFromAddress(): string {
  return (process.env.EMAIL_FROM || "Kaplao <onboarding@resend.dev>").trim();
}

export function appBaseUrl(): string {
  const raw =
    process.env.NEXT_PUBLIC_APP_URL ||
    process.env.APP_URL ||
    "https://kaplao.ru";
  return raw.replace(/\/$/, "");
}

export function isResendConfigured(): boolean {
  return Boolean((process.env.RESEND_API_KEY || "").trim());
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export async function issueAndSendCode(params: {
  email: string;
  purpose: VerificationPurpose;
  nickname?: string;
}): Promise<{ code: string; emailed: boolean; previewCode?: string }> {
  const email = params.email.trim().toLowerCase();
  const code = randomCode(6);
  const expiresAt = new Date(Date.now() + CODE_TTL_MS).toISOString();

  await saveVerificationCode({
    email,
    code,
    expiresAt,
    purpose: params.purpose,
  });

  const subject =
    params.purpose === "reset_password"
      ? "Kaplao — password reset code"
      : "Kaplao — email verification code";

  const title =
    params.purpose === "reset_password"
      ? "Password reset"
      : "Verify your email";

  const hint =
    params.purpose === "reset_password"
      ? "Enter this code on the password reset page. It expires in 15 minutes."
      : "Enter this code on the verification page to activate your account. It expires in 15 minutes.";

  const html = `
    <div style="font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#111">
      <h2 style="margin:0 0 12px">${escapeHtml(title)}</h2>
      <p style="margin:0 0 12px">Hi${params.nickname ? ` ${escapeHtml(params.nickname)}` : ""},</p>
      <p style="margin:0 0 12px">${escapeHtml(hint)}</p>
      <p style="font-size:28px;letter-spacing:6px;font-weight:700;margin:16px 0">${escapeHtml(code)}</p>
      <p style="margin:0;color:#666;font-size:13px">If you did not request this, ignore this email.</p>
    </div>
  `;

  const text = `${title}\n\nCode: ${code}\n\n${hint}`;

  if (!isResendConfigured()) {
    console.warn(
      `[mail] RESEND_API_KEY missing — code for ${email} (${params.purpose}): ${code}`
    );
    return { code, emailed: false, previewCode: code };
  }

  const resend = new Resend(process.env.RESEND_API_KEY!.trim());
  const { error } = await resend.emails.send({
    from: emailFromAddress(),
    to: email,
    subject,
    html,
    text,
  });

  if (error) {
    console.error("[mail] Resend error", error);
    throw new Error(error.message || "Failed to send email.");
  }

  return { code, emailed: true };
}
