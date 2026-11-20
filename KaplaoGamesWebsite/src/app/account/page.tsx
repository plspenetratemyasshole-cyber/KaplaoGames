"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Alert, Button, Card, Space, Typography, message, Popconfirm } from "antd";
import { useCallback, useEffect, useState } from "react";
import MobileNav from "@/components/MobileNav";

type MeResponse = {
  ok?: boolean;
  email?: string;
  nickname?: string;
  isSuperAdmin?: boolean;
  error?: string;
};

export default function AccountPage() {
  const router = useRouter();
  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");
  const [email, setEmail] = useState("");
  const [nickname, setNickname] = useState("");
  const [isSuperAdmin, setIsSuperAdmin] = useState(false);

  const loadMe = useCallback(async (authToken: string) => {
    const response = await fetch("/api/me", {
      headers: { Authorization: `Bearer ${authToken}` },
    });
    const data = (await response.json().catch(() => ({}))) as MeResponse;
    if (!response.ok) {
      throw new Error(data.error || "Session expired");
    }
    setEmail(data.email || "");
    setNickname(data.nickname || "");
    
    // Check admin rights
    if (data.isSuperAdmin) {
      setIsSuperAdmin(true);
      // Sync admin token
      localStorage.setItem("kaplao.admin.token", authToken);
    } else {
      // Not an admin — make sure no stale admin token lingers
      localStorage.removeItem("kaplao.admin.token");
    }
  }, []);

  useEffect(() => {
    const saved = localStorage.getItem("kaplao.account.token") || "";
    if (!saved) {
      setLoading(false);
      return;
    }
    setToken(saved);
    (async () => {
      try {
        await loadMe(saved);
      } catch (err) {
        localStorage.removeItem("kaplao.account.token");
        setToken("");
        setError(err instanceof Error ? err.message : "Failed to load account");
      } finally {
        setLoading(false);
      }
    })();
  }, [loadMe]);

  function signOut() {
    localStorage.removeItem("kaplao.account.token");
    localStorage.removeItem("kaplao.admin.token");
    setToken("");
    setEmail("");
    setNickname("");
    message.success("Signed out");
    router.push("/login");
  }

  async function deleteAccount() {
    if (!token) return;
    
    setDeleting(true);
    setError("");
    try {
      const response = await fetch("/api/account/delete", {
        method: "DELETE",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
      });
      
      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.error || "Failed to delete account");
      }
      
      localStorage.removeItem("kaplao.account.token");
      localStorage.removeItem("kaplao.admin.token");
      message.success("Account deleted successfully");
      router.push("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete account");
    } finally {
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <>
        <MobileNav isSignedIn={false} />
        <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
          <Card style={{ width: 420 }}>Loading…</Card>
        </main>
      </>
    );
  }

  if (!token) {
    return (
      <>
        <MobileNav isSignedIn={false} />
        <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
          <Card style={{ width: 420, maxWidth: "92vw" }}>
            <Typography.Title level={3} style={{ marginTop: 0 }}>
              Account
            </Typography.Title>
            <Typography.Paragraph type="secondary">
              Log in to change your nickname or manage your Kaplao account.
            </Typography.Paragraph>
            <Space>
              <Link href="/login">
                <Button type="primary">Log in</Button>
              </Link>
              <Link href="/register">
                <Button>Register</Button>
              </Link>
            </Space>
          </Card>
        </main>
      </>
    );
  }

  return (
    <>
      <MobileNav 
        isSignedIn={true} 
        nickname={nickname}
        isSuperAdmin={isSuperAdmin}
        onSignOut={signOut}
      />
      <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
        <Card style={{ width: 480, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
          <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
            Account
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
            Signed in as <b>{nickname}</b>
            <br />
            {email}
          </Typography.Paragraph>

          {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

          <Space direction="vertical" size="middle" style={{ width: "100%" }}>
            <Link href="/account/change-nickname" style={{ display: "block" }}>
              <Button size="large" block>
                Change nickname
              </Button>
            </Link>
            <Link href="/account/change-password" style={{ display: "block" }}>
              <Button size="large" block>
                Change password
              </Button>
            </Link>
            <Link href="/account/contact-dev" style={{ display: "block" }}>
              <Button size="large" block>
                Contact Developer
              </Button>
            </Link>
            {isSuperAdmin ? (
              <Link href="/admin" style={{ display: "block" }}>
                <Button size="large" block type="primary">
                  Admin panel
                </Button>
              </Link>
            ) : null}
            <Button size="large" block onClick={signOut}>
              Sign out
            </Button>
            
            <Popconfirm
              title="Delete Account"
              description="Are you sure you want to delete your account? This action cannot be undone."
              onConfirm={deleteAccount}
              okText="Yes, delete my account"
              cancelText="Cancel"
              okButtonProps={{ danger: true, loading: deleting }}
            >
              <Button size="large" block danger loading={deleting}>
                Delete Account
              </Button>
            </Popconfirm>
          </Space>
        </Card>
      </main>
    </>
  );
}
