# Kaplao Games Account Website

Ant Design + Next.js account site for Kaplao Games (`kaplao.ru`).

## Auth

- Register → **email verification code** (Resend) → `/verify`
- Login (blocks unverified accounts)
- Forgot / reset password via email code
- `/account` — change nickname
- Unique nicknames in Supabase (or local `data/db.json`)

## Run locally

```bash
cd KaplaoGamesWebsite
copy .env.example .env.local
npm install
npm run dev
```

Open http://localhost:3000

Set in `.env.local` at least `JWT_SECRET`. For real emails: `RESEND_API_KEY` + `EMAIL_FROM`.

## Deploy + domain + Resend

- **Domain kaplao.ru + Resend (подробно, RU):** [DOMAIN_AND_RESEND.md](./DOMAIN_AND_RESEND.md)
- **Vercel / Supabase overview:** [DEPLOY_VERCEL.md](./DEPLOY_VERCEL.md)
- **Admin panel:** [ADMIN.md](./ADMIN.md)

After deploy, set Unity Website URL to `https://kaplao.ru`.
