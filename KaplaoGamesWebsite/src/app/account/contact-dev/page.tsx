"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Alert, Button, Card, Input, List, Space, Typography, message } from "antd";
import { useCallback, useEffect, useRef, useState } from "react";
import { ArrowLeftOutlined, SendOutlined } from "@ant-design/icons";
import styles from "./ContactDev.module.css";

type DevChatMessage = {
  id: string;
  senderEmail: string;
  senderNickname: string;
  message: string;
  isFromAdmin: boolean;
  adminEmail?: string;
  adminNickname?: string;
  createdAt: string;
  readByAdmin: boolean;
  readByPlayer: boolean;
};

export default function ContactDevPage() {
  const router = useRouter();
  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [messages, setMessages] = useState<DevChatMessage[]>([]);
  const [messageText, setMessageText] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const pollingRef = useRef<NodeJS.Timeout | null>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  const loadMessages = useCallback(async (authToken: string) => {
    try {
      const response = await fetch("/api/dev-chat/messages", {
        headers: { Authorization: `Bearer ${authToken}` },
      });
      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.error || "Failed to load messages");
      }
      setMessages(data.messages || []);
      
      // Mark admin messages as read
      if (data.messages?.some((m: DevChatMessage) => m.isFromAdmin && !m.readByPlayer)) {
        fetch("/api/dev-chat/mark-read", {
          method: "PUT",
          headers: {
            Authorization: `Bearer ${authToken}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ readByAdmin: false }),
        });
      }
    } catch (err) {
      console.error("Failed to load messages:", err);
    }
  }, []);

  useEffect(() => {
    const saved = localStorage.getItem("kaplao.account.token") || "";
    if (!saved) {
      setLoading(false);
      setError("You must be logged in to contact the developer.");
      return;
    }

    setToken(saved);
    (async () => {
      try {
        await loadMessages(saved);
        
        // Start polling for new messages every 5 seconds
        pollingRef.current = setInterval(() => {
          loadMessages(saved);
        }, 5000);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load chat");
      } finally {
        setLoading(false);
      }
    })();

    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
      }
    };
  }, [loadMessages]);

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  async function sendMessage() {
    if (!messageText.trim() || !token) return;

    setSending(true);
    try {
      const response = await fetch("/api/dev-chat/send", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ message: messageText }),
      });

      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.error || "Failed to send message");
      }

      setMessageText("");
      await loadMessages(token);
      message.success("Message sent!");
    } catch (err) {
      message.error(err instanceof Error ? err.message : "Failed to send message");
    } finally {
      setSending(false);
    }
  }

  function handleKeyPress(e: React.KeyboardEvent) {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  }

  if (loading) {
    return (
      <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
        <Card style={{ width: 720, maxWidth: "96vw" }}>Loading…</Card>
      </main>
    );
  }

  if (!token) {
    return (
      <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
        <Card style={{ width: 480, maxWidth: "92vw" }}>
          <Typography.Title level={3} style={{ marginTop: 0 }}>
            Contact Developer
          </Typography.Title>
          <Alert
            type="warning"
            message="You must be logged in to contact the developer."
            showIcon
            style={{ marginBottom: 16 }}
          />
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
    );
  }

  return (
    <main className={styles.container}>
      <Card
        className={styles.card}
        bodyStyle={{ display: "flex", flexDirection: "column", flex: 1, padding: 0 }}
      >
        {/* Header */}
        <div className={styles.header}>
          <Space>
            <Link href="/account">
              <Button type="text" icon={<ArrowLeftOutlined />} />
            </Link>
            <div>
              <Typography.Title level={4} className={styles.headerTitle}>
                Contact Developer
              </Typography.Title>
              <Typography.Text type="secondary" className={styles.headerSubtitle}>
                Ask questions or report issues
              </Typography.Text>
            </div>
          </Space>
        </div>

        {error ? (
          <Alert type="error" message={error} showIcon style={{ margin: 16 }} />
        ) : null}

        {/* Messages */}
        <div className={styles.messagesContainer}>
          {messages.length === 0 ? (
            <div className={styles.emptyState}>
              <Typography.Text type="secondary">
                No messages yet. Send a message to contact the developer!
              </Typography.Text>
            </div>
          ) : (
            <List
              dataSource={messages}
              renderItem={(msg) => (
                <div
                  key={msg.id}
                  className={`${styles.messageWrapper} ${
                    msg.isFromAdmin ? styles.messageWrapperAdmin : styles.messageWrapperUser
                  }`}
                >
                  <div
                    className={`${styles.messageBubble} ${
                      msg.isFromAdmin ? styles.messageBubbleAdmin : styles.messageBubbleUser
                    }`}
                  >
                    {msg.isFromAdmin && msg.adminNickname ? (
                      <Typography.Text strong className={styles.adminName}>
                        {msg.adminNickname} (Developer)
                      </Typography.Text>
                    ) : null}
                    <Typography.Text className={styles.messageText}>
                      {msg.message}
                    </Typography.Text>
                    <div className={styles.messageTime}>
                      <Typography.Text type="secondary" className={styles.messageTimeText}>
                        {new Date(msg.createdAt).toLocaleString()}
                      </Typography.Text>
                    </div>
                  </div>
                </div>
              )}
            />
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Input */}
        <div className={styles.inputArea}>
          <Space.Compact className={styles.inputCompact}>
            <Input.TextArea
              placeholder="Type your message... (Shift+Enter for new line)"
              value={messageText}
              onChange={(e) => setMessageText(e.target.value)}
              onKeyPress={handleKeyPress}
              autoSize={{ minRows: 1, maxRows: 4 }}
              maxLength={5000}
              disabled={sending}
            />
            <Button
              type="primary"
              icon={<SendOutlined />}
              onClick={sendMessage}
              loading={sending}
              disabled={!messageText.trim()}
            >
              Send
            </Button>
          </Space.Compact>
          <Typography.Text type="secondary" className={styles.inputHint}>
            Messages are monitored by developers. Response time may vary.
          </Typography.Text>
        </div>
      </Card>
    </main>
  );
}
