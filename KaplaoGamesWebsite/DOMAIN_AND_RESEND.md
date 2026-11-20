# Kaplao.ru + Resend — подробная инструкция

Сайт: Next.js в папке `KaplaoGamesWebsite`, хостинг **Vercel**.  
Почта: **Resend** (коды верификации и сброса пароля).  
Домен: **kaplao.ru**.

Сделай по порядку: сначала Resend (хотя бы API key), потом домен на Vercel, потом DNS у регистратора, потом верификация домена в Resend.

---

## Часть A. Resend — отправка кодов на почту

### A1. Создай аккаунт Resend

1. Открой https://resend.com → Sign up.  
2. Подтверди свой email аккаунта Resend.

### A2. API Key

1. https://resend.com/api-keys  
2. **Create API Key**  
   - Name: `kaplao-vercel`  
   - Permission: **Sending access** (достаточно)  
3. Скопируй ключ (`re_...`) — он показывается **один раз**.  
4. Это значение для Vercel: `RESEND_API_KEY`.

### A3. Пока домен ещё не привязан (тестовый From)

Можно временно слать так:

```
EMAIL_FROM=Kaplao <onboarding@resend.dev>
```

**Ограничение Resend:** с `onboarding@resend.dev` письма уходят **только на email твоего аккаунта Resend**.  
Для проверки «на себя» этого хватает. Для реальных игроков нужен свой домен (см. A5).

Если `RESEND_API_KEY` не задан, сайт всё равно работает: код пишется в логи сервера и показывается на странице verify/reset как **preview code** (только для отладки).

### A4. Переменные в Vercel (почта)

Vercel → твой проект сайта → **Settings → Environment Variables** → добавь:

| Name | Value | Environments |
|------|--------|----------------|
| `RESEND_API_KEY` | `re_...` | Production (и Preview по желанию) |
| `EMAIL_FROM` | сначала `Kaplao <onboarding@resend.dev>` | Production |
| `NEXT_PUBLIC_APP_URL` | `https://kaplao.ru` | Production |

После добавления переменных: **Deployments → … на последнем деплое → Redeploy**.

### A5. Привязать домен kaplao.ru к Resend (чтобы слать кому угодно)

1. https://resend.com/domains → **Add Domain** → `kaplao.ru`  
2. Resend покажет DNS-записи (обычно):
   - **DKIM** — TXT (имя вроде `resend._domainkey`)
   - иногда **SPF** / MX для inbound — добавь то, что просит панель Resend  
3. Зайди в панель DNS у регистратора домена (где куплен `kaplao.ru`).  
4. Создай **точно те записи**, которые показал Resend (тип, имя/host, value).  
5. Подожди 5–60 минут (иногда до 24–48 ч).  
6. В Resend нажми **Verify** / дождись статуса **Verified**.  
7. Когда домен Verified, смени From на свой ящик:

```
EMAIL_FROM=Kaplao <noreply@kaplao.ru>
```

(Можно `hello@kaplao.ru`, `auth@kaplao.ru` — главное, домен verified.)

8. Обнови `EMAIL_FROM` в Vercel → Redeploy.  
9. Зарегистрируй тестовый аккаунт на **чужой** почте (Gmail и т.д.) и проверь письмо с кодом.

### A6. Проверка, что письма работают

1. Открой `https://kaplao.ru/register`  
2. Зарегистрируйся.  
3. Должен прийти код на почту.  
4. Открой `https://kaplao.ru/verify` (или редирект сам) → введи код.  
5. Логин без верификации должен отказывать и вести на verify.

**Forgot password:** `/forgot-password` → код на почту → `/reset-password`.

Если письма нет:

- Spam / Промоакции  
- Домен Resend не Verified, а From уже `@kaplao.ru`  
- Неверный `RESEND_API_KEY`  
- Vercel logs: Deployments → Functions / Runtime Logs → ищи `[mail]`

---

## Часть B. Привязать Vercel к домену kaplao.ru

### B1. Убедись, что сайт уже на Vercel

Проект должен быть задеплоен (из GitHub папки сайта или monorepo root = `KaplaoGamesWebsite`).  
Сейчас обычно есть URL вида `https://something.vercel.app`.

### B2. Добавь домен в Vercel

1. https://vercel.com/dashboard → проект сайта Kaplao.  
2. **Settings → Domains**.  
3. **Add** → введи:
   - `kaplao.ru`
   - `www.kaplao.ru` (рекомендуется)  
4. Vercel покажет, какие DNS нужны.

Обычно:

**Вариант для корневого домена `kaplao.ru` (Apex):**

| Type | Name / Host | Value |
|------|-------------|--------|
| **A** | `@` или пусто | `76.76.21.21` |

(IP Vercel может отличаться — **смотри точное значение в панели Vercel Domains**, не копируй вслепую из старых гайдов.)

