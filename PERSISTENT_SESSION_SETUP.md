# Persistent Session & Quick Sign-In Setup

## Что было сделано

Реализован **persistent login** (сохранение сессии в браузере) и **quick sign-in** (быстрый вход в игре без повторного ввода пароля).

### Изменения

#### 1. Website (KaplaoGamesWebsite)

**Новые файлы:**
- `src/app/api/me/quick/route.ts` — API endpoint для проверки валидности сохраненной сессии
- `src/app/device-signin/page.tsx` — страница быстрого входа с проверкой localStorage

**Существующий код (уже работал):**
- При `/login` и `/verify` токен сохраняется в `localStorage.setItem("kaplao.account.token", token)`
- Токен остается в браузере даже после закрытия вкладки

#### 2. Unity Game (Assets/Scripts/Shared)

**Измененные файлы:**
- `KaplaoAccountClient.cs` — изменен URL с `/login?device=...` на `/device-signin?device=...`

## Как это работает

### Flow для нового пользователя:
1. Игрок нажимает "Sign in" в игре
2. Unity создает device code и открывает браузер на **`/device-signin?device=CODE`**
3. Браузер проверяет localStorage — токена нет
4. **Автоматический редирект** на `/register?device=CODE`
5. Пользователь регистрируется → получает email код → вводит код на `/verify`
6. После верификации:
   - Токен сохраняется в localStorage
   - Device code привязывается к аккаунту
   - Показывается `/success` — игрок возвращается в игру

### Flow для существующего пользователя (быстрый вход):
1. Игрок нажимает "Sign in" в игре (второй раз или после перезапуска)
2. Unity создает device code и открывает браузер на **`/device-signin?device=CODE`**
3. Браузер проверяет localStorage — **токен найден**
4. Отправляет запрос на `/api/me/quick` с токеном
5. Если токен валиден:
   - Показывает панель: **"Sign in as email@domain?"** с кнопками "Sign in" и "Use different account"
   - Игрок нажимает "Sign in" → device code привязывается → `/success`
6. Если токен невалиден (истек или отозван):
   - localStorage очищается
   - Редирект на `/register?device=CODE`

### Кнопка "Use different account":
- Очищает localStorage (`localStorage.removeItem("kaplao.account.token")`)
- Редиректит на `/register?device=CODE`
- Позволяет войти в другой аккаунт или создать новый

## Deployment

### 1. Website на Vercel

```bash
cd KaplaoGamesWebsite
git add .
git commit -m "feat: persistent session and quick sign-in"
git push origin main
```

Vercel автоматически задеплоит изменения (если подключен GitHub).

**Никаких SQL миграций не требуется!** Все работает с существующими таблицами.

### 2. Unity game

1. Скомпилировать проект с обновленным `KaplaoAccountClient.cs`
2. Билд будет открывать `/device-signin` вместо `/login`

## Проверка работоспособности

### Тест 1: Новый пользователь
1. Открыть игру
2. Settings → Sign in
3. Должен открыться браузер на `kaplao.ru/device-signin?device=XXXXX`
4. Сайт должен автоматически редиректнуть на `kaplao.ru/register?device=XXXXX`
5. Зарегистрироваться → ввести код из email
6. Должно показать `/success` с сообщением "return to the game"
7. Вернуться в игру, нажать "Sign in" снова — должно подключить аккаунт

### Тест 2: Повторный вход (quick sign-in)
1. Закрыть игру полностью
2. Запустить снова
3. Settings → Sign in
4. Должен открыться браузер на `kaplao.ru/device-signin?device=YYYYY`
5. **Должна показаться панель с вопросом "Sign in as your@email.com?"** с двумя кнопками
6. Нажать "Sign in as ..." → должно сразу привязать и показать `/success`
7. Вернуться в игру, нажать "Sign in" снова — должно подключить аккаунт

### Тест 3: Смена аккаунта
1. На панели "Sign in as ...?" нажать "Use different account"
2. localStorage должен очиститься
3. Должен редиректнуть на `/register?device=...`
4. Можно войти в другой аккаунт или создать новый

## Технические детали

### Безопасность
- Токен — это **JWT**, подписанный с помощью `JWT_SECRET` на сервере
- Токен хранится только в **localStorage браузера** (не в cookies, не в Unity)
- Unity получает токен через device code flow (polling `/api/device/poll`)
- Проверка токена идет через `/api/me/quick` с заголовком `Authorization: Bearer TOKEN`

### Session lifetime
- Токены в JWT по умолчанию не истекают (если не настроен `exp` claim)
- Можно добавить expiration позже в `signAuthToken()` в `lib/auth.ts`:
  ```typescript
  exp: Math.floor(Date.now() / 1000) + 60 * 60 * 24 * 30 // 30 days
  ```

### Что **не** хранится в localStorage
- Пароль (никогда!)
- Email (только токен)
- Device codes (они одноразовые и живут 10 минут на сервере)

## Откат (Rollback)

Если нужно вернуть старое поведение:

### Website:
```bash
git revert HEAD
git push origin main
```

### Unity:
Изменить обратно в `KaplaoAccountClient.cs`:
```csharp
string loginUrl = $"{pollBaseUrl}/login?device={UnityWebRequest.EscapeURL(deviceCode)}";
```

## FAQ

**Q: Что если токен украдут из localStorage?**  
A: Злоумышленник сможет войти в аккаунт, но:
- Только если имеет физический доступ к браузеру пользователя
- localStorage доступен только с того же домена (kaplao.ru)
- Можно добавить 2FA или IP проверку позже

**Q: Как пользователь выходит из всех устройств?**  
A: Нужно добавить кнопку "Sign out everywhere" на `/account` странице, которая:
1. Генерирует новый `JWT_SECRET` ключ (или добавляет версию токенов в БД)
2. Все старые токены станут невалидными
3. Пользователю придется войти заново

**Q: Нужно ли менять что-то в Supabase?**  
A: Нет! Все работает с существующими таблицами `kaplao_users` и `kaplao_device_codes`.

**Q: Как работает "автоматический вход после регистрации"?**  
A: Страница `/verify` уже сохраняет токен в localStorage после успешной верификации email кода. Это уже работало в старой версии.
