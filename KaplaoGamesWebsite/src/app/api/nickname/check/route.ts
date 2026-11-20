import { NextResponse } from "next/server";
import { sanitizeNickname } from "@/lib/auth";
import { findUserByNickname } from "@/lib/store";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const nickname = sanitizeNickname(String(searchParams.get("nickname") || ""));
  if (!nickname) {
    return NextResponse.json({ available: false, error: "Empty nickname." }, { status: 400 });
  }

  const existing = await findUserByNickname(nickname);
  return NextResponse.json({ available: !existing });
}
