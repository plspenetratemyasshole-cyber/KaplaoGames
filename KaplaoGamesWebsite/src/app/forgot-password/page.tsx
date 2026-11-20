"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Alert, Button, Card, Form, Input, Typography, message } from "antd";
import { Suspense, useState } from "react";
import MobileNav from "@/components/MobileNav";

function ForgotForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [previewCode, setPreviewCode] = useState("");

  async function onFinish(values: { email: string }) {
    setLoading(true);
    setError("");
    try {
      const response = await fetch("/api/forgot-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const data = await response.json();
      if (!response.ok) {
        setError(data.error || "Request failed");
        return;
      }
      if (data.previewCode) setPreviewCode(data.previewCode);
      message.success(data.message || "Check your email");
      const params = new URLSearchParams({ email: values.email });
      if (data.previewCode) params.set("preview", data.previewCode);
      const device = searchParams.get("device");
      if (device) params.set("device", device);
      router.push(`/reset-password?${params.toString()}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Network error");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
      <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
        Forgot password
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
        We will email a reset code to your account address.
      </Typography.Paragraph>

      {previewCode ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Dev / preview code"
          description={`Use code: ${previewCode}`}
        />
      ) : null}
      {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

      <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
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
        <Button type="primary" htmlType="submit" size="large" block loading={loading}>
          Send reset code
        </Button>
      </Form>

      <Typography.Paragraph style={{ marginTop: 20, marginBottom: 0, textAlign: "center" }}>
        <Link href="/login">Back to login</Link>
      </Typography.Paragraph>
    </Card>
  );
}

export default function ForgotPasswordPage() {
  return (
    <>
      <MobileNav isSignedIn={false} />
      <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
        <Suspense fallback={<Card style={{ width: 420 }}>Loading…</Card>}>
          <ForgotForm />
        </Suspense>
      </main>
    </>
  );
}
