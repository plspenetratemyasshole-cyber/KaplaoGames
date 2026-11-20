"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Alert, Button, Card, Form, Input, Typography, message } from "antd";
import { useEffect, useState } from "react";
import MobileNav from "@/components/MobileNav";

export default function ChangePasswordPage() {
  const router = useRouter();
  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const saved = localStorage.getItem("kaplao.account.token") || "";
    if (!saved) {
      setLoading(false);
      router.push("/login");
      return;
    }
    setToken(saved);
    setLoading(false);
  }, [router]);

  async function onFinish(values: {
    oldPassword: string;
    newPassword: string;
    confirmPassword: string;
  }) {
    if (!token) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/account/change-password", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          oldPassword: values.oldPassword,
          newPassword: values.newPassword,
        }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to change password");
        return;
      }
      message.success("Password changed successfully");
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
      <MobileNav isSignedIn={true} />
      <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
        <Card style={{ width: 480, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
        <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
          Change Password
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
          Enter your current password and choose a new one.
        </Typography.Paragraph>

        {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}

        <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item
            label="Current Password"
            name="oldPassword"
            rules={[{ required: true, message: "Enter current password" }]}
          >
            <Input.Password size="large" placeholder="••••••••" />
          </Form.Item>
          <Form.Item
            label="New Password"
            name="newPassword"
            rules={[
              { required: true, message: "Enter new password" },
              { min: 6, message: "At least 6 characters" },
            ]}
          >
            <Input.Password size="large" placeholder="••••••••" />
          </Form.Item>
          <Form.Item
            label="Confirm New Password"
            name="confirmPassword"
            dependencies={["newPassword"]}
            rules={[
              { required: true, message: "Confirm your new password" },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue("newPassword") === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error("Passwords do not match"));
                },
              }),
            ]}
          >
            <Input.Password size="large" placeholder="••••••••" />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block loading={saving}>
            Change password
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
