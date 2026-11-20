"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Button, Card, Space, Typography, Divider } from "antd";
import { UserOutlined } from "@ant-design/icons";
import MobileNav from "@/components/MobileNav";

export default function HomePage() {
  const [isSignedIn, setIsSignedIn] = useState(false);
  const [nickname, setNickname] = useState("");

  useEffect(() => {
    const token = localStorage.getItem("kaplao.account.token");
    if (token) {
      // Проверяем токен
      fetch("/api/me", {
        headers: { Authorization: `Bearer ${token}` },
      })
        .then((res) => res.json())
        .then((data) => {
          if (data.email) {
            setIsSignedIn(true);
            setNickname(data.nickname || data.email);
          }
        })
        .catch(() => {
          setIsSignedIn(false);
        });
    }
  }, []);

  return (
    <>
      <MobileNav isSignedIn={isSignedIn} nickname={nickname} />
      <main
        style={{
          minHeight: "100vh",
          display: "flex",
          flexDirection: "column",
          padding: "16px",
          paddingTop: "80px",
          gap: "16px",
          maxWidth: "600px",
          margin: "0 auto",
        }}
      >
        {/* Hero Card */}
        <Card
          style={{
            background: "linear-gradient(135deg, #2f6fed 0%, #1a47a3 100%)",
            color: "white",
            textAlign: "center",
            borderRadius: "12px",
            boxShadow: "0 8px 24px rgba(47, 111, 237, 0.3)",
          }}
        >
          <div style={{ fontSize: "48px", marginBottom: "16px" }}>🎮</div>
          <Typography.Title level={2} style={{ marginTop: 0, color: "white" }}>
            Kaplao Games
          </Typography.Title>
          <Typography.Paragraph style={{ color: "rgba(255,255,255,0.9)", marginBottom: "16px" }}>
            A 2D platformer with a built-in programming language for creating and sharing custom levels
          </Typography.Paragraph>
          <a href="https://zopo55.itch.io/kaplao-games" target="_blank" rel="noopener noreferrer">
            <Button 
              size="large" 
              style={{ 
                backgroundColor: "rgba(255, 255, 255, 0.95)", 
                color: "#2f6fed",
                fontWeight: "600",
                height: "48px",
                fontSize: "16px"
              }}
            >
              ⬇️ Download Game
            </Button>
          </a>
        </Card>

        {/* Features */}
        <Card>
          <Typography.Title level={4} style={{ marginTop: 0 }}>
            🎮 About the Game
          </Typography.Title>
          <Space direction="vertical" style={{ width: "100%" }}>
            <div>
              <Typography.Text strong>Built-in Programming Language</Typography.Text>
              <Typography.Paragraph type="secondary" style={{ margin: "4px 0" }}>
                Create complex game mechanics using a custom scripting language
              </Typography.Paragraph>
            </div>
            <Divider style={{ margin: "8px 0" }} />
            <div>
              <Typography.Text strong>Level Creator & Publisher</Typography.Text>
              <Typography.Paragraph type="secondary" style={{ margin: "4px 0" }}>
                Design your own 2D platformer levels and share them with the community
              </Typography.Paragraph>
            </div>
            <Divider style={{ margin: "8px 0" }} />
            <div>
              <Typography.Text strong>Account System</Typography.Text>
              <Typography.Paragraph type="secondary" style={{ margin: "4px 0" }}>
                Save your creations, manage published levels, and customize your profile
              </Typography.Paragraph>
            </div>
          </Space>
        </Card>

        {/* Getting Started or Account */}
        {isSignedIn ? (
          <Card>
            <Typography.Title level={4} style={{ marginTop: 0 }}>
              👋 Welcome back, {nickname}!
            </Typography.Title>
            <Space direction="vertical" size="middle" style={{ width: "100%" }}>
              <Link href="/account" style={{ display: "block" }}>
                <Button type="primary" size="large" block icon={<UserOutlined />}>
                  Go to My Account
                </Button>
              </Link>
              <Typography.Paragraph type="secondary" style={{ fontSize: "12px", textAlign: "center", marginTop: 0, marginBottom: 0 }}>
                Manage your profile, change nickname, or contact the developer
              </Typography.Paragraph>
            </Space>
          </Card>
        ) : (
          <Card>
            <Typography.Title level={4} style={{ marginTop: 0 }}>
              🚀 Getting Started
            </Typography.Title>
            <Space direction="vertical" size="middle" style={{ width: "100%" }}>
              <Link href="/register" style={{ display: "block" }}>
                <Button type="primary" size="large" block icon={<UserOutlined />}>
                  Create Account
                </Button>
              </Link>
              <Link href="/login" style={{ display: "block" }}>
                <Button size="large" block>
                  Sign In
                </Button>
              </Link>
              <Typography.Paragraph type="secondary" style={{ fontSize: "12px", textAlign: "center", marginTop: 0, marginBottom: 0 }}>
                Or open Kaplao game and sign in from Settings
              </Typography.Paragraph>
            </Space>
          </Card>
        )}

        {/* Footer Info */}
        <Card style={{ backgroundColor: "var(--card-secondary-bg)" }}>
          <Typography.Paragraph type="secondary" style={{ fontSize: "12px", marginBottom: 0 }}>
            <strong>💡 Pro Tip:</strong> Link your game account on this website to manage your profile, change your nickname, or contact the developer.
          </Typography.Paragraph>
        </Card>

        <div style={{ height: "32px" }} />
      </main>
    </>
  );
}
