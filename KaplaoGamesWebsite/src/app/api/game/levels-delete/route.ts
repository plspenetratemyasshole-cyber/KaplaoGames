import { NextResponse } from "next/server";
import { resolvePlayerRole, sessionFromAuthToken, verifyGameServerSecret } from "@/lib/auth";
import { DeletePublicLevelError, deletePublicLevel } from "@/lib/delete-public-level";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    if (!verifyGameServerSecret(request)) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json().catch(() => ({}));
    const token = String(body.token || "").trim();
    const levelId = String(body.levelId || "").trim();
    const confirmTitle = String(body.confirmTitle || "").trim();

    if (!token) {
      return NextResponse.json({ error: "Sign in required." }, { status: 401 });
    }

    const session = await sessionFromAuthToken(token);
    if (!session) {
      return NextResponse.json(
        { error: "Invalid or expired session. Sign in again from the main menu." },
        { status: 401 }
      );
    }

    const result = await deletePublicLevel({
      email: session.email,
      nickname: session.nickname,
      levelId,
      confirmTitle,
    });

    return NextResponse.json({ ok: true, deletedId: result.deletedId });
  } catch (error) {
    if (error instanceof DeletePublicLevelError) {
      return NextResponse.json({ error: error.message }, { status: error.status });
    }

    console.error("[game/levels-delete]", error);
    return NextResponse.json({ error: "Delete failed." }, { status: 500 });
  }
}
