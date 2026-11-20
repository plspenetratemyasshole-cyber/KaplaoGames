-- Run once in Supabase → SQL Editor → New query → Run
-- https://supabase.com/dashboard

create table if not exists kaplao_users (
  id uuid primary key,
  nickname text not null,
  email text not null,
  password_hash text not null,
  verified boolean not null default false,
  created_at timestamptz not null default now()
);

create unique index if not exists kaplao_users_nickname_lower
  on kaplao_users (lower(nickname));

create unique index if not exists kaplao_users_email_lower
  on kaplao_users (lower(email));

create table if not exists kaplao_verification_codes (
  email text primary key,
  code text not null,
  expires_at timestamptz not null,
  purpose text not null default 'verify_email'
);

alter table if exists kaplao_verification_codes
  add column if not exists purpose text not null default 'verify_email';


create table if not exists kaplao_device_sessions (
  code text primary key,
  status text not null,
  expires_at timestamptz not null,
  token text,
  nickname text,
  email text
);

-- IMPORTANT: service writes must stick. Disable RLS on auth helper tables.
alter table if exists kaplao_device_sessions disable row level security;
alter table if exists kaplao_verification_codes disable row level security;
-- users table: keep writable for service role; disable RLS if login/register fails oddly
alter table if exists kaplao_users disable row level security;

-- Game admins (server-verified privileges). Super-admin panel owner is hardcoded in app code.
create table if not exists kaplao_admins (
  email text primary key,
  created_at timestamptz not null default now(),
  created_by text
);

create unique index if not exists kaplao_admins_email_lower
  on kaplao_admins (lower(email));

alter table if exists kaplao_admins disable row level security;

-- Per-player rights + nickname preset (style JSON lives in kaplao_nickname_presets).
create table if not exists kaplao_player_roles (
  email text primary key,
  permission text not null default 'none',
  rights jsonb not null default '[]'::jsonb,
  nickname_preset text not null default 'none',
  updated_at timestamptz not null default now(),
  updated_by text
);

create unique index if not exists kaplao_player_roles_email_lower
  on kaplao_player_roles (lower(email));

alter table if exists kaplao_player_roles disable row level security;

alter table if exists kaplao_player_roles
  add column if not exists rights jsonb not null default '[]'::jsonb;

-- Custom nickname presets with full Easy Text Effects style JSON (built-in: none).
create table if not exists kaplao_nickname_presets (
  name text primary key,
  created_at timestamptz not null default now(),
  created_by text,
  style jsonb not null default '{}'::jsonb
);

create unique index if not exists kaplao_nickname_presets_name_lower
  on kaplao_nickname_presets (lower(name));

alter table if exists kaplao_nickname_presets disable row level security;

alter table if exists kaplao_nickname_presets
  add column if not exists style jsonb not null default '{}'::jsonb;

-- Player action logs for admin panel (stored in Supabase; file fallback in data/player-logs.json).
create table if not exists kaplao_player_logs (
  id uuid primary key,
  action text not null,
  created_at timestamptz not null default now(),
  nickname text not null default '',
  email text not null default '',
  level_title text,
  actor_nickname text,
  actor_email text,
  metadata jsonb
);

create index if not exists kaplao_player_logs_created_at
  on kaplao_player_logs (created_at desc);

alter table if exists kaplao_player_logs disable row level security;

create table if not exists kaplao_player_log_settings (
  id text primary key,
  auto_delete text not null default 'none',
  updated_at timestamptz not null default now(),
  updated_by text
);

insert into kaplao_player_log_settings (id, auto_delete)
values ('default', 'none')
on conflict (id) do nothing;

alter table if exists kaplao_player_log_settings disable row level security;

-- Admin-granted runner skins (per player, independent of admin/mod role).
create table if not exists kaplao_player_skins (
  email text not null,
  skin_id text not null,
  granted_at timestamptz not null default now(),
  granted_by text,
  primary key (email, skin_id)
);

create index if not exists kaplao_player_skins_email_lower
  on kaplao_player_skins (lower(email));

alter table if exists kaplao_player_skins disable row level security;

-- One-time migration helper (safe to re-run):
insert into kaplao_player_roles (email, permission, nickname_preset)
select email, 'admin', 'own'
from kaplao_admins
on conflict (email) do update
  set permission = excluded.permission,
      nickname_preset = coalesce(kaplao_player_roles.nickname_preset, excluded.nickname_preset);
