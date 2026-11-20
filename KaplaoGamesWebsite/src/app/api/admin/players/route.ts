import { NextResponse } from "next/server";
import {
  ALL_PLAYER_RIGHTS,
  authSessionFromRequest,
  isSuperAdminEmail,
  requireWebConsoleSession,
  SUPER_ADMIN_EMAIL,
} from "@/lib/auth";
import {
  DEFAULT_NICKNAME_PRESET,
  DEFAULT_PLAYER_RIGHTS,
  normalizeNicknamePresetName,
  normalizePlayerRights,
  PLAYER_RIGHT_OPTIONS,
} from "@/lib/player-roles";
import { deletePlayerAccount, listAllNicknamePresetNames, listPlayersWithRoles, upsertPlayerRole } from "@/lib/store";
import { deletePlayerSkins, listPlayerSkinsByEmail, setPlayerSkinIds } from "@/lib/player-skins-store";
import { normalizeAdminSkinIds } from "@/lib/runner-skins";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    const session = await requireWebConsoleSession(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const [players, presetNames, skinsByEmail] = await Promise.all([
      listPlayersWithRoles(),
      listAllNicknamePresetNames(),
      listPlayerSkinsByEmail(),
    ]);

    return NextResponse.json({
      ok: true,
      players: players.map((player) => ({
        id: player.id,
        nickname: player.nickname,
        email: player.email,
        verified: player.verified,
        createdAt: player.createdAt,
        permission: player.permission,
        rights: player.rights,
        nicknamePreset: player.nicknamePreset,
        grantedSkinIds: skinsByEmail.get(player.email.toLowerCase()) ?? [],
      })),
      rightOptions: PLAYER_RIGHT_OPTIONS,
      presetNames,
      defaults: {
        rights: DEFAULT_PLAYER_RIGHTS,
        nicknamePreset: DEFAULT_NICKNAME_PRESET,
      },
    });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to load players." }, { status: 500 });
  }
}

export async function PUT(request: Request) {
  try {
    const session = await requireWebConsoleSession(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const email = String(body.email || "").trim().toLowerCase();
    if (!email) {
      return NextResponse.json({ error: "Email required." }, { status: 400 });
    }

    let rights = normalizePlayerRights(body.rights);
    // Root owner always keeps full access.
    if (isSuperAdminEmail(email)) {
      rights = [...ALL_PLAYER_RIGHTS];
    }

    const nicknamePreset = normalizeNicknamePresetName(
      String(body.nicknamePreset || DEFAULT_NICKNAME_PRESET)
    );

    const saved = await upsertPlayerRole(email, rights, nicknamePreset, session.email);

    let grantedSkinIds: string[] | undefined;
    if (Array.isArray(body.grantedSkinIds)) {
      grantedSkinIds = await setPlayerSkinIds(
        email,
        normalizeAdminSkinIds(body.grantedSkinIds),
        session.email
      );
    }

    return NextResponse.json({ ok: true, role: saved, grantedSkinIds });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "Failed to save player role.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  try {
    const session = await requireWebConsoleSession(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const email = String(body.email || "").trim().toLowerCase();
    if (!email) {
      return NextResponse.json({ error: "Email required." }, { status: 400 });
    }

    if (email === SUPER_ADMIN_EMAIL.toLowerCase()) {
      return NextResponse.json({ error: "The root owner account cannot be deleted." }, { status: 400 });
    }

    if (email === session.email.toLowerCase()) {
      return NextResponse.json({ error: "You cannot delete your own account while signed in." }, { status: 400 });
    }

    const deleted = await deletePlayerAccount(email);
    if (!deleted) {
      return NextResponse.json({ error: "Account not found." }, { status: 404 });
    }

    await deletePlayerSkins(email);

    return NextResponse.json({ ok: true, deletedEmail: email });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "Failed to delete account.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
