# Dev Chat Database Migration

## Applying the Migration

### Option 1: Supabase Dashboard (Recommended)

1. Open your Supabase project dashboard
2. Go to **SQL Editor**
3. Click **New Query**
4. Copy the contents of `create_dev_messages_table.sql`
5. Paste into the query editor
6. Click **Run** or press `Ctrl+Enter`

### Option 2: Supabase CLI

```bash
# Navigate to project root
cd KaplaoGamesWebsite

# Apply migration
supabase migration new dev_chat_messages
# Copy the contents of create_dev_messages_table.sql to the new migration file
supabase db push
```

### Verification

After applying the migration, verify the table was created:

```sql
-- Run this in Supabase SQL Editor
SELECT * FROM kaplao_dev_messages LIMIT 1;
```

You should see column headers without errors, even if no rows exist yet.

## What This Creates

- **Table**: `kaplao_dev_messages` - stores all dev chat messages
- **Indexes**: 
  - Fast lookup by sender email (player conversations)
  - Fast lookup by created_at (admin panel conversation list)
  - Fast unread count queries
- **RLS Policies**: Service role has full access (auth handled in API layer)

## Rollback

If you need to remove the table:

```sql
DROP TABLE IF EXISTS kaplao_dev_messages CASCADE;
```

⚠️ This will delete all dev chat messages permanently.
