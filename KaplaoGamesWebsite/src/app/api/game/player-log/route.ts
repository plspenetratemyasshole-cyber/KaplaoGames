import { NextResponse } from "next/server";
import { verifyGameServerSecret } from "@/lib/auth";
import { appendPlayerLog } from "@/lib/player-logs";
import { isPlayerLogAction } from "@/lib/player-log-types";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    if (!verifyGameServerSecret(request)) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json().catch(() => ({}));
    const action = String(body.action || "").trim();
    if (!action) {
      return NextResponse.json({ error: "Missing action." }, { status: 400 });
    }
    if (!isPlayerLogAction(action)) {
      return NextResponse.json({ error: `Unknown action: ${action}` }, { status: 400 });
    }

    const record = await appendPlayerLog({
      action,
      nickname: body.nickname,
      email: body.email,
      levelTitle: body.levelTitle,
      actorNickname: body.actorNickname,
      actorEmail: body.actorEmail,
      metadata: body.metadata,
    });

    return NextResponse.json({ ok: true, record });
  } catch (error) {
    console.error("[game/player-log]", error);
    return NextResponse.json({ error: "Failed to append log." }, { status: 500 });
  }
}
