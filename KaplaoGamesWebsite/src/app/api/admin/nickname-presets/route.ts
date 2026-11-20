import { NextResponse } from "next/server";
import { requireWebConsoleSession } from "@/lib/auth";
import {
  BUILTIN_NICKNAME_PRESETS,
  isBuiltinNicknamePreset,
  isValidNicknamePresetName,
  normalizeNicknamePresetName,
} from "@/lib/player-roles";
import { normalizeNicknamePresetStyle } from "@/lib/nickname-preset-style";
import {
  addNicknamePreset,
  listAllNicknamePresetNames,
  listNicknamePresets,
  removeNicknamePreset,
  updateNicknamePresetStyle,
} from "@/lib/store";

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

    const [customPresets, presetNames] = await Promise.all([
      listNicknamePresets(),
      listAllNicknamePresetNames(),
    ]);

    return NextResponse.json({
      ok: true,
      builtinPresets: BUILTIN_NICKNAME_PRESETS,
      customPresets,
      presetNames,
    });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to load presets." }, { status: 500 });
  }
}

export async function POST(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const name = normalizeNicknamePresetName(String(body.name || ""));
    if (!isValidNicknamePresetName(name)) {
      return NextResponse.json({ error: "Invalid preset name (use a-z, 0-9, _)." }, { status: 400 });
    }
    if (isBuiltinNicknamePreset(name)) {
      return NextResponse.json({ error: "Built-in preset already exists." }, { status: 400 });
    }

    const style = body.style ? normalizeNicknamePresetStyle(body.style) : undefined;
    const preset = await addNicknamePreset(name, session.email, style);
    return NextResponse.json({ ok: true, preset });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "Failed to add preset.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

export async function PUT(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const name = normalizeNicknamePresetName(String(body.name || ""));
    if (!name) {
      return NextResponse.json({ error: "Preset name required." }, { status: 400 });
    }

    const style = normalizeNicknamePresetStyle(body.style);
    const preset = await updateNicknamePresetStyle(name, style, session.email);
    return NextResponse.json({ ok: true, preset });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "Failed to update preset.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const name = normalizeNicknamePresetName(String(body.name || ""));
    if (!name) {
      return NextResponse.json({ error: "Preset name required." }, { status: 400 });
    }

    await removeNicknamePreset(name);
    return NextResponse.json({ ok: true });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "Failed to remove preset.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
