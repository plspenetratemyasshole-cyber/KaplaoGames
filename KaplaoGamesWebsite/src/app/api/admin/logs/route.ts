import { NextResponse } from "next/server";
import { requireWebConsoleSession, verifyGameServerSecret } from "@/lib/auth";
import {
  appendPlayerLog,
  deletePlayerLog,
  getPlayerLogSettings,
  listPlayerLogs,
  purgeExpiredPlayerLogs,
  setPlayerLogSettings,
} from "@/lib/player-logs";
import { LogAutoDeletePolicy, PlayerLogAction } from "@/lib/player-log-types";

export const dynamic = "force-dynamic";

async function requireSuperAdmin(request: Request) {
  return requireWebConsoleSession(request);
}

export async function GET(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    await purgeExpiredPlayerLogs();
    const [logs, settings] = await Promise.all([listPlayerLogs(1000), getPlayerLogSettings()]);
    return NextResponse.json({ ok: true, logs, settings });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to load logs." }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json().catch(() => ({}));
    const id = String(body.id || "").trim();
    if (!id) {
      return NextResponse.json({ error: "Missing log id." }, { status: 400 });
    }

    await deletePlayerLog(id);
    return NextResponse.json({ ok: true });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to delete log." }, { status: 500 });
  }
}

export async function PUT(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json().catch(() => ({}));
    const autoDelete = String(body.autoDelete || "none") as LogAutoDeletePolicy;
    const settings = await setPlayerLogSettings(autoDelete, session.email);
    return NextResponse.json({ ok: true, settings });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to save settings." }, { status: 500 });
  }
}

export async function POST(request: Request) {
  try {
    if (!verifyGameServerSecret(request)) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json().catch(() => ({}));
    const action = String(body.action || "").trim() as PlayerLogAction;
    if (!action) {
      return NextResponse.json({ error: "Missing action." }, { status: 400 });
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
    console.error("[admin/logs POST]", error);
    return NextResponse.json({ error: "Failed to append log." }, { status: 500 });
  }
}