**Для www:**

| Type | Name | Value |
|------|------|--------|
| **CNAME** | `www` | `cname.vercel-dns.com` |

Или Vercel предложит свой CNAME target — используй **их** значение.

Рекомендация: сделай `www.kaplao.ru` → redirect на `kaplao.ru` (или наоборот) кнопкой в Vercel Domains (Redirect).

### B3. DNS у регистратора (где куплен kaplao.ru)

1. Зайди в панель регистратора (REG.RU / Timeweb / Cloudflare / Nic.ru и т.д.).  
2. Раздел **DNS / Управление зоной** для `kaplao.ru`.  
3. Удали конфликтующие старые A/CNAME на `@` и `www`, если они указывают не на Vercel.  
4. Добавь записи из B2.  
5. TTL можно 300–3600.

**Важно про Resend + сайт:**

- Записи для **Vercel** (A/CNAME сайта) и записи для **Resend** (DKIM TXT и т.д.) живут **рядом** в одной DNS-зоне.  
- Не удаляй DKIM Resend, когда правишь A для Vercel.

### B4. Дождись статуса Valid на Vercel

В **Settings → Domains** статус станет **Valid** (часто 5–30 минут, иногда дольше).  
Пока Invalid — сайт на кастомном домене не откроется (`.vercel.app` при этом работает).

Проверка:

```text
https://kaplao.ru
https://www.kaplao.ru
```

Должен открыться Kaplao Account (register/login).

### B5. HTTPS

Vercel сам выпускает SSL (Let's Encrypt).  
Если браузер ругается — подожди выпуска сертификата после Valid.

### B6. Обнови URL в Unity

В игре:

1. `KaplaoAccountConfig` / Public Level Catalog — **Website URL** = `https://kaplao.ru`  
2. Убери `localhost`, если был.  
3. `GAME_SERVER_SECRET` на сайте и в Unity должен совпадать.

Иначе логин из игры / verify-player / nickname presets не достучатся до нового домена.

### B7. Переменные Production ещё раз

В Vercel Production обязательно:

- `JWT_SECRET`
- `GAME_SERVER_SECRET`
- `SUPABASE_URL` / `SUPABASE_SERVICE_ROLE_KEY`
- `RESEND_API_KEY` / `EMAIL_FROM`
- `NEXT_PUBLIC_APP_URL=https://kaplao.ru`
- уровни S3 / Levels Supabase — как было

После смены домена — **Redeploy**.

---

## Часть C. SQL в Supabase (для кодов)

Если таблицы уже есть, выполни миграцию purpose:

`KaplaoGamesWebsite/supabase/migrations/20260905_verification_purpose.sql`

```sql
alter table if exists kaplao_verification_codes
  add column if not exists purpose text not null default 'verify_email';
```

Без этой колонки сброс пароля может не отличаться от верификации на старых схемах.

---

## Часть D. Что появилось на сайте (кратко)

| URL | Назначение |
|-----|------------|
| `/register` | Регистрация → письмо с кодом → `/verify` |
| `/verify` | Ввод кода верификации (+ Resend) |
| `/login` | Вход (без verified → на verify). Есть «Forgot password?» |
| `/forgot-password` | Запрос кода сброса |
| `/reset-password` | Новый пароль по коду |
| `/account` | Смена ника (после логина на сайте) |

Смена ника обновляет JWT на сайте; **в игре** нужно снова Sign in, чтобы подтянуть новый ник.

---

## Часть E. Чеклист «всё работает»

1. [ ] `kaplao.ru` Valid в Vercel  
2. [ ] `https://kaplao.ru/register` открывается  
3. [ ] Resend Domain `kaplao.ru` Verified  
4. [ ] `EMAIL_FROM=Kaplao <noreply@kaplao.ru>`  
5. [ ] Письмо с кодом приходит на Gmail  
6. [ ] Verify → Account  
7. [ ] Forgot password → Reset работает  
8. [ ] Unity Website URL = `https://kaplao.ru`  
9. [ ] Логин из игры (device code) после verify работает  

---

## Частые ошибки

| Симптом | Что проверить |
|---------|----------------|
| Письма только себе | Всё ещё `onboarding@resend.dev` или домен не Verified |
| 550 / domain not verified | From с `@kaplao.ru`, а DNS DKIM не прошёл |
| Домен не открывается | A/CNAME не те; DNS не обновился; не тот проект Vercel |
| WWW открывается, apex нет | Нет A-записи на `@` |
| Игра не логинит | Старый Website URL / secret |
| «Invalid or expired code» | Просрочка 15 мин; не тот purpose; не прогнана SQL migration |

---

## Полезные ссылки

- Vercel Domains: https://vercel.com/docs/domains  
- Resend Domains: https://resend.com/docs/dashboard/domains/introduction  
- Resend API Keys: https://resend.com/api-keys  
