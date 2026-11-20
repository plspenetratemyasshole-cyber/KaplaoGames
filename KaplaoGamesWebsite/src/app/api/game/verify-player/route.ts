import { NextResponse } from "next/server";
import {
  resolvePlayerRole,
  sessionFromAuthToken,
  verifyGameServerSecret,
} from "@/lib/auth";
import { getPlayerSkinIds } from "@/lib/player-skins-store";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  try {
    if (!verifyGameServerSecret(request)) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const token = String(body.token || "").trim();
    if (!token) {
      return NextResponse.json({
        valid: false,
        email: "",
        nickname: "",
        isAdmin: false,
        permission: "none",
        rights: [] as string[],
        canDeleteLevels: false,
        nicknamePreset: "none",
        ownedSkinIds: [] as string[],
      });
    }

    const session = await sessionFromAuthToken(token);
    if (!session) {
      return NextResponse.json({
        valid: false,
        email: "",
        nickname: "",
        isAdmin: false,
        permission: "none",
        rights: [] as string[],
        canDeleteLevels: false,
        nicknamePreset: "none",
        ownedSkinIds: [] as string[],
      });
    }

    const role = await resolvePlayerRole(session.email);
    const ownedSkinIds = await getPlayerSkinIds(session.email);

    return NextResponse.json({
      valid: true,
      email: session.email,
      nickname: session.nickname,
      isAdmin: role.isAdmin,
      permission: role.permission,
      rights: role.rights,
      canDeleteLevels: role.canDeleteLevels,
      nicknamePreset: role.nicknamePreset,
      ownedSkinIds,
    });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Verification failed." }, { status: 500 });
  }
}
