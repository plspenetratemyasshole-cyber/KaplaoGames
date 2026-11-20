-- Verification codes: purpose for email verify vs password reset.
-- Safe to re-run.

alter table if exists kaplao_verification_codes
  add column if not exists purpose text not null default 'verify_email';
