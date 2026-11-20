"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Alert,
  Button,
  Card,
  Form,
  Input,
  List,
  Popconfirm,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
  message,
} from "antd";
import type { ColumnsType } from "antd/es/table";
import { Suspense, useCallback, useEffect, useMemo, useState } from "react";
import MobileNav from "@/components/MobileNav";
import {
  LOG_AUTO_DELETE_OPTIONS,
  type LogAutoDeletePolicy,
  type PlayerLogAction,
} from "@/lib/player-log-types";
import { ADMIN_SKIN_IDS, ADMIN_SKIN_LABELS, type AdminSkinId } from "@/lib/runner-skins";
import {
  EMPTY_NICKNAME_STYLE,
  type NicknamePresetStyle,
} from "@/lib/nickname-preset-style";
import {
  PLAYER_RIGHT_OPTIONS,
  type PlayerRight,
} from "@/lib/player-roles";
import { NicknamePresetStyleEditor } from "./NicknamePresetStyleEditor";
import { DevChatTab } from "./DevChatTab";

type PlayerRow = {
  id: string;
  nickname: string;
  email: string;
  verified: boolean;
  createdAt: string;
  permission: "admin" | "level_mod" | "none";
  rights: PlayerRight[];
  nicknamePreset: string;
  grantedSkinIds: AdminSkinId[];
};

type PresetRecord = {
  name: string;
  createdAt: string;
  createdBy?: string;
  style: NicknamePresetStyle;
};

type LogRow = {
  id: string;
  action: PlayerLogAction;
  createdAt: string;
  nickname: string;
  email: string;
  levelTitle?: string;
  actorNickname?: string;
  actorEmail?: string;
};

type DevChatConversation = {
  playerEmail: string;
  playerNickname: string;
  lastMessage: string;
  lastMessageAt: string;
  unreadCount: number;
  isLastFromAdmin: boolean;
};

const ACTION_LABELS: Record<PlayerLogAction, string> = {
  account_login: "Login",
  account_logout: "Logout",
  account_created: "Account created",
  account_deleted: "Account deleted",
  singleplayer_world_enter: "Singleplayer world",
  server_join: "Server join",
  level_published: "Level published",
  level_deleted: "Level deleted",
};

function actionTag(action: string | undefined) {
  const key = (action || "").trim() as PlayerLogAction;
  const colors: Partial<Record<PlayerLogAction, string>> = {
    account_login: "green",
    account_logout: "default",
    account_created: "cyan",
    account_deleted: "volcano",
    singleplayer_world_enter: "blue",
    server_join: "geekblue",
    level_published: "purple",
    level_deleted: "red",
  };
  const label = ACTION_LABELS[key] || key || "unknown";
  return <Tag color={colors[key]}>{label}</Tag>;
}

function normalizeLogRow(raw: Record<string, unknown>): LogRow {
  return {
    id: String(raw.id || ""),
    action: String(raw.action || "unknown") as PlayerLogAction,
    createdAt: String(raw.createdAt || raw.created_at || ""),
    nickname: String(raw.nickname || ""),
    email: String(raw.email || ""),
    levelTitle: raw.levelTitle
      ? String(raw.levelTitle)
      : raw.level_title
        ? String(raw.level_title)
        : undefined,
    actorNickname: raw.actorNickname
      ? String(raw.actorNickname)
      : raw.actor_nickname
        ? String(raw.actor_nickname)
        : undefined,
    actorEmail: raw.actorEmail
      ? String(raw.actorEmail)
      : raw.actor_email
        ? String(raw.actor_email)
        : undefined,
  };
}

const ADMIN_SKIN_OPTIONS = ADMIN_SKIN_IDS.map((id) => ({
  value: id,
  label: ADMIN_SKIN_LABELS[id],
}));

const RIGHT_OPTIONS = PLAYER_RIGHT_OPTIONS.map((item) => ({
  value: item.value,
  label: item.label,
}));

function authHeaders(token: string) {
  return {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  };
}

