-- Independent player rights (multi-select like skins).
-- Safe to re-run.

alter table if exists kaplao_player_roles
  add column if not exists rights jsonb not null default '[]'::jsonb;

-- Migrate legacy single permission → rights[]
update kaplao_player_roles
set rights = case lower(permission)
  when 'admin' then '["level_purge","game_host"]'::jsonb
  when 'level_mod' then '["level_purge"]'::jsonb
  else '[]'::jsonb
end
where coalesce(jsonb_array_length(rights), 0) = 0
  and lower(coalesce(permission, 'none')) <> 'none';

-- Root owner always has full rights when a role row exists.
insert into kaplao_player_roles (email, permission, nickname_preset, rights, updated_at, updated_by)
values (
  'asmoloff55@gmail.com',
  'admin',
  'none',
  '["level_purge","game_host","web_console"]'::jsonb,
  now(),
  'migration'
)
on conflict (email) do update
set
  rights = '["level_purge","game_host","web_console"]'::jsonb,
  permission = 'admin',
  updated_at = now();
