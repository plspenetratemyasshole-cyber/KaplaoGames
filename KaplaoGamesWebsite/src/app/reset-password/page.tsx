"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Alert, Button, Card, Form, Input, Typography, message } from "antd";
import { Suspense, useMemo, useState } from "react";

function ResetForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const emailFromQuery = searchParams.get("email") || "";
  const previewFromQuery = searchParams.get("preview") || "";
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const initial = useMemo(
    () => ({ email: emailFromQuery, code: previewFromQuery }),
    [emailFromQuery, previewFromQuery]
  );

  async function onFinish(values: { email: string; code: string; password: string }) {
    setLoading(true);
    setError("");
    try {
      const response = await fetch("/api/reset-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const data = await response.json();
      if (!response.ok) {
        setError(data.error || "Reset failed");
        return;
      }
      message.success("Password updated");
      router.push("/login");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Network error");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
      <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
        Reset password
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
        Enter the email code and your new password.
      </Typography.Paragraph>

      {previewFromQuery ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Dev / preview code"
          description={`Suggested code: ${previewFromQuery}`}
        />
      ) : null}
      {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

      <Form layout="vertical" onFinish={onFinish} requiredMark={false} initialValues={initial}>
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
        <Form.Item label="Code" name="code" rules={[{ required: true, message: "Enter code" }]}>
          <Input size="large" placeholder="123456" maxLength={8} />
        </Form.Item>
        <Form.Item
          label="New password"
          name="password"
          rules={[
            { required: true, message: "Enter password" },
            { min: 6, message: "At least 6 characters" },
          ]}
        >
          <Input.Password size="large" placeholder="••••••••" />
        </Form.Item>
        <Button type="primary" htmlType="submit" size="large" block loading={loading}>
          Update password
        </Button>
      </Form>

      <Typography.Paragraph style={{ marginTop: 20, marginBottom: 0, textAlign: "center" }}>
        <Link href="/forgot-password">Request another code</Link>
        {" · "}
        <Link href="/login">Log in</Link>
      </Typography.Paragraph>
    </Card>
  );
}

export default function ResetPasswordPage() {
  return (
    <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
      <Suspense fallback={<Card style={{ width: 420 }}>Loading…</Card>}>
        <ResetForm />
      </Suspense>
    </main>
  );
}
