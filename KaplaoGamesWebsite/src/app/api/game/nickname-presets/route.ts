import { NextResponse } from "next/server";
import { verifyGameServerSecret } from "@/lib/auth";
import { listNicknamePresets } from "@/lib/store";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    if (!verifyGameServerSecret(request)) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const customPresets = await listNicknamePresets();
    return NextResponse.json({
      ok: true,
      presets: customPresets.map((preset) => ({
        name: preset.name,
        style: preset.style,
      })),
    });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to load nickname presets." }, { status: 500 });
  }
}
