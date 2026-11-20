# Deploy KaplaoGamesWebsite на Vercel (полная инструкция)

Сайт = папка `KaplaoGamesWebsite` (Next.js).  
Игра Unity **не** деплоится на Vercel — только сайт аккаунтов.

> **Домен `kaplao.ru` + Resend (коды на почту):** см. подробный гайд  
> **[DOMAIN_AND_RESEND.md](./DOMAIN_AND_RESEND.md)**

---

## 0. Что должно быть готово заранее

1. Аккаунт **GitHub**: https://github.com/signup  
2. Аккаунт **Vercel** (вход через GitHub): https://vercel.com/signup  
3. Проект **Supabase**: https://supabase.com/dashboard  
4. (Желательно) **Resend** для писем: https://resend.com  

Без Supabase на Vercel аккаунты **не сохранятся** (нет постоянного диска).

---

## 1. Supabase — создать таблицы (один раз)

1. Открой https://supabase.com/dashboard → свой проект.  
2. Слева **SQL Editor** → **New query**.  
3. Открой файл из репозитория:

`KaplaoGamesWebsite/supabase/schema.sql`

4. Скопируй весь SQL → вставь в редактор → **Run**.  
5. Должно быть Success.

6. Скопируй ключи (понадобятся в Vercel):

- **Project Settings** (шестерёнка) → **API**
- `Project URL` → это `SUPABASE_URL` (пример: `https://abcd1234.supabase.co`)
- `service_role` (secret) → это `SUPABASE_SERVICE_ROLE_KEY`  
  **Не** путай с `anon` key. **Не** вставляй service_role в Unity.

---

## 2. JWT_SECRET — придумай секрет

В PowerShell:

```powershell
-join ((48..57 + 65..90 + 97..122) | Get-Random -Count 48 | ForEach-Object {[char]$_})
```

Скопируй строку — это `JWT_SECRET`.

---

## 3. Resend (почта с кодом) — опционально, но нужно для реальных писем

1. https://resend.com/api-keys → Create → скопируй `RESEND_API_KEY`.  
2. Пока без своего домена:

```
EMAIL_FROM=Kaplao <onboarding@resend.dev>
```

Ограничение: `onboarding@resend.dev` шлёт **только на email твоего аккаунта Resend**.  
Для любой почты: https://resend.com/domains → свой домен → From с этого домена.

Если Resend не настроен — код покажется **на странице verify** (после регистрации).

---

## 4. Залить сайт на GitHub

### Вариант А — отдельный репозиторий только сайта (проще)

1. На GitHub: **New repository** → имя например `kaplao-website` → Create (без README, если пушишь существующую папку).  
2. В терминале:

```powershell
cd C:\Romareo228\Programs\UnityProjects\KaplaoGames\KaplaoGamesWebsite

git init
git add .
git commit -m "Kaplao account website for Vercel"
git branch -M main
git remote add origin https://github.com/ТВОЙ_НИК/kaplao-website.git
git push -u origin main
```

Замени `ТВОЙ_НИК` и имя репо на свои.

### Важно — не коммить секреты

В репо **не должно** попасть:

- `.env.local`
- `node_modules/`
- `.next/`
- `data/db.json` (локальная база)

Проверь `.gitignore`. Если `.env.local` уже закоммичен — удали из git:

```powershell
git rm --cached .env.local
git commit -m "Remove local env from git"
git push
```

Корень репозитория на GitHub должен содержать:

```
package.json
next.config.mjs
src/
public/
supabase/
...
```

---

## 5. Создать проект на Vercel

1. Открой https://vercel.com/dashboard  
2. **Add New…** → **Project**  
3. **Import** репозиторий `kaplao-website` (подключи GitHub, если ещё не).  
4. Настройки импорта:

| Поле | Значение |
|------|----------|
| Framework Preset | **Next.js** (обычно авто) |
| Root Directory | `.` (корень репо) |
| Build Command | `npm run build` (по умолчанию) |
| Output | не трогай (Next.js сам) |
| Install Command | `npm install` |

