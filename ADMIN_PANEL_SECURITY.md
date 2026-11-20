# Admin Panel Security Documentation

## КРИТИЧЕСКАЯ ИНФОРМАЦИЯ О БЕЗОПАСНОСТИ

### ✅ ВСЕ Admin API Endpoints ЗАЩИЩЕНЫ

**Каждый** admin API endpoint имеет серверную проверку через `requireWebConsoleSession(request)`:

```typescript
export async function requireWebConsoleSession(request: Request): Promise<AuthSession | null> {
  const session = await authSessionFromRequest(request);
  if (!session) return null;
  if (!(await canAccessWebConsole(session.email))) return null;
  return session;
}
```

### Защищенные endpoints:

#### 1. `/api/admin/players` (GET, PUT, DELETE)
```typescript
export async function GET(request: Request) {
  const session = await requireWebConsoleSession(request);
  if (!session) {
    return NextResponse.json({ error: "Forbidden." }, { status: 403 });
  }
  // ... load players
}

export async function PUT(request: Request) {
  const session = await requireWebConsoleSession(request);
  if (!session) {
    return NextResponse.json({ error: "Forbidden." }, { status: 403 });
  }
  // ... update player rights/preset
}

export async function DELETE(request: Request) {
  const session = await requireWebConsoleSession(request);
  if (!session) {
    return NextResponse.json({ error: "Forbidden." }, { status: 403 });
  }
  // ... delete account
}
```

#### 2. `/api/admin/nickname-presets` (GET, POST, PUT, DELETE)
```typescript
async function requireSuperAdmin(request: Request) {
  return requireWebConsoleSession(request);
}

export async function GET(request: Request) {
  const session = await requireSuperAdmin(request);
  if (!session) {
    return NextResponse.json({ error: "Forbidden." }, { status: 403 });
  }
  // ... load presets
}

// POST, PUT, DELETE - все также проверяют requireSuperAdmin
```

#### 3. `/api/admin/logs` (GET, PUT, DELETE)
```typescript
async function requireSuperAdmin(request: Request) {
  return requireWebConsoleSession(request);
}

export async function GET(request: Request) {
  const session = await requireSuperAdmin(request);
  if (!session) {
    return NextResponse.json({ error: "Forbidden." }, { status: 403 });
  }
  // ... load logs
}

// PUT (settings), DELETE (log entry) - все проверяют requireSuperAdmin
```

#### 4. `/api/admin/admins` (GET, POST, DELETE)
```typescript
async function requireSuperAdmin(request: Request) {
  return requireWebConsoleSession(request);
}

export async function GET(request: Request) {
  const session = await requireSuperAdmin(request);
  if (!session) {
    return NextResponse.json({ error: "Forbidden." }, { status: 403 });
  }
  // ... load admin list
}

// POST (add admin), DELETE (remove admin) - все проверяют requireSuperAdmin
```

---

## Как работает проверка прав

### 1. Проверка токена:
```typescript
async function authSessionFromRequest(request: Request): Promise<AuthSession | null> {
  const authHeader = request.headers.get("authorization") || "";
  const token = authHeader.replace(/^Bearer\s+/i, "").trim();
  if (!token) return null;
  return await sessionFromAuthToken(token);
}
```

### 2. Проверка прав Web Console:
```typescript
export async function canAccessWebConsole(email: string): Promise<boolean> {
  const normalized = email.trim().toLowerCase();
  
  // Super admin всегда имеет доступ
  if (isSuperAdminEmail(normalized)) return true;
  
  // Проверка прав из БД
  try {
    const role = await getPlayerRole(normalized);
    return rightsGrantWebConsole(role?.rights ?? []);
  } catch {
    return false;
  }
}

// Проверяет наличие права "web_console" в массиве прав
export function rightsGrantWebConsole(rights: PlayerRight[]): boolean {
  return roleHasRight(rights, "web_console");
}
```

### 3. Получение прав из БД:
```typescript
export async function getPlayerRole(email: string): Promise<PlayerRoleRecord | undefined> {
  const key = email.trim().toLowerCase();
  if (!key) return undefined;

  const sb = getSupabase();
  if (sb) {
    const { data, error } = await sb
      .from("kaplao_player_roles")
      .select("*")
      .eq("email", key)
      .maybeSingle();
    throwIfSbError(error, "getPlayerRole");
    return data ? rowToPlayerRole(data as SbPlayerRoleRow) : undefined;
  }
  
  // Fallback to file-based DB
  const db = await readFileDb();
  return db.playerRoles.find((r) => r.email.toLowerCase() === key);
}
```

---

## Что МОЖЕТ сделать обычный пользователь

### ❌ НЕ МОЖЕТ:
- Открыть `/api/admin/players` — вернет `403 Forbidden`
- Сохранить изменения игроков — вернет `403 Forbidden`
- Создать/изменить nickname presets — вернет `403 Forbidden`
- Удалить логи — вернет `403 Forbidden`
- Добавить/удалить админов — вернет `403 Forbidden`

### ✅ МОЖЕТ (но это не проблема):
- **Видеть** страницу `/admin` в браузере (клиентский HTML/CSS/JS)
- Видеть форму логина на `/admin` (если нет токена, редиректит на `/login`)
- Пытаться отправить запросы на admin API (все запросы вернут `403 Forbidden`)

**Причина:** Страница `/admin` — это React компонент, который **рендерится на клиенте**. Любой может открыть HTML, но **все API вызовы заблокированы на сервере**.

---

## Проверка безопасности

### Тест 1: Попытка доступа без токена
```bash
curl -X GET "https://kaplao.ru/api/admin/players"
# Ожидается: {"error":"Forbidden."}  Status: 403
```

