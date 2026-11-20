-- Developer Chat Messages Table
-- Run this SQL in Supabase SQL Editor

CREATE TABLE IF NOT EXISTS kaplao_dev_messages (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  sender_email TEXT NOT NULL,
  sender_nickname TEXT NOT NULL,
  message TEXT NOT NULL,
  is_from_admin BOOLEAN NOT NULL DEFAULT FALSE,
  admin_email TEXT,
  admin_nickname TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  read_by_admin BOOLEAN NOT NULL DEFAULT FALSE,
  read_by_player BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS idx_dev_messages_sender ON kaplao_dev_messages(sender_email, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_dev_messages_created ON kaplao_dev_messages(created_at DESC);

ALTER TABLE kaplao_dev_messages ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Service role full access" ON kaplao_dev_messages
  FOR ALL TO service_role
  USING (true) WITH CHECK (true);
