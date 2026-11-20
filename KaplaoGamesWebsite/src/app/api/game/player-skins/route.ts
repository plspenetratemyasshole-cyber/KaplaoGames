import { NextResponse } from "next/server";
import { sessionFromAuthToken } from "@/lib/auth";
import { getPlayerSkinIds } from "@/lib/player-skins-store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    const body = await request.json().catch(() => ({}));
    const headerToken = (request.headers.get("authorization") || "").replace(/^Bearer\s+/i, "").trim();
    const token = String(body.token || headerToken || "").trim();
    if (!token) {
      return NextResponse.json({ ok: false, skinIds: [] as string[] });
    }

    const session = await sessionFromAuthToken(token);
    if (!session) {
      return NextResponse.json({ ok: false, skinIds: [] as string[] });
    }

    const skinIds = await getPlayerSkinIds(session.email);
    return NextResponse.json({ ok: true, skinIds });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to load player skins." }, { status: 500 });
  }
}
