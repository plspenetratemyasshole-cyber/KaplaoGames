"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Alert, Button, Card, Form, Input, Typography, message } from "antd";
import { Suspense, useEffect, useState } from "react";
import MobileNav from "@/components/MobileNav";

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

function RegisterForm() {
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
        // Ignore error, let user register
      }

      setChecking(false);
    };

    checkSession();
  }, [router]);

  async function onFinish(values: {
    nickname: string;
    email: string;
    password: string;
    confirmPassword: string;
  }) {
    setLoading(true);
    setError("");
    try {
      const response = await fetch("/api/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const data = await response.json();
      if (!response.ok) {
        setError(data.error || "Registration failed");
        return;
      }

      if (data.needsVerification) {
        const params = new URLSearchParams({ email: data.email || values.email });
        if (device) params.set("device", device);
        if (data.previewCode) params.set("preview", data.previewCode);
        message.success(data.message || "Check your email for a code");
        router.push(`/verify?${params.toString()}`);
        return;
      }

      await completeDevice(device, data.token);
      if (data.token) localStorage.setItem("kaplao.account.token", data.token);

      if (device) {
        message.success("Account created — return to the game");
        router.push(`/success?device=${encodeURIComponent(device)}`);
      } else {
        message.success("Account created");
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
        Register
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
        Create a Kaplao account. You will verify your email with a code.
      </Typography.Paragraph>

      {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

      <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
        <Form.Item
          label="Nickname"
          name="nickname"
          rules={[
            { required: true, message: "Enter a nickname" },
            { max: 20, message: "Max 20 characters" },
          ]}
        >
          <Input size="large" placeholder="CoolRunner" maxLength={20} />
        </Form.Item>
        <Form.Item
          label="Email"
          name="email"
          rules={[
            { required: true, message: "Enter email" },
            { type: "email", message: "Invalid email" },
          ]}
        >
          <Input size="large" placeholder="you@email.com" />
        </Form.Item>
        <Form.Item
          label="Password"
          name="password"
          rules={[
            { required: true, message: "Enter password" },
            { min: 6, message: "At least 6 characters" },
          ]}
        >
          <Input.Password size="large" placeholder="••••••••" />
        </Form.Item>
        <Form.Item
          label="Confirm Password"
          name="confirmPassword"
          dependencies={["password"]}
          rules={[
            { required: true, message: "Confirm your password" },
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue("password") === value) {
                  return Promise.resolve();
                }
                return Promise.reject(new Error("Passwords do not match"));
              },
            }),
          ]}
        >
          <Input.Password size="large" placeholder="••••••••" />
        </Form.Item>
        <Button type="primary" htmlType="submit" size="large" block loading={loading}>
          Create account
        </Button>
      </Form>

      <Typography.Paragraph style={{ marginTop: 20, marginBottom: 0, textAlign: "center", fontSize: 12 }}>
        <Link href={device ? `/login?device=${encodeURIComponent(device)}` : "/login"}>
          Log in to account
        </Link>
      </Typography.Paragraph>
    </Card>
  );
}

export default function RegisterPage() {
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
          <RegisterForm />
        </Suspense>
      </main>
    </>
  );
}
