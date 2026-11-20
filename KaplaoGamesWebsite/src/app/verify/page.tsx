"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Alert, Button, Card, Form, Input, Typography, message } from "antd";
import { Suspense, useMemo, useState } from "react";
import MobileNav from "@/components/MobileNav";

async function completeDevice(device: string, token: string) {
  if (!device) return;
  const response = await fetch("/api/device/complete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ deviceCode: device, token }),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.error || "Failed to link this login to the game");
  }
}

function VerifyForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const emailFromQuery = searchParams.get("email") || "";
  const device = searchParams.get("device") || "";
  const previewFromQuery = searchParams.get("preview") || "";

  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const [error, setError] = useState("");
  const [previewCode, setPreviewCode] = useState(previewFromQuery);

  const initialEmail = useMemo(() => emailFromQuery, [emailFromQuery]);

  async function onFinish(values: { email: string; code: string }) {
    setLoading(true);
    setError("");
    try {
      const response = await fetch("/api/verify", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const data = await response.json();
      if (!response.ok) {
        setError(data.error || "Verification failed");
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
        
        await completeDevice(device, data.token);
      }

      message.success("Email verified");
      if (device) {
        router.push(`/success?device=${encodeURIComponent(device)}`);
      } else {
        router.push("/account");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Network error");
    } finally {
      setLoading(false);
    }
  }

  async function onResend(email: string) {
    if (!email) {
      setError("Enter your email first");
      return;
    }
    setResending(true);
    setError("");
    try {
      const response = await fetch("/api/resend", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
      const data = await response.json();
      if (!response.ok) {
        setError(data.error || "Failed to resend");
        return;
      }
      if (data.previewCode) setPreviewCode(data.previewCode);
      message.success(data.message || "Code sent");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Network error");
    } finally {
      setResending(false);
    }
  }

  return (
    <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
      <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
        Verify email
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
        Enter the 6-digit code we sent to your email.
      </Typography.Paragraph>

      {previewCode ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Dev / preview code"
          description={`Resend is not fully configured. Use code: ${previewCode}`}
        />
      ) : null}

      {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

      <Form
        layout="vertical"
        onFinish={onFinish}
        requiredMark={false}
        initialValues={{ email: initialEmail }}
      >
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
          label="Code"
          name="code"
          rules={[{ required: true, message: "Enter code" }]}
        >
          <Input size="large" placeholder="123456" maxLength={8} />
        </Form.Item>
        <Button type="primary" htmlType="submit" size="large" block loading={loading}>
          Verify
        </Button>
        <Form.Item shouldUpdate style={{ marginBottom: 0, marginTop: 12 }}>
          {({ getFieldValue }) => (
            <Button
              block
              loading={resending}
              onClick={() => onResend(String(getFieldValue("email") || ""))}
            >
              Resend code
            </Button>
          )}
        </Form.Item>
      </Form>

      <Typography.Paragraph style={{ marginTop: 20, marginBottom: 0, textAlign: "center" }}>
        <Link href={device ? `/login?device=${encodeURIComponent(device)}` : "/login"}>Log in</Link>
      </Typography.Paragraph>
    </Card>
  );
}

export default function VerifyPage() {
  return (
    <>
      <MobileNav isSignedIn={false} />
      <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
        <Suspense fallback={<Card style={{ width: 420 }}>Loading…</Card>}>
          <VerifyForm />
        </Suspense>
      </main>
    </>
  );
}
