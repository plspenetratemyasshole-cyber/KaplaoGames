-- Nickname preset full style JSON (Easy Text Effects config from admin).
-- Safe to re-run.

alter table if exists kaplao_nickname_presets
  add column if not exists style jsonb not null default '{}'::jsonb;

alter table if exists kaplao_player_roles
  alter column nickname_preset set default 'none';

update kaplao_player_roles
set nickname_preset = 'none'
where lower(nickname_preset) in ('own', '');

-- Keep legacy named presets assignable until reconfigured in admin.
insert into kaplao_nickname_presets (name, created_at, created_by, style)
values
  ('mod', now(), 'migration', '{"fontName":"","baseColor":"#FFFFFF","effects":[]}'::jsonb),
  ('creator', now(), 'migration', '{"fontName":"","baseColor":"#FFFFFF","effects":[]}'::jsonb)
on conflict (name) do nothing;
