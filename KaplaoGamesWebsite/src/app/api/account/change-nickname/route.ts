import { NextResponse } from "next/server";
import {
  authSessionFromRequest,
  sanitizeNickname,
  signAuthToken,
} from "@/lib/auth";
import {
  findUserByEmail,
  findUserByNickname,
  updateUserNickname,
} from "@/lib/store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    const session = await authSessionFromRequest(request);
    if (!session) {
      return NextResponse.json({ error: "Unauthorized." }, { status: 401 });
    }

    const body = await request.json();
    const nickname = sanitizeNickname(String(body.nickname || ""));
    if (!nickname || nickname.toLowerCase() === "player") {
      return NextResponse.json(
        { error: "Choose a nickname (cannot be Player)." },
        { status: 400 }
      );
    }

    const existing = await findUserByNickname(nickname);
    if (existing && existing.id !== session.userId) {
      return NextResponse.json({ error: "Nickname already taken." }, { status: 409 });
    }

    const user = await findUserByEmail(session.email);
    if (!user || user.id !== session.userId) {
      return NextResponse.json({ error: "Account not found." }, { status: 404 });
    }
    if (!user.verified) {
      return NextResponse.json({ error: "Verify your email first." }, { status: 403 });
    }

    const updated = await updateUserNickname(user.id, nickname);
    if (!updated) {
      return NextResponse.json({ error: "Failed to update nickname." }, { status: 500 });
    }

    const token = await signAuthToken({
      sub: updated.id,
      nickname: updated.nickname,
      email: updated.email,
    });

    return NextResponse.json({
      ok: true,
      nickname: updated.nickname,
      email: updated.email,
      token,
      message: "Nickname updated.",
    });
  } catch (error) {
    console.error("[change-nickname]", error);
    return NextResponse.json({ error: "Failed to change nickname." }, { status: 500 });
  }
}
