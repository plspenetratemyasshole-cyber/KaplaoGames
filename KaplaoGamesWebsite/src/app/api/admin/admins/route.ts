import { NextResponse } from "next/server";
import {
  isSuperAdminEmail,
  isValidEmail,
  requireWebConsoleSession,
  SUPER_ADMIN_EMAIL,
} from "@/lib/auth";
import { addAdminEmail, listAdmins, removeAdminEmail } from "@/lib/store";

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

    const admins = await listAdmins();
    return NextResponse.json({
      ok: true,
      superAdminEmail: SUPER_ADMIN_EMAIL,
      admins,
    });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to load admins." }, { status: 500 });
  }
}

export async function POST(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const email = String(body.email || "").trim().toLowerCase();
    if (!isValidEmail(email)) {
      return NextResponse.json({ error: "Invalid email." }, { status: 400 });
    }

    const record = await addAdminEmail(email, session.email);
    return NextResponse.json({ ok: true, admin: record });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to add admin." }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  try {
    const session = await requireSuperAdmin(request);
    if (!session) {
      return NextResponse.json({ error: "Forbidden." }, { status: 403 });
    }

    const body = await request.json();
    const email = String(body.email || "").trim().toLowerCase();
    if (!email) {
      return NextResponse.json({ error: "Email required." }, { status: 400 });
    }
    if (isSuperAdminEmail(email)) {
      return NextResponse.json(
        { error: "Super admin cannot be removed from the admin list." },
        { status: 400 }
      );
    }

    const removed = await removeAdminEmail(email);
    if (!removed) {
      return NextResponse.json({ error: "Admin not found." }, { status: 404 });
    }

    return NextResponse.json({ ok: true });
  } catch (error) {
    console.error(error);
    return NextResponse.json({ error: "Failed to remove admin." }, { status: 500 });
  }
}