### Тест 2: Попытка доступа с токеном обычного пользователя
```bash
curl -X GET "https://kaplao.ru/api/admin/players" \
  -H "Authorization: Bearer USER_TOKEN_HERE"
# Ожидается: {"error":"Forbidden."}  Status: 403
```

### Тест 3: Доступ с токеном админа
```bash
curl -X GET "https://kaplao.ru/api/admin/players" \
  -H "Authorization: Bearer ADMIN_TOKEN_HERE"
# Ожидается: {"ok":true,"players":[...]}  Status: 200
```

### Тест 4: Попытка изменить данные без прав
```bash
curl -X PUT "https://kaplao.ru/api/admin/players" \
  -H "Authorization: Bearer USER_TOKEN_HERE" \
  -H "Content-Type: application/json" \
  -d '{"email":"victim@example.com","rights":["game_host"]}'
# Ожидается: {"error":"Forbidden."}  Status: 403
```

---

## Выдача прав Web Console админу

### Через SQL (Supabase):
```sql
INSERT INTO kaplao_player_roles (email, permission, rights, nickname_preset, updated_at)
VALUES (
  'admin@example.com',
  'admin',
  '["web_console", "game_host", "level_delete"]'::jsonb,
  'none',
  NOW()
)
ON CONFLICT (email) DO UPDATE SET
  rights = '["web_console", "game_host", "level_delete"]'::jsonb,
  permission = 'admin',
  updated_at = NOW();
```

### Через admin панель (если уже есть один админ):
1. Войти в `/admin` с существующим админ-аккаунтом
2. Players tab → найти пользователя по email
3. Выбрать права: `web_console`, `game_host`, etc.
4. Save

### Через переменные окружения (Super Admin):
```env
SUPER_ADMIN_EMAIL=owner@example.com
```

Этот email **всегда** имеет полные права, включая `web_console`.

---

## Изменения безопасности (06.09.2026)

### ✅ Удалена кнопка "Sign out" с admin панели
**Причина:** Кнопка "Sign out" на `/admin` только очищала `localStorage`, но **не** проверяла сессию. Это создавало путаницу.

**Решение:** 
- Кнопка удалена
- Пользователь может выйти через `/account` → Sign out
- Это очистит **оба** ключа: `kaplao.account.token` и `kaplao.admin.token`

---

## FAQ

### Q: Может ли кто-то взломать admin панель?
**A:** Нет. Все критичные операции **защищены на сервере**. Даже если кто-то откроет HTML страницу `/admin`, все API вызовы будут возвращать `403 Forbidden` без прав `web_console`.

### Q: Что если токен украдут?
**A:** JWT токен подписан с помощью `JWT_SECRET`. Без знания секрета невозможно создать валидный токен. Если токен украден, админ должен:
1. Сменить пароль → старые токены станут невалидными
2. Или поменять `JWT_SECRET` → все токены станут невалидными

### Q: Можно ли обойти проверку `requireWebConsoleSession`?
**A:** Нет. Проверка происходит на **сервере** при каждом API запросе. Клиентский код не может обойти серверную проверку.

### Q: Что если в БД нет записи о правах?
**A:** Если в таблице `kaplao_player_roles` нет записи для email, функция `getPlayerRole()` вернет `undefined`, и `rightsGrantWebConsole()` вернет `false` → доступ запрещен.

### Q: Как проверить, что все работает?
**A:** 
1. Открыть `/admin` **без** логина → должно редиректить на `/login`
2. Войти с **обычным** аккаунтом → `/admin` покажет ошибку "Your account does not have admin rights"
3. Попытаться загрузить `/api/admin/players` в браузере → должно вернуть `403 Forbidden`
4. Войти с **админ-аккаунтом** → `/admin` должен загрузиться полностью

---

## Структура безопасности (диаграмма)

```
Пользователь запрос → /api/admin/players (GET)
                          ↓
              authSessionFromRequest(request)
                          ↓
              Проверка токена из Authorization header
                          ↓
              sessionFromAuthToken(token)
                          ↓
              Валидация JWT подписи
                          ↓
              Получение email из токена
                          ↓
              canAccessWebConsole(email)
                          ↓
    ┌─────────────────────┴─────────────────────┐
    ↓                                           ↓
isSuperAdminEmail(email)?              getPlayerRole(email)
    ↓ YES                                      ↓
  return true                    Проверка rights в БД
                                               ↓
                                 rightsGrantWebConsole(rights)
                                               ↓
                                 roleHasRight(rights, "web_console")
                                               ↓
                                    return true/false
                          ↓
              requireWebConsoleSession()
                          ↓
         ┌────────────────┴────────────────┐
         ↓ session === null                ↓ session !== null
   return 403 Forbidden            Выполнить запрос
                                    return 200 OK
```

---

## Рекомендации

1. **Регулярно проверяйте логи** (`/api/admin/logs`) на подозрительную активность
2. **Используйте сильные пароли** для админ-аккаунтов
3. **Не делитесь токенами** — они дают полный доступ к админ-панели
4. **Ограничьте количество админов** — выдавайте права `web_console` только доверенным лицам
5. **Регулярно меняйте `JWT_SECRET`** если подозреваете компрометацию токенов

---

## Deployment

Все изменения уже на сервере. **SQL миграций не требуется** — проверка прав работает через существующую таблицу `kaplao_player_roles`.

Чтобы задеплоить удаление кнопки Sign out:

```bash
cd KaplaoGamesWebsite
git add .
git commit -m "security: remove Sign out button from admin panel"
git push origin main
```

Vercel задеплоит автоматически.
