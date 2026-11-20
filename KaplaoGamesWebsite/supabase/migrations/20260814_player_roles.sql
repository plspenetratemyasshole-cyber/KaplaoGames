-- Run in Supabase → SQL Editor (safe to re-run)

create table if not exists kaplao_player_roles (
  email text primary key,
  permission text not null default 'none',
  nickname_preset text not null default 'own',
  updated_at timestamptz not null default now(),
  updated_by text
);

create unique index if not exists kaplao_player_roles_email_lower
  on kaplao_player_roles (lower(email));

alter table if exists kaplao_player_roles disable row level security;

create table if not exists kaplao_nickname_presets (
  name text primary key,
  created_at timestamptz not null default now(),
  created_by text
);

create unique index if not exists kaplao_nickname_presets_name_lower
  on kaplao_nickname_presets (lower(name));

alter table if exists kaplao_nickname_presets disable row level security;

insert into kaplao_player_roles (email, permission, nickname_preset)
select email, 'admin', 'own'
from kaplao_admins
on conflict (email) do update
  set permission = excluded.permission,
      nickname_preset = coalesce(kaplao_player_roles.nickname_preset, excluded.nickname_preset);
