import { NextResponse } from "next/server";
import { isValidEmail, signAuthToken } from "@/lib/auth";
import { consumeVerificationCode, findUserByEmail, upsertUser } from "@/lib/store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const email = String(body.email || "").trim().toLowerCase();
    const code = String(body.code || "").trim();

    if (!isValidEmail(email) || !code) {
      return NextResponse.json({ error: "Email and code required." }, { status: 400 });
    }

    const user = await findUserByEmail(email);
    if (!user) {
      return NextResponse.json({ error: "Account not found." }, { status: 404 });
    }

    if (user.verified) {
      const token = await signAuthToken({
        sub: user.id,
        nickname: user.nickname,
        email: user.email,
      });
      return NextResponse.json({
        ok: true,
        alreadyVerified: true,
        token,
        nickname: user.nickname,
        email: user.email,
      });
    }

    const ok = await consumeVerificationCode(email, code, "verify_email");
    if (!ok) {
      return NextResponse.json({ error: "Invalid or expired code." }, { status: 400 });
    }

    user.verified = true;
    await upsertUser(user);

    const token = await signAuthToken({
      sub: user.id,
      nickname: user.nickname,
      email: user.email,
    });

    return NextResponse.json({
      ok: true,
      token,
      nickname: user.nickname,
      email: user.email,
      message: "Email verified.",
    });
  } catch (error) {
    console.error("[verify]", error);
    return NextResponse.json({ error: "Verification failed." }, { status: 500 });
  }
}
