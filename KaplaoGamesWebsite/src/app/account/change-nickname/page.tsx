"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Alert, Button, Card, Form, Input, Typography, message } from "antd";
import { useCallback, useEffect, useState } from "react";
import MobileNav from "@/components/MobileNav";

type MeResponse = {
  ok?: boolean;
  email?: string;
  nickname?: string;
  error?: string;
};

export default function ChangeNicknamePage() {
  const router = useRouter();
  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [nickname, setNickname] = useState("");

  const loadMe = useCallback(async (authToken: string) => {
    const response = await fetch("/api/me", {
      headers: { Authorization: `Bearer ${authToken}` },
    });
    const data = (await response.json().catch(() => ({}))) as MeResponse;
    if (!response.ok) {
      throw new Error(data.error || "Session expired");
    }
    setNickname(data.nickname || "");
  }, []);

  useEffect(() => {
    const saved = localStorage.getItem("kaplao.account.token") || "";
    if (!saved) {
      setLoading(false);
      router.push("/login");
      return;
    }
    setToken(saved);
    (async () => {
      try {
        await loadMe(saved);
      } catch (err) {
        localStorage.removeItem("kaplao.account.token");
        localStorage.removeItem("kaplao.admin.token");
        setToken("");
        setError(err instanceof Error ? err.message : "Failed to load account");
        router.push("/login");
      } finally {
        setLoading(false);
      }
    })();
  }, [loadMe, router]);

  async function onFinish(values: { nickname: string }) {
    if (!token) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/account/change-nickname", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify(values),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to change nickname");
        return;
      }
      if (data.token) {
        localStorage.setItem("kaplao.account.token", data.token);
        setToken(data.token);
      }
      setNickname(data.nickname || values.nickname);
      message.success("Nickname updated. Re-sign in inside the game to sync.");
      router.push("/account");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Network error");
    } finally {
      setSaving(false);
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
    return null; // Will redirect
  }

  return (
    <>
      <MobileNav isSignedIn={true} nickname={nickname} />
      <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
        <Card style={{ width: 480, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
        <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
          Change Nickname
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
          Choose a new nickname for your account.
        </Typography.Paragraph>

        {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

        <Form
          layout="vertical"
          onFinish={onFinish}
          requiredMark={false}
          initialValues={{ nickname }}
          key={nickname}
        >
          <Form.Item
            label="New Nickname"
            name="nickname"
            rules={[
              { required: true, message: "Enter a nickname" },
              { max: 20, message: "Max 20 characters" },
            ]}
          >
            <Input size="large" maxLength={20} placeholder="CoolRunner" />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block loading={saving}>
            Save nickname
          </Button>
        </Form>

        <div style={{ marginTop: 20, textAlign: "center" }}>
          <Link href="/account">
            <Button>Back to Account</Button>
          </Link>
        </div>
        </Card>
      </main>
    </>
  );
}
