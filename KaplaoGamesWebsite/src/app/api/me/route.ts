import { NextResponse } from "next/server";
import { authSessionFromRequest, resolveAdminFlags } from "@/lib/auth";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    const session = await authSessionFromRequest(request);
    if (!session) {
      return NextResponse.json({ error: "Unauthorized." }, { status: 401 });
    }

    const flags = await resolveAdminFlags(session.email);
    return NextResponse.json({
      ok: true,
      nickname: session.nickname,
      email: session.email,
      isAdmin: flags.isAdmin,
      isSuperAdmin: flags.isSuperAdmin,
      rights: flags.rights,
    });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to load session." }, { status: 500 });
  }
}
