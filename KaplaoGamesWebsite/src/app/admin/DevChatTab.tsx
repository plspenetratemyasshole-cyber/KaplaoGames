"use client";

import { Alert, Button, Input, List, Space, Typography, message } from "antd";
import { useCallback, useEffect, useRef, useState } from "react";
import { SendOutlined, ArrowLeftOutlined } from "@ant-design/icons";
import styles from "./DevChatTab.module.css";

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

type DevChatConversation = {
  playerEmail: string;
  playerNickname: string;
  lastMessage: string;
  lastMessageAt: string;
  unreadCount: number;
  isLastFromAdmin: boolean;
};

type DevChatTabProps = {
  token: string;
  adminNickname: string;
  onConversationsChange?: (conversations: DevChatConversation[]) => void;
};

function authHeaders(token: string) {
  return {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  };
}

export function DevChatTab({ token, adminNickname, onConversationsChange }: DevChatTabProps) {
  const [conversations, setConversations] = useState<DevChatConversation[]>([]);
  const [selectedPlayerEmail, setSelectedPlayerEmail] = useState<string | null>(null);
  const [chatMessages, setChatMessages] = useState<DevChatMessage[]>([]);
  const [chatLoading, setChatLoading] = useState(false);
  const [chatMessageText, setChatMessageText] = useState("");
  const [sending, setSending] = useState(false);
  const [isMobileView, setIsMobileView] = useState(false);
  const [showChatOnMobile, setShowChatOnMobile] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const pollingRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    const checkMobile = () => {
      setIsMobileView(window.innerWidth <= 768);
    };
    checkMobile();
    window.addEventListener("resize", checkMobile);
    return () => window.removeEventListener("resize", checkMobile);
  }, []);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  const loadConversations = useCallback(async () => {
    try {
      const response = await fetch("/api/dev-chat/conversations", {
        headers: authHeaders(token),
      });
      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.error || "Failed to load conversations");
      }
      const convs = data.conversations || [];
      setConversations(convs);
      onConversationsChange?.(convs);
    } catch (err) {
      console.error("Failed to load conversations:", err);
    }
  }, [token, onConversationsChange]);

  const loadMessages = useCallback(
    async (playerEmail: string) => {
      setChatLoading(true);
      try {
        const response = await fetch(`/api/dev-chat/messages?playerEmail=${encodeURIComponent(playerEmail)}`, {
          headers: authHeaders(token),
        });
        const data = await response.json();
        if (!response.ok) {
          throw new Error(data.error || "Failed to load messages");
        }
        setChatMessages(data.messages || []);

        // Mark player messages as read by admin
        if (data.messages?.some((m: DevChatMessage) => !m.isFromAdmin && !m.readByAdmin)) {
          fetch("/api/dev-chat/mark-read", {
            method: "PUT",
            headers: authHeaders(token),
            body: JSON.stringify({ playerEmail, readByAdmin: true }),
          });
        }
      } catch (err) {
        message.error(err instanceof Error ? err.message : "Failed to load messages");
      } finally {
        setChatLoading(false);
      }
    },
    [token]
  );

  useEffect(() => {
    loadConversations();

    // Poll for new conversations every 10 seconds
    pollingRef.current = setInterval(() => {
      loadConversations();
      if (selectedPlayerEmail) {
        loadMessages(selectedPlayerEmail);
      }
    }, 10000);

    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
      }
    };
  }, [loadConversations, loadMessages, selectedPlayerEmail]);

  useEffect(() => {
    if (selectedPlayerEmail) {
      loadMessages(selectedPlayerEmail);
    }
  }, [selectedPlayerEmail, loadMessages]);

  useEffect(() => {
    scrollToBottom();
  }, [chatMessages]);

  async function sendMessage() {
    if (!chatMessageText.trim() || !selectedPlayerEmail) return;

    setSending(true);
    try {
      const response = await fetch("/api/dev-chat/send", {
        method: "POST",
        headers: authHeaders(token),
        body: JSON.stringify({
          message: chatMessageText,
          isFromAdmin: true,
          playerEmail: selectedPlayerEmail,
        }),
      });

      const data = await response.json();
      if (!response.ok) {
        throw new Error(data.error || "Failed to send message");
      }

      setChatMessageText("");
      await loadMessages(selectedPlayerEmail);
      await loadConversations(); // Refresh conversation list
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

  const selectedConversation = conversations.find((c) => c.playerEmail === selectedPlayerEmail);

  const handleConversationClick = (email: string) => {
    setSelectedPlayerEmail(email);
    if (isMobileView) {
      setShowChatOnMobile(true);
    }
  };

  const handleBackToList = () => {
    setShowChatOnMobile(false);
    setSelectedPlayerEmail(null);
  };

  // Mobile: show either list OR chat
  if (isMobileView) {
    if (showChatOnMobile && selectedPlayerEmail) {
      return (
        <Space direction="vertical" size="middle" style={{ width: "100%" }}>
          {/* Chat header with back button */}
          <div style={{ display: "flex", alignItems: "center", gap: 12, borderBottom: "1px solid #f0f0f0", paddingBottom: 12 }}>
            <Button icon={<ArrowLeftOutlined />} onClick={handleBackToList} />
            <div>
              <Typography.Title level={5} style={{ margin: 0 }}>
                {selectedConversation?.playerNickname}
              </Typography.Title>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {selectedConversation?.playerEmail}
              </Typography.Text>
            </div>
          </div>

          {/* Messages */}
          <div className={styles.messagesContainer}>
            {chatLoading ? (
              <Typography.Text type="secondary">Loading messages...</Typography.Text>
            ) : chatMessages.length === 0 ? (
              <Typography.Text type="secondary">No messages yet.</Typography.Text>
            ) : (
              <>
                {chatMessages.map((msg) => (
                  <div
                    key={msg.id}
                    className={`${styles.messageWrapper} ${msg.isFromAdmin ? styles.fromAdmin : styles.fromPlayer}`}
                  >
                    <div className={`${styles.messageBubble} ${msg.isFromAdmin ? styles.fromAdmin : styles.fromPlayer}`}>
                      {msg.isFromAdmin && msg.adminNickname ? (
                        <Typography.Text
                          strong
                          style={{
                            fontSize: 12,
                            color: "white",
                            display: "block",
                            marginBottom: 4,
                          }}
                        >
                          {msg.adminNickname} (You)
                        </Typography.Text>
                      ) : null}
                      <Typography.Text
                        style={{ whiteSpace: "pre-wrap", color: msg.isFromAdmin ? "white" : "inherit" }}
                      >
                        {msg.message}
                      </Typography.Text>
                      <div style={{ marginTop: 4 }}>
                        <Typography.Text
                          type="secondary"
                          style={{ fontSize: 11, color: msg.isFromAdmin ? "#ccc" : undefined }}
                        >
                          {new Date(msg.createdAt).toLocaleString()}
                        </Typography.Text>
                      </div>
                    </div>
                  </div>
                ))}
                <div ref={messagesEndRef} />
              </>
            )}
          </div>

          {/* Input */}
          <Space.Compact style={{ width: "100%" }}>
            <Input.TextArea
              placeholder="Type your reply..."
              value={chatMessageText}
              onChange={(e) => setChatMessageText(e.target.value)}
              onKeyPress={handleKeyPress}
              autoSize={{ minRows: 2, maxRows: 4 }}
              maxLength={5000}
              disabled={sending}
            />
            <Button
              type="primary"
              icon={<SendOutlined />}
              onClick={sendMessage}
              loading={sending}
              disabled={!chatMessageText.trim()}
              style={{ height: "auto" }}
            >
              Send
            </Button>
          </Space.Compact>
        </Space>
      );
    }

    // Mobile: show conversations list
    return (
      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Alert
          type="info"
          showIcon
          message="Developer Chat"
          description="Players can contact you from the game or website. Tap a conversation to reply."
        />

        <Typography.Title level={5}>Conversations ({conversations.length})</Typography.Title>
        {conversations.length === 0 ? (
          <Typography.Text type="secondary">No conversations yet.</Typography.Text>
        ) : (
          <List
            dataSource={conversations}
            renderItem={(conv) => (
              <List.Item
                key={conv.playerEmail}
                onClick={() => handleConversationClick(conv.playerEmail)}
                className={styles.conversationItem}
              >
                <List.Item.Meta
                  title={
                    <Space>
                      <span>{conv.playerNickname}</span>
                      {conv.unreadCount > 0 ? (
                        <span
                          style={{
                            backgroundColor: "#ff4d4f",
                            color: "white",
                            borderRadius: 10,
                            padding: "2px 8px",
                            fontSize: 12,
                          }}
                        >
                          {conv.unreadCount}
                        </span>
                      ) : null}
                    </Space>
                  }
                  description={
                    <>
                      <div style={{ fontSize: 12, color: "#666" }}>{conv.playerEmail}</div>
                      <div
                        style={{
                          fontSize: 12,
                          color: "#999",
                          marginTop: 4,
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                        }}
                      >
                        {conv.lastMessage}
                      </div>
                    </>
                  }
                />
              </List.Item>
            )}
          />
        )}
      </Space>
    );
  }

  // Desktop: show side-by-side

  return (
    <Space direction="vertical" size="middle" style={{ width: "100%" }}>
      <Alert
        type="info"
        showIcon
        message="Developer Chat"
        description="Players can contact you from the game or website. Reply here and they will see your response in real-time."
      />

      <div style={{ display: "flex", gap: 16, minHeight: 500 }}>
        {/* Conversations list (left side) */}
        <div style={{ width: 300, borderRight: "1px solid #f0f0f0", paddingRight: 16 }}>
          <Typography.Title level={5}>Conversations ({conversations.length})</Typography.Title>
          {conversations.length === 0 ? (
            <Typography.Text type="secondary">No conversations yet.</Typography.Text>
          ) : (
            <List
              dataSource={conversations}
              renderItem={(conv) => (
                <List.Item
                  key={conv.playerEmail}
                  onClick={() => setSelectedPlayerEmail(conv.playerEmail)}
                  style={{
                    cursor: "pointer",
                    backgroundColor:
                      selectedPlayerEmail === conv.playerEmail ? "#e6f7ff" : "transparent",
                    padding: 12,
                    borderRadius: 4,
                    marginBottom: 8,
                  }}
                >
                  <List.Item.Meta
                    title={
                      <Space>
                        <span>{conv.playerNickname}</span>
                        {conv.unreadCount > 0 ? (
                          <span
                            style={{
                              backgroundColor: "#ff4d4f",
                              color: "white",
                              borderRadius: 10,
                              padding: "2px 8px",
                              fontSize: 12,
                            }}
                          >
                            {conv.unreadCount}
                          </span>
                        ) : null}
                      </Space>
                    }
                    description={
                      <>
                        <div style={{ fontSize: 12, color: "#666" }}>{conv.playerEmail}</div>
                        <div
                          style={{
                            fontSize: 12,
                            color: "#999",
                            marginTop: 4,
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                          }}
                        >
                          {conv.lastMessage}
                        </div>
                      </>
                    }
                  />
                </List.Item>
              )}
            />
          )}
        </div>

        {/* Chat view (right side) */}
        <div style={{ flex: 1, display: "flex", flexDirection: "column" }}>
          {!selectedPlayerEmail ? (
            <div style={{ textAlign: "center", paddingTop: 100 }}>
              <Typography.Text type="secondary">
                Select a conversation to view messages
              </Typography.Text>
            </div>
          ) : (
            <>
              {/* Chat header */}
              <div style={{ borderBottom: "1px solid #f0f0f0", paddingBottom: 12, marginBottom: 12 }}>
                <Typography.Title level={5} style={{ margin: 0 }}>
                  {selectedConversation?.playerNickname}
                </Typography.Title>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {selectedConversation?.playerEmail}
                </Typography.Text>
              </div>

              {/* Messages */}
              <div
                className={styles.messagesContainer}
                style={{
                  flex: 1,
                  marginBottom: 12,
                  maxHeight: 400,
                }}
              >
                {chatLoading ? (
                  <Typography.Text type="secondary">Loading messages...</Typography.Text>
                ) : chatMessages.length === 0 ? (
                  <Typography.Text type="secondary">No messages yet.</Typography.Text>
                ) : (
                  <>
                    {chatMessages.map((msg) => (
                      <div
                        key={msg.id}
                        className={`${styles.messageWrapper} ${msg.isFromAdmin ? styles.fromAdmin : styles.fromPlayer}`}
                      >
                        <div className={`${styles.messageBubble} ${msg.isFromAdmin ? styles.fromAdmin : styles.fromPlayer}`}>
                          {msg.isFromAdmin && msg.adminNickname ? (
                            <Typography.Text
                              strong
                              style={{
                                fontSize: 12,
                                color: "white",
                                display: "block",
                                marginBottom: 4,
                              }}
                            >
                              {msg.adminNickname} (You)
                            </Typography.Text>
                          ) : null}
                          <Typography.Text
                            style={{ whiteSpace: "pre-wrap", color: msg.isFromAdmin ? "white" : "inherit" }}
                          >
                            {msg.message}
                          </Typography.Text>
                          <div style={{ marginTop: 4 }}>
                            <Typography.Text
                              type="secondary"
                              style={{ fontSize: 11, color: msg.isFromAdmin ? "#ccc" : undefined }}
                            >
                              {new Date(msg.createdAt).toLocaleString()}
                            </Typography.Text>
                          </div>
                        </div>
                      </div>
                    ))}
                    <div ref={messagesEndRef} />
                  </>
                )}
              </div>

              {/* Input */}
              <Space.Compact style={{ width: "100%" }}>
                <Input.TextArea
                  placeholder="Type your reply... (Shift+Enter for new line)"
                  value={chatMessageText}
                  onChange={(e) => setChatMessageText(e.target.value)}
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
                  disabled={!chatMessageText.trim()}
                >
                  Send
                </Button>
              </Space.Compact>
            </>
          )}
        </div>
      </div>
    </Space>
  );
}