5. **Ещё не жми Deploy** — сначала Environment Variables (шаг 6).  
   Если кнопки переменных на этом экране нет — Deploy, потом сразу Settings → Environment Variables → Redeploy.

---

## 6. Environment Variables на Vercel

**Project → Settings → Environment Variables**

Добавь каждую (Environment: Production, Preview, Development — лучше отметить все):

| Name | Value | Откуда взять |
|------|--------|--------------|
| `JWT_SECRET` | длинная строка | шаг 2 |
| `SUPABASE_URL` | `https://xxxx.supabase.co` | Supabase → Settings → API |
| `SUPABASE_SERVICE_ROLE_KEY` | `eyJ...` | Supabase → service_role |
| `RESEND_API_KEY` | `re_...` | Resend (можно пусто на тест) |
| `EMAIL_FROM` | `Kaplao <onboarding@resend.dev>` | или свой домен |
| `NEXT_PUBLIC_APP_NAME` | `Kaplao Games` | любое |

`DATA_DIR` **не нужен** на Vercel.

Сохрани → **Deployments → … на последнем деплое → Redeploy** (чтобы подтянуть новые env).

---

## 7. Дождаться деплоя

1. Вкладка **Deployments** → статус **Ready**.  
2. Открой URL вида:

```
https://kaplao-website-xxxx.vercel.app
```

или свой домен, если привяжешь позже.

3. Проверка в браузере:

- открывается `/register`
- регистрация → verify (код на почте **или** на экране)
- login работает

Логи при ошибках: Deployment → **Logs** / **Functions**.

---

## 8. Вставить URL в Unity

1. Unity → Main Menu → компонент **Kaplao Account Config**  
2. **Website Url**:

```
https://ТВОЙ-ПРОЕКТ.vercel.app
```

Без `/` в конце. Не `localhost`.

3. Play → Settings → **Sign in** → браузер → логин → вернуться в игру.

Локальная разработка: снова можно ставить `http://localhost:3000` и `npm run dev`.

---

## 9. Локально с той же Supabase (необязательно)

В `KaplaoGamesWebsite/.env.local`:

```env
NEXT_PUBLIC_APP_NAME=Kaplao Games
JWT_SECRET=тот-же-или-другой-секрет

SUPABASE_URL=https://xxxx.supabase.co
SUPABASE_SERVICE_ROLE_KEY=eyJ...

RESEND_API_KEY=re_...
EMAIL_FROM=Kaplao <onboarding@resend.dev>
```

```powershell
cd C:\Romareo228\Programs\UnityProjects\KaplaoGames\KaplaoGamesWebsite
npm install
npm run dev
```

Если `SUPABASE_*` пустые — локально используется `data/db.json`.

---

## 10. Чеклист «всё работает»

- [ ] SQL `schema.sql` выполнен в Supabase  
- [ ] Код сайта запушен на GitHub (с `@supabase/supabase-js`)  
- [ ] Vercel Import + env переменные  
- [ ] Redeploy после добавления env  
- [ ] https://….vercel.app открывается  
- [ ] Регистрация + verify  
- [ ] Unity Website Url = Vercel URL  
- [ ] Sign in из игры подтягивает аккаунт  

---

## 11. Частые проблемы

| Симптом | Что делать |
|---------|------------|
| Build failed на Vercel | Смотри Build Logs; локально `npm run build` |
| Registration failed / 500 | Нет таблиц или неверный `SUPABASE_SERVICE_ROLE_KEY` → SQL Editor + env + Redeploy |
| Письмо не приходит | Без Resend код на странице verify; с Resend — spam / только email аккаунта Resend |
| Sign in в игре не завершается | Неверный Website Url; смотри Network на `/api/device/*`; проверь Supabase таблицу `kaplao_device_sessions` |
| Старый Deno URL | В Unity замени на Vercel URL |

---

## 12. Порядок действий одной строкой

**SQL в Supabase → push на GitHub → Import в Vercel → env → Deploy → URL в Unity.**
