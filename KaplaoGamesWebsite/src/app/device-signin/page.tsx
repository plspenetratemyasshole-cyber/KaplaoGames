"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Alert, Button, Card, Space, Typography, message } from "antd";
import { Suspense, useEffect, useState } from "react";

async function completeDevice(device: string, token: string) {
  if (!device) return;
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
}

function DeviceSignInForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const device = searchParams.get("device") || "";

  const [loading, setLoading] = useState(true);
  const [signingIn, setSigningIn] = useState(false);
  const [error, setError] = useState("");
  const [sessionInfo, setSessionInfo] = useState<{
    email: string;
    nickname: string;
  } | null>(null);

  useEffect(() => {
    checkExistingSession();
  }, []);

  async function checkExistingSession() {
    setLoading(true);
    setError("");

    try {
      const savedToken = localStorage.getItem("kaplao.account.token");
      if (!savedToken) {
        // No saved session - redirect to register
        redirectToRegister();
        return;
      }

      // Check if token is still valid
      const response = await fetch("/api/me/quick", {
        headers: {
          Authorization: `Bearer ${savedToken}`,
        },
      });
      const data = await response.json().catch(() => ({ ok: false }));

      if (!response.ok || !data.ok || !data.email) {
        // Invalid token - clear and redirect to register
        localStorage.removeItem("kaplao.account.token");
        redirectToRegister();
        return;
      }

      // Valid session found!
      setSessionInfo({
        email: data.email,
        nickname: data.nickname || data.email,
      });
    } catch (err) {
      console.error("[device-signin] check failed:", err);
      setError("Failed to check session");
      // On error, still redirect to register after a delay
      setTimeout(() => redirectToRegister(), 2000);
    } finally {
      setLoading(false);
    }
  }

  function redirectToRegister() {
    if (device) {
      router.push(`/register?device=${encodeURIComponent(device)}`);
    } else {
      router.push("/register");
    }
  }

  async function handleQuickSignIn() {
    if (!sessionInfo) return;

    setSigningIn(true);
    setError("");

    try {
      const savedToken = localStorage.getItem("kaplao.account.token");
      if (!savedToken) {
        setError("Session expired");
        redirectToRegister();
        return;
      }

      // Complete device linking
      await completeDevice(device, savedToken);

      if (device) {
        message.success("Signed in — return to the game");
        router.push(`/success?device=${encodeURIComponent(device)}`);
      } else {
        message.success("Already signed in");
        router.push("/account");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sign in failed");
    } finally {
      setSigningIn(false);
    }
  }

  function handleCancel() {
    // Clear saved session and go to register
    localStorage.removeItem("kaplao.account.token");
    redirectToRegister();
  }

  if (loading) {
    return (
      <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
        <Typography.Title level={3} style={{ marginTop: 0, marginBottom: 16, textAlign: "center" }}>
          Checking session…
        </Typography.Title>
      </Card>
    );
  }

  if (!sessionInfo) {
    // Should redirect, but show a fallback
    return (
      <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
        <Typography.Title level={3} style={{ marginTop: 0, marginBottom: 16, textAlign: "center" }}>
          Redirecting…
        </Typography.Title>
      </Card>
    );
  }

  return (
    <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
      <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
        Quick Sign In
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
        You have an active session. Sign in to the game with this account?
      </Typography.Paragraph>

      {device ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message="Signing in for Kaplao game"
          description={`Keep this tab open. After signing in, return to the game. Device: ${device}`}
        />
      ) : null}

      {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

      <Card
        size="small"
        style={{
          marginBottom: 24,
          backgroundColor: "var(--card-secondary-bg)",
          borderColor: "var(--border-color)",
        }}
      >
        <Typography.Text strong style={{ fontSize: 16 }}>
          {sessionInfo.nickname}
        </Typography.Text>
        <br />
        <Typography.Text type="secondary">{sessionInfo.email}</Typography.Text>
      </Card>

      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Button
          type="primary"
          size="large"
          block
          loading={signingIn}
          onClick={handleQuickSignIn}
        >
          Sign in as {sessionInfo.nickname}
        </Button>
        <Button size="large" block onClick={handleCancel} disabled={signingIn}>
          Use different account
        </Button>
      </Space>

      <Typography.Paragraph style={{ marginTop: 20, marginBottom: 0, textAlign: "center" }}>
        <a href="/account">Account Settings</a>
      </Typography.Paragraph>
    </Card>
  );
}

export default function DeviceSignInPage() {
  return (
    <main
      style={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        padding: 24,
      }}
    >
      <Suspense
        fallback={
          <Card style={{ width: 420, maxWidth: "92vw" }}>
            <Typography.Title level={3} style={{ textAlign: "center" }}>
              Loading…
            </Typography.Title>
          </Card>
        }
      >
        <DeviceSignInForm />
      </Suspense>
    </main>
  );
}