function rightsTags(rights: PlayerRight[]) {
  if (!rights?.length) return <Tag>None</Tag>;
  return (
    <Space size={[4, 4]} wrap>
      {rights.map((right) => {
        const meta = PLAYER_RIGHT_OPTIONS.find((o) => o.value === right);
        const color =
          right === "web_console" ? "gold" : right === "game_host" ? "red" : "blue";
        return (
          <Tag key={right} color={color}>
            {meta?.label || right}
          </Tag>
        );
      })}
    </Space>
  );
}

function AdminPanel() {
  const router = useRouter();
  const [token, setToken] = useState("");
  const [adminNickname, setAdminNickname] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [superAdminEmail] = useState("asmoloff55@gmail.com");
  const [players, setPlayers] = useState<PlayerRow[]>([]);
  const [presetNames, setPresetNames] = useState<string[]>(["none"]);
  const [customPresets, setCustomPresets] = useState<PresetRecord[]>([]);
  const [newPresetName, setNewPresetName] = useState("");
  const [editingPresetName, setEditingPresetName] = useState<string | null>(null);
  const [editingStyle, setEditingStyle] = useState<NicknamePresetStyle>(EMPTY_NICKNAME_STYLE);
  const [drafts, setDrafts] = useState<
    Record<
      string,
      {
        rights: PlayerRight[];
        nicknamePreset: string;
        grantedSkinIds: AdminSkinId[];
      }
    >
  >({});
  const [logs, setLogs] = useState<LogRow[]>([]);
  const [logsLoading, setLogsLoading] = useState(false);
  const [autoDelete, setAutoDelete] = useState<LogAutoDeletePolicy>("none");
  const [conversations, setConversations] = useState<DevChatConversation[]>([]);

  const loadPlayers = useCallback(async (authToken: string) => {
    const response = await fetch("/api/admin/players", {
      headers: authHeaders(authToken),
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(data.error || "Failed to load players");
    }
    const rows: PlayerRow[] = data.players || [];
    setPlayers(rows);
    setPresetNames(data.presetNames || ["none"]);
    const nextDrafts: Record<
      string,
      {
        rights: PlayerRight[];
        nicknamePreset: string;
        grantedSkinIds: AdminSkinId[];
      }
    > = {};
    for (const row of rows) {
      nextDrafts[row.email] = {
        rights: row.rights ?? [],
        nicknamePreset: row.nicknamePreset,
        grantedSkinIds: row.grantedSkinIds ?? [],
      };
    }
    setDrafts(nextDrafts);
  }, []);

  const loadPresets = useCallback(async (authToken: string) => {
    const response = await fetch("/api/admin/nickname-presets", {
      headers: authHeaders(authToken),
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(data.error || "Failed to load presets");
    }
    setCustomPresets(
      (data.customPresets || []).map((preset: PresetRecord) => ({
        ...preset,
        style: preset.style || EMPTY_NICKNAME_STYLE,
      }))
    );
    setPresetNames(data.presetNames || ["none"]);
  }, []);

  const loadLogs = useCallback(async (authToken: string) => {
    setLogsLoading(true);
    try {
      const response = await fetch("/api/admin/logs", {
        headers: authHeaders(authToken),
        cache: "no-store",
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(data.error || "Failed to load logs");
      }
      setLogs((data.logs || []).map((row: Record<string, unknown>) => normalizeLogRow(row)));
      setAutoDelete(data.settings?.autoDelete || "none");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load logs");
    } finally {
      setLogsLoading(false);
    }
  }, []);

  const loadAll = useCallback(
    async (authToken: string) => {
      await Promise.all([loadPlayers(authToken), loadPresets(authToken), loadLogs(authToken)]);
    },
    [loadPlayers, loadPresets, loadLogs]
  );

  useEffect(() => {
    const saved = localStorage.getItem("kaplao.admin.token") || "";

    // If there's an active account session, it takes priority.
    // A non-admin account must never sneak in via a stale admin token.
    const accountToken = localStorage.getItem("kaplao.account.token") || "";
    if (accountToken) {
      (async () => {
        try {
          const meResponse = await fetch("/api/me", { headers: authHeaders(accountToken) });
          const me = await meResponse.json().catch(() => ({}));
          if (meResponse.ok && !me.isSuperAdmin) {
            // Logged-in account has no admin rights — wipe any stale admin token and block
            localStorage.removeItem("kaplao.admin.token");
            setError(
              `Your account (${me.email || "unknown"}) does not have admin rights. Sign out first, then log in with the admin account.`
            );
            setLoading(false);
            return;
          }
          // Account has admin rights — proceed with it as the admin token
          if (meResponse.ok && me.isSuperAdmin) {
            const effectiveToken = accountToken;
            localStorage.setItem("kaplao.admin.token", effectiveToken);
            setToken(effectiveToken);
            setAdminNickname(me.nickname || me.email || "Admin");
            await loadAll(effectiveToken);
            setLoading(false);
            return;
          }
          // /api/me failed (expired account token) — fall through to admin token check below
        } catch {
          // network error — fall through
        }

        // Account token check failed, fall through to admin token logic
        proceedWithAdminToken(saved);
      })();
      return;
    }

    proceedWithAdminToken(saved);

    async function proceedWithAdminToken(adminToken: string) {
      if (!adminToken) {
        // No token - show built-in login form (no redirect, so /admin always stays on /admin)
        setLoading(false);
        return;
      }

      setToken(adminToken);
      try {
        const meResponse = await fetch("/api/me", { headers: authHeaders(adminToken) });
        const me = await meResponse.json().catch(() => ({}));
        if (!meResponse.ok) {
          localStorage.removeItem("kaplao.admin.token");
          setToken("");
          setError(me.error || `Session check failed (${meResponse.status}). Redeploy the site or run Supabase SQL migration.`);
          setLoading(false);
          // Show built-in login form, don't redirect away from /admin
          return;
        }

        // CHECK ADMIN RIGHTS BEFORE LOADING ANY DATA
        if (!me.isSuperAdmin) {
          localStorage.removeItem("kaplao.admin.token");
          setToken("");
          setError(
            `Your account (${me.email || "unknown"}) does not have admin rights. Contact the site administrator to grant Web Console permission.`
          );
          setLoading(false);
          // DO NOT REDIRECT - just show error
          return;
        }

        // Only load data if user has admin rights
        setAdminNickname(me.nickname || me.email || "Admin");
        await loadAll(adminToken);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load admin panel");
      } finally {
        setLoading(false);
      }
    }
  }, [loadAll, router]);

  async function onLogin(values: { login: string; password: string }) {
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const data = await response.json();
      if (!response.ok) {
        setError(data.error || "Login failed");
        return;
      }

      const meResponse = await fetch("/api/me", {
        headers: authHeaders(data.token),
      });
      const me = await meResponse.json().catch(() => ({}));
      if (!meResponse.ok) {
        setError(me.error || `Session check failed (${meResponse.status}). Redeploy the site or run Supabase SQL migration.`);
        return;
      }
      if (!me.isSuperAdmin) {
        setError(
          `Your account (${me.email || values.login}) does not have admin rights. Contact the site administrator to grant Web Console permission.`
        );
        return;
      }

      localStorage.setItem("kaplao.admin.token", data.token);
      localStorage.setItem("kaplao.account.token", data.token);
      setToken(data.token);
      setAdminNickname(me.nickname || me.email || "Admin");
      await loadAll(data.token);
      message.success("Signed in");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setSaving(false);
    }
  }

  async function onSavePlayer(email: string) {
    if (!token) return;
    const draft = drafts[email];
    if (!draft) return;

    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/admin/players", {
        method: "PUT",
        headers: authHeaders(token),
        body: JSON.stringify({
          email,
          rights: draft.rights,
          nicknamePreset: draft.nicknamePreset,
          grantedSkinIds: draft.grantedSkinIds,
        }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to save player");
        return;
      }
      await loadPlayers(token);
      message.success(`Saved ${email}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save player");
    } finally {
      setSaving(false);
    }
  }

  async function onAddPreset() {
    if (!token || !newPresetName.trim()) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/admin/nickname-presets", {
        method: "POST",
        headers: authHeaders(token),
        body: JSON.stringify({
          name: newPresetName.trim(),
          style: {
            ...EMPTY_NICKNAME_STYLE,
            effects: [],
          },
        }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to add preset");
        return;
      }
      setNewPresetName("");
      await loadAll(token);
      const created = data.preset as PresetRecord | undefined;
      if (created?.name) {
        setEditingPresetName(created.name);
        setEditingStyle(created.style || EMPTY_NICKNAME_STYLE);
      }
      message.success("Preset added — configure effects below");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add preset");
    } finally {
      setSaving(false);
    }
  }

  async function onSavePresetStyle() {
    if (!token || !editingPresetName) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/admin/nickname-presets", {
        method: "PUT",
        headers: authHeaders(token),
        body: JSON.stringify({
          name: editingPresetName,
          style: editingStyle,
        }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to save preset style");
        return;
      }
      await loadPresets(token);
      message.success(`Saved style for ${editingPresetName}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save preset style");
    } finally {
      setSaving(false);
    }
  }

  async function onRemovePreset(name: string) {
    if (!token) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/admin/nickname-presets", {
        method: "DELETE",
        headers: authHeaders(token),
        body: JSON.stringify({ name }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to remove preset");
        return;
      }
      if (editingPresetName === name) {
        setEditingPresetName(null);
        setEditingStyle(EMPTY_NICKNAME_STYLE);
      }
      await loadAll(token);
      message.success("Preset removed");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove preset");
    } finally {
      setSaving(false);
    }
  }

  async function onDeletePlayer(email: string) {
    if (!token) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/admin/players", {
        method: "DELETE",
        headers: authHeaders(token),
        body: JSON.stringify({ email }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to delete account");
        return;
      }
      await loadPlayers(token);
      message.success(`Deleted ${email}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete account");
    } finally {
      setSaving(false);
    }
  }

  async function onDeleteLog(id: string) {
    if (!token) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/admin/logs", {
        method: "DELETE",
        headers: authHeaders(token),
        body: JSON.stringify({ id }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to delete log");
        return;
      }
      await loadLogs(token);
      message.success("Log deleted");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete log");
    } finally {
      setSaving(false);
    }
  }

  async function onSaveAutoDelete(value: LogAutoDeletePolicy) {
    if (!token) return;
    setSaving(true);
    setError("");
    try {
      const response = await fetch("/api/admin/logs", {
        method: "PUT",
        headers: authHeaders(token),
        body: JSON.stringify({ autoDelete: value }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        setError(data.error || "Failed to save auto-delete setting");
        return;
      }
      setAutoDelete(data.settings?.autoDelete || value);
      message.success("Auto-delete setting saved");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save auto-delete setting");
    } finally {
      setSaving(false);
    }
  }

  const presetOptions = useMemo(
    () => presetNames.map((name) => ({ value: name, label: name })),
    [presetNames]
  );

  const playerColumns: ColumnsType<PlayerRow> = [
    {
      title: "Email",
      dataIndex: "email",
      key: "email",
      width: 240,
      ellipsis: true,
    },
    {
      title: "Nickname",
      dataIndex: "nickname",
      key: "nickname",
      width: 140,
    },
    {
      title: "Verified",
      dataIndex: "verified",
      key: "verified",
      width: 90,
      render: (verified: boolean) =>
        verified ? <Tag color="green">Yes</Tag> : <Tag color="default">No</Tag>,
    },
    {
      title: "Rights",
      key: "rights",
      width: 260,
      render: (_, row) => (
        <Select
          mode="multiple"
          size="small"
          style={{ width: 240 }}
          placeholder="None"
          value={drafts[row.email]?.rights ?? row.rights ?? []}
          options={RIGHT_OPTIONS}
          optionRender={(option) => {
            const meta = PLAYER_RIGHT_OPTIONS.find((o) => o.value === option.value);
            return (
              <div>
                <div>{meta?.label || String(option.value)}</div>
                <div style={{ fontSize: 11, opacity: 0.65 }}>{meta?.description}</div>
              </div>
            );
          }}
          onChange={(value) =>
            setDrafts((prev) => ({
              ...prev,
              [row.email]: {
                rights: value as PlayerRight[],
                nicknamePreset: prev[row.email]?.nicknamePreset ?? row.nicknamePreset,
                grantedSkinIds: prev[row.email]?.grantedSkinIds ?? row.grantedSkinIds ?? [],
              },
            }))
          }
        />
      ),
    },
    {
      title: "Nick preset",
      key: "nicknamePreset",
      width: 140,
      render: (_, row) => (
        <Select
          size="small"
          style={{ width: 120 }}
          value={drafts[row.email]?.nicknamePreset ?? row.nicknamePreset}
          options={presetOptions}
          onChange={(value) =>
            setDrafts((prev) => ({
              ...prev,
              [row.email]: {
                rights: prev[row.email]?.rights ?? row.rights ?? [],
                nicknamePreset: value,
                grantedSkinIds: prev[row.email]?.grantedSkinIds ?? row.grantedSkinIds ?? [],
              },
            }))
          }
        />
      ),
    },
    {
      title: "Monster skins",
      key: "grantedSkinIds",
      width: 220,
      render: (_, row) => (
        <Select
          mode="multiple"
          size="small"
          style={{ width: 200 }}
          placeholder="None"
          value={drafts[row.email]?.grantedSkinIds ?? row.grantedSkinIds ?? []}
          options={ADMIN_SKIN_OPTIONS}
          onChange={(value) =>
            setDrafts((prev) => ({
              ...prev,
              [row.email]: {
                rights: prev[row.email]?.rights ?? row.rights ?? [],
                nicknamePreset: prev[row.email]?.nicknamePreset ?? row.nicknamePreset,
                grantedSkinIds: value as AdminSkinId[],
              },
            }))
          }
        />
      ),
    },
    {
      title: "Current",
      key: "current",
      width: 180,
      render: (_, row) => (
        <Space direction="vertical" size={0}>
          {rightsTags(row.rights ?? [])}
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {row.nicknamePreset}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: "",
      key: "actions",
      width: 170,
      render: (_, row) => {
        const draft = drafts[row.email];
        const dirty =
          draft &&
          (JSON.stringify(draft.rights ?? []) !== JSON.stringify(row.rights ?? []) ||
            draft.nicknamePreset !== row.nicknamePreset ||
            JSON.stringify(draft.grantedSkinIds ?? []) !==
              JSON.stringify(row.grantedSkinIds ?? []));
        const isProtected =
          row.email.toLowerCase() === superAdminEmail.toLowerCase();
        return (
          <Space size="small">
            <Button
              type="primary"
              size="small"
              disabled={!dirty}
              loading={saving}
              onClick={() => onSavePlayer(row.email)}
            >
              Save
            </Button>
            {!isProtected ? (
              <Popconfirm
                title={`Delete account ${row.email}?`}
                description="This permanently removes the account, roles, and login data."
                okText="Delete"
                okButtonProps={{ danger: true }}
                onConfirm={() => onDeletePlayer(row.email)}
              >
                <Button danger size="small" loading={saving}>
                  Delete
                </Button>
              </Popconfirm>
            ) : null}
          </Space>
        );
      },
    },
  ];

  const logColumns: ColumnsType<LogRow> = [
    {
      title: "When",
      dataIndex: "createdAt",
      key: "createdAt",
      width: 170,
      render: (value: string) => new Date(value).toLocaleString(),
    },
    {
      title: "Action",
      key: "action",
      width: 150,
      render: (_, row) => actionTag(row.action),
    },
    {
      title: "Player",
      key: "player",
      width: 200,
      render: (_, row) => (
        <Space direction="vertical" size={0}>
          <Typography.Text>{row.nickname || "—"}</Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {row.email || "—"}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: "Level",
      dataIndex: "levelTitle",
      key: "levelTitle",
      width: 160,
      ellipsis: true,
      render: (value?: string) => value || "—",
    },
    {
      title: "Actor",
      key: "actor",
      width: 180,
      ellipsis: true,
      render: (_, row) => {
        if (!row.actorEmail && !row.actorNickname) return "—";
        return (
          <Space direction="vertical" size={0}>
            <Typography.Text>{row.actorNickname || "—"}</Typography.Text>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.actorEmail || "—"}
            </Typography.Text>
          </Space>
        );
      },
    },
    {
      title: "",
      key: "actions",
      width: 90,
      render: (_, row) => (
        <Popconfirm
          title="Delete this log entry?"
          okText="Delete"
          okButtonProps={{ danger: true }}
          onConfirm={() => onDeleteLog(row.id)}
        >
          <Button danger size="small" loading={saving}>
            Delete
          </Button>
        </Popconfirm>
      ),
    },
  ];

  if (loading) {
    return (
      <>
        <MobileNav isSignedIn={false} />
        <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
          <Card style={{ width: 960, maxWidth: "96vw" }}>Loading…</Card>
        </main>
      </>
    );
  }

  // Show login form if no token OR if there's an error (e.g. no admin rights)
  if (!token || error) {
    return (
      <>
        <MobileNav isSignedIn={false} />
        <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
          <Card style={{ width: 420, maxWidth: "92vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
        <Typography.Title level={2} style={{ marginTop: 0 }}>
          Admin panel
        </Typography.Title>
        <Typography.Paragraph type="secondary">
          Sign in with the super admin account to manage players and roles.
        </Typography.Paragraph>
        {error ? <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Form layout="vertical" onFinish={onLogin} requiredMark={false}>
          <Form.Item label="Email" name="login" rules={[{ required: true, message: "Enter email" }]}>
            <Input size="large" placeholder="Email" />
          </Form.Item>
          <Form.Item
            label="Password"
            name="password"
            rules={[{ required: true, message: "Enter password" }]}
          >
            <Input.Password size="large" />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block loading={saving}>
            Sign in
          </Button>
        </Form>
        <Typography.Paragraph style={{ marginTop: 20, marginBottom: 0, textAlign: "center" }}>
          <Link href="/">Back to site</Link>
        </Typography.Paragraph>
      </Card>
        </main>
      </>
    );
  }

  return (
    <>
      <MobileNav 
        isSignedIn={true} 
        nickname={adminNickname}
        isSuperAdmin={true}
      />
      <main style={{ minHeight: "100vh", padding: 24, display: "flex", justifyContent: "center" }}>
        <Card style={{ width: 1100, maxWidth: "96vw", boxShadow: "0 12px 40px rgba(20,40,80,0.12)" }}>
      <Space direction="vertical" size="large" style={{ width: "100%" }}>
        <div>
          <Typography.Title level={2} style={{ marginTop: 0, marginBottom: 8 }}>
            Kaplao admin
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            Super admin root: {superAdminEmail} (always has Web Console). Assign independent rights
            and nickname presets. Rights work like skins — pick any combination per player.
          </Typography.Paragraph>
        </div>

        {error ? <Alert type="error" message={error} showIcon closable onClose={() => setError("")} /> : null}

        <Tabs
          items={[
            {
              key: "players",
              label: `Players (${players.length})`,
              children: (
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                  <Alert
                    type="warning"
                    showIcon
                    message="Rights"
                    description={
                      <>
                        <b>Level Purge</b> — delete levels from public servers. <b>Game Host</b> —
                        permanent in-game admin powers (like the host). <b>Web Console</b> — access
                        this website admin panel. Assign any combination. Changes apply after they
                        rejoin a game (Web Console applies on next login here).
                      </>
                    }
                  />
                  <Table
                    rowKey="email"
                    size="small"
                    loading={loading}
                    dataSource={players}
                    columns={playerColumns}
                    pagination={{ pageSize: 20, showSizeChanger: true }}
                    scroll={{ x: 900 }}
                  />
                </Space>
              ),
            },
            {
              key: "presets",
              label: "Nickname presets",
              children: (
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                  <Alert
                    type="info"
                    showIcon
                    message="Full nickname customization"
                    description={
                      <>
                        Built-in <Tag>none</Tag> is plain text (no effects). Create a custom preset,
                        then edit font name (string matching Unity registry), base hex color, and as
                        many Easy Text Effects as you want (gradient, move, rotate, scale, composite,
                        per-vertex).
                      </>
                    }
                  />

                  <Space.Compact style={{ width: "100%", maxWidth: 420 }}>
                    <Input
                      placeholder="new_preset_name"
                      value={newPresetName}
                      onChange={(e) => setNewPresetName(e.target.value)}
                      onPressEnter={onAddPreset}
                    />
                    <Button type="primary" loading={saving} onClick={onAddPreset}>
                      Add preset
                    </Button>
                  </Space.Compact>

                  <List
                    bordered
                    header="Custom presets"
                    locale={{ emptyText: "No custom presets yet." }}
                    dataSource={customPresets}
                    renderItem={(item) => (
                      <List.Item
                        actions={[
                          <Button
                            key="edit"
                            size="small"
                            type={editingPresetName === item.name ? "primary" : "default"}
                            onClick={() => {
                              setEditingPresetName(item.name);
                              setEditingStyle(item.style || EMPTY_NICKNAME_STYLE);
                            }}
                          >
                            Edit style
                          </Button>,
                          <Popconfirm
                            key="remove"
                            title={`Remove preset "${item.name}"?`}
                            onConfirm={() => onRemovePreset(item.name)}
                          >
                            <Button danger size="small" loading={saving}>
                              Remove
                            </Button>
                          </Popconfirm>,
                        ]}
                      >
                        <List.Item.Meta
                          title={<Tag>{item.name}</Tag>}
                          description={
                            <>
                              {item.style?.fontName ? `font: ${item.style.fontName} · ` : ""}
                              {(item.style?.effects?.length || 0)} effect(s)
                              {item.createdBy
                                ? ` · ${item.createdBy} · ${new Date(item.createdAt).toLocaleString()}`
                                : ` · ${new Date(item.createdAt).toLocaleString()}`}
                            </>
                          }
                        />
                      </List.Item>
                    )}
                  />

                  {editingPresetName ? (
                    <Card
                      title={`Edit preset: ${editingPresetName}`}
                      extra={
                        <Button type="primary" loading={saving} onClick={onSavePresetStyle}>
                          Save style
                        </Button>
                      }
                    >
                      <NicknamePresetStyleEditor value={editingStyle} onChange={setEditingStyle} />
                    </Card>
                  ) : null}

                  <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    All available presets: {presetNames.join(", ")}
                  </Typography.Paragraph>
                </Space>
              ),
            },
            {
              key: "logs",
              label: `Logs (${logs.length})`,
              children: (
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                  <Alert
                    type="info"
                    showIcon
                    message="Player action logs"
                    description="Login, logout, world enter, server join, level publish/delete. Stored on Timeweb (Supabase or local JSON fallback)."
                  />
                  <Space wrap align="center">
                    <Typography.Text>Auto-delete logs older than:</Typography.Text>
                    <Select
                      style={{ minWidth: 180 }}
                      value={autoDelete}
                      options={LOG_AUTO_DELETE_OPTIONS}
                      onChange={onSaveAutoDelete}
                      loading={saving}
                    />
                    <Button onClick={() => token && loadLogs(token)} loading={logsLoading}>
                      Refresh
                    </Button>
                  </Space>
                  <Table
                    rowKey="id"
                    size="small"
                    loading={logsLoading}
                    dataSource={logs}
                    columns={logColumns}
                    pagination={{ pageSize: 25, showSizeChanger: true }}
                    scroll={{ x: 900 }}
                  />
                </Space>
              ),
            },
            {
              key: "devchat",
              label: `Dev Chat${conversations.filter(c => c.unreadCount > 0).length > 0 ? ` (${conversations.filter(c => c.unreadCount > 0).length} unread)` : ""}`,
              children: <DevChatTab token={token} adminNickname={adminNickname} onConversationsChange={setConversations} />,
            },
          ]}
        />

        <Space>
          <Button onClick={() => router.push("/")}>Back to site</Button>
        </Space>
      </Space>
        </Card>
      </main>
    </>
  );
}

export default function AdminPage() {
  return (
    <Suspense fallback={
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
          <Card style={{ width: 960 }}>Loading…</Card>
        </main>
      </>
    }>
      <AdminPanel />
    </Suspense>
  );
}
