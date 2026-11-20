-- Track publisher email so authors can delete their own public levels.
alter table if exists public.levels
  add column if not exists author_email text;

create index if not exists levels_author_email_idx
  on public.levels (lower(author_email));
