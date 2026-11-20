# Supabase Edge Functions — upload/download уровней (Timeweb S3)

Unity не резолвит `kaplaogames.vercel.app` на некоторых Windows-ПК, но нормально ходит на `*.supabase.co`.
Поэтому **upload/download zip** идут через Supabase Edge Functions, а не через Vercel.

Vercel по-прежнему нужен для **аккаунтов** (логин, админка).

---

## 1. Установи Supabase CLI (один раз)

```powershell
npm install -g supabase
```

Или: https://supabase.com/docs/guides/cli

---

## 2. Supabase Access Token (один раз)

Это **не** anon key и **не** service role key из настроек проекта.

1. Открой: **https://supabase.com/dashboard/account/tokens**
2. Войди в аккаунт Supabase (тот же, где проект Kaplao)
3. **Generate new token**
4. Имя, например: `kaplao-deploy`
5. **Generate token** → **скопируй** (показывается один раз, начинается с `sbp_...`)

Сохрани токен — он нужен только для деплоя функций из терминала.

---

## 3. Войди и привяжи проект

```powershell
cd KaplaoGamesWebsite
supabase login
supabase link --project-ref dmazorbjzecsfrcyxdsq
```

(`project-ref` — из URL Supabase: `https://dmazorbjzecsfrcyxdsq.supabase.co`)

---

## 3. Секреты для функций

Те же значения, что на Vercel:

```powershell
supabase secrets set GAME_SERVER_SECRET="твой-секрет"
supabase secrets set TIMEWEB_S3_ENDPOINT="https://s3.twcstorage.ru"
supabase secrets set TIMEWEB_S3_REGION="ru-1"
supabase secrets set TIMEWEB_S3_BUCKET="ПОЛНОЕ_ИМЯ_БАКЕТА"
supabase secrets set TIMEWEB_S3_ACCESS_KEY="..."
supabase secrets set TIMEWEB_S3_SECRET_KEY="..."
```

Опционально (если `GAME_SERVER_SECRET` не задан):

```powershell
supabase secrets set JWT_SECRET="..."
```

---

## 4. Деплой функций

```powershell
supabase functions deploy levels-upload-url
supabase functions deploy levels-download
```

---

## 5. Проверка

```powershell
$secret = "ТВОЙ_GAME_SERVER_SECRET"
$anon = "ТВОЙ_SUPABASE_ANON_KEY"
Invoke-RestMethod -Method POST `
  -Uri "https://dmazorbjzecsfrcyxdsq.supabase.co/functions/v1/levels-upload-url" `
  -Headers @{
    "apikey" = $anon
    "Authorization" = "Bearer $anon"
    "X-Kaplao-Game-Secret" = $secret
    "Content-Type" = "application/json"
  } `
  -Body "{}"
```

Ожидается JSON с `ok: true`, `upload_url`, `storage_path`.

---

## 6. Unity

Ничего менять в Inspector не нужно — игра уже использует:

- `https://dmazorbjzecsfrcyxdsq.supabase.co/functions/v1/levels-upload-url`
- `https://dmazorbjzecsfrcyxdsq.supabase.co/functions/v1/levels-download?path=...`

(`websiteUrl` на Vercel остаётся только для входа в аккаунт.)

---

## Ошибки

| Ошибка | Решение |
|--------|---------|
| `403 Forbidden` | `GAME_SERVER_SECRET` в Supabase secrets ≠ Unity |
| `503 Timeweb S3 is not configured` | Не заданы `TIMEWEB_S3_*` secrets |
| `404` на function | Не задеплоены функции (шаг 4) |
| `401` | Проверь `apikey` / anon key в запросе |
