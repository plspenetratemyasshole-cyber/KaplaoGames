"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Alert, Button, Card, Form, Input, Typography, message } from "antd";
import { Suspense, useEffect, useState } from "react";
import MobileNav from "@/components/MobileNav";

async function completeDevice(device: string, token: string) {
  if (!device) return { ok: true as const, skipped: true as const };
  const response = await fetch("/api/device/complete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      deviceCode: device,
      token,
    }),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.error || "Failed to link this login to the game");
  }
  return { ok: true as const, skipped: false as const };
}

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const device = searchParams.get("device") || "";
  const [loading, setLoading] = useState(false);
  const [checking, setChecking] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    // Check if user is already signed in
    const checkSession = async () => {
      const savedToken = localStorage.getItem("kaplao.account.token");
      if (!savedToken) {
        setChecking(false);
        return;
      }

      try {
        const response = await fetch("/api/me/quick", {
          headers: { Authorization: `Bearer ${savedToken}` },
        });
        const data = await response.json().catch(() => ({ ok: false }));

        if (data.ok && data.email) {
          // User is already signed in - redirect to account
          router.push("/account");
          return;
        }
      } catch {
        // Ignore error, let user login
      }

      setChecking(false);
    };

    checkSession();
  }, [router]);

  async function onFinish(values: { login: string; password: string }) {
    setLoading(true);
    setError("");
    try {
      const response = await fetch("/api/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const data = await response.json();
      if (!response.ok) {
        if (data.needsVerification && data.email) {
          const params = new URLSearchParams({ email: data.email });
          if (device) params.set("device", device);
          message.warning("Verify your email first");
          router.push(`/verify?${params.toString()}`);
          return;
        }
        setError(data.error || "Login failed");
        return;
      }

      if (data.token) {
        localStorage.setItem("kaplao.account.token", data.token);
        
        // Check if user has admin rights and sync admin token
        try {
          const meResponse = await fetch("/api/me", {
            headers: { Authorization: `Bearer ${data.token}` },
          });
          const me = await meResponse.json().catch(() => ({}));
          if (meResponse.ok && me.isSuperAdmin) {
            localStorage.setItem("kaplao.admin.token", data.token);
          }
        } catch {
          // Ignore error, admin token sync is optional
        }
      }
      await completeDevice(device, data.token);

      if (device) {
        message.success("Signed in — return to the game window");
        router.push(`/success?device=${encodeURIComponent(device)}`);
      } else {
        message.success("Logged in");
        router.push("/account");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Network error");
    } finally {
      setLoading(false);
    }
  }

  if (checking) {
    return (
      <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
        <Typography.Title level={3} style={{ marginTop: 0, marginBottom: 16, textAlign: "center" }}>
          Checking session…
        </Typography.Title>
      </Card>
    );
  }

  return (
    <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
      <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
        Log in
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
        Use your nickname or email.
      </Typography.Paragraph>

      {device ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message="Signing in for Kaplao game"
          description={`Keep this tab. After login, return to the game. Device code: ${device}`}
        />
      ) : (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Opened without the game"
          description="If you want the account in Kaplao, open Settings → Sign in in the game (do not open this site manually)."
        />
      )}

      {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

      <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
        <Form.Item
          label="Nickname / Email"
          name="login"
          rules={[{ required: true, message: "Enter nickname or email" }]}
        >
          <Input size="large" placeholder="CoolRunner or you@email.com" />
        </Form.Item>
        <Form.Item
          label="Password"
          name="password"
          rules={[{ required: true, message: "Enter password" }]}
        >
          <Input.Password size="large" placeholder="••••••••" />
        </Form.Item>
        <Typography.Paragraph style={{ marginTop: -8, marginBottom: 16 }}>
          <Link href="/forgot-password">Forgot password?</Link>
        </Typography.Paragraph>
        <Button type="primary" htmlType="submit" size="large" block loading={loading}>
          Log in
        </Button>
      </Form>

      <Typography.Paragraph style={{ marginTop: 20, marginBottom: 0, textAlign: "center", fontSize: 12 }}>
        <Link href={device ? `/register?device=${encodeURIComponent(device)}` : "/register"}>
          Register new account
        </Link>
        {" · "}
        <Link href="/account">Account</Link>
      </Typography.Paragraph>
    </Card>
  );
}

export default function LoginPage() {
  return (
    <>
      <MobileNav isSignedIn={false} />
      <main
        style={{
          minHeight: "100vh",
          display: "grid",
          placeItems: "center",
          padding: 24,
        }}
      >
        <Suspense fallback={<Card style={{ width: 420 }}>Loading…</Card>}>
          <LoginForm />
        </Suspense>
      </main>
    </>
  );
}
