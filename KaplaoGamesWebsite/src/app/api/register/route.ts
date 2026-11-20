import { NextResponse } from "next/server";
import {
  hashPassword,
  isValidEmail,
  sanitizeNickname,
} from "@/lib/auth";
import { issueAndSendCode } from "@/lib/mail";
import { isSupabaseConfigured } from "@/lib/supabase";
import { appendPlayerLog } from "@/lib/player-logs";
import {
  findUserByEmail,
  findUserByNickname,
  upsertUser,
} from "@/lib/store";

export const dynamic = "force-dynamic";

function newId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  const hex = Array.from({ length: 32 }, () =>
    Math.floor(Math.random() * 16).toString(16)
  ).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-4${hex.slice(13, 16)}-a${hex.slice(17, 20)}-${hex.slice(20)}`;
}

function errorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  return String(error);
}

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const nickname = sanitizeNickname(String(body.nickname || ""));
    const email = String(body.email || "").trim().toLowerCase();
    const password = String(body.password || "");

    if (!nickname || nickname.toLowerCase() === "player") {
      return NextResponse.json(
        { error: "Choose a nickname (cannot be Player)." },
        { status: 400 }
      );
    }
    if (!isValidEmail(email)) {
      return NextResponse.json({ error: "Invalid email." }, { status: 400 });
    }
    if (password.length < 6) {
      return NextResponse.json(
        { error: "Password must be at least 6 characters." },
        { status: 400 }
      );
    }

    if (!isSupabaseConfigured() && process.env.VERCEL === "1") {
      return NextResponse.json(
        {
          error:
            "Supabase is not configured on the server. Set SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY in Vercel.",
        },
        { status: 500 }
      );
    }

    if (await findUserByNickname(nickname)) {
      return NextResponse.json({ error: "Nickname already taken." }, { status: 409 });
    }
    const existingEmail = await findUserByEmail(email);
    if (existingEmail) {
      return NextResponse.json({ error: "Email already registered." }, { status: 409 });
    }

    const passwordHash = await hashPassword(password);
    const user = {
      id: newId(),
      nickname,
      email,
      passwordHash,
      verified: false,
      createdAt: new Date().toISOString(),
    };

    await upsertUser(user);

    const mail = await issueAndSendCode({
      email,
      purpose: "verify_email",
      nickname,
    });

    void appendPlayerLog({
      action: "account_created",
      nickname: user.nickname,
      email: user.email,
      metadata: { source: "register", verified: false },
    }).catch((error) => console.error("[register] log", error));

    return NextResponse.json({
      ok: true,
      needsVerification: true,
      email: user.email,
      nickname: user.nickname,
      emailed: mail.emailed,
      previewCode: mail.previewCode,
      message: mail.emailed
        ? "Account created. Enter the code from your email."
        : "Account created. Email is not configured — use the preview code on the verify page.",
    });
  } catch (error) {
    console.error("[register]", error);
    return NextResponse.json(
      { error: `Registration failed: ${errorMessage(error)}` },
      { status: 500 }
    );
  }
}
