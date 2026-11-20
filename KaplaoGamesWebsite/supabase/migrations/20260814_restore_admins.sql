-- Restore admin flags from kaplao_admins into kaplao_player_roles.
-- Safe to re-run. Does not overwrite an existing nickname_preset.

insert into kaplao_player_roles (email, permission, nickname_preset, updated_by)
select lower(email), 'admin', 'own', 'restore_admins'
from kaplao_admins
on conflict (email) do update
  set permission = 'admin';
