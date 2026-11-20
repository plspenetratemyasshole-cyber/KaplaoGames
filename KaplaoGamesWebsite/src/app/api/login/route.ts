import { NextResponse } from "next/server";
import { signAuthToken, verifyPassword } from "@/lib/auth";
import { appendPlayerLog } from "@/lib/player-logs";
import { findUserByEmailOrNickname } from "@/lib/store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const login = String(body.login || "").trim();
    const password = String(body.password || "");

    if (!login || !password) {
      return NextResponse.json({ error: "Login and password required." }, { status: 400 });
    }

    const user = await findUserByEmailOrNickname(login);
    if (!user) {
      return NextResponse.json({ error: "Account not found." }, { status: 404 });
    }

    const ok = await verifyPassword(password, user.passwordHash);
    if (!ok) {
      return NextResponse.json({ error: "Wrong password." }, { status: 401 });
    }

    if (!user.verified) {
      return NextResponse.json(
        {
          error: "Email not verified. Enter the code from your email.",
          needsVerification: true,
          email: user.email,
        },
        { status: 403 }
      );
    }

    const token = await signAuthToken({
      sub: user.id,
      nickname: user.nickname,
      email: user.email,
    });

    void appendPlayerLog({
      action: "account_login",
      nickname: user.nickname,
      email: user.email,
      metadata: { source: "website-login" },
    }).catch((error) => console.error("[login] log", error));

    return NextResponse.json({
      ok: true,
      token,
      nickname: user.nickname,
      email: user.email,
    });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Login failed." }, { status: 500 });
  }
}
