# Account UX Improvements

## Что было сделано

Улучшен UX управления аккаунтом на KaplaoGamesWebsite:

### 1. ✅ Redirect с /register и /login на /account при активной сессии

**Изменено:**
- `src/app/register/page.tsx` — добавлена проверка localStorage при загрузке страницы
- `src/app/login/page.tsx` — добавлена проверка localStorage при загрузке страницы

**Логика:**
```typescript
useEffect(() => {
  const savedToken = localStorage.getItem("kaplao.account.token");
  if (!savedToken) return;
  
  // Проверка валидности токена через /api/me/quick
  const response = await fetch("/api/me/quick", {
    headers: { Authorization: `Bearer ${savedToken}` },
  });
  
  if (data.ok && data.email) {
    router.push("/account"); // Redirect
  }
}, []);
```

**Результат:** Если пользователь уже залогинен и открывает `/login` или `/register`, его автоматически перекидывает на `/account`.

---

### 2. ✅ Отдельная панель для смены никнейма

**Новый файл:**
- `src/app/account/change-nickname/page.tsx` — отдельная страница с формой смены ника

**Изменено:**
- `src/app/account/page.tsx` — удалена встроенная форма смены ника, добавлена кнопка "Change nickname"

**Результат:** 
- На `/account` показывается только кнопка "Change nickname"
- При клике открывается отдельная страница `/account/change-nickname` с формой
- После успешной смены ника редирект обратно на `/account`

---

### 3. ✅ Отдельная панель для смены пароля с проверкой старого пароля

**Новые файлы:**
- `src/app/account/change-password/page.tsx` — страница смены пароля
- `src/app/api/account/change-password/route.ts` — API endpoint для смены пароля

**Форма включает:**
- Current Password (текущий пароль)
- New Password (новый пароль, минимум 6 символов)
- Confirm New Password (подтверждение нового пароля)

**API endpoint логика:**
1. Проверяет авторизацию через Bearer token
2. Проверяет правильность старого пароля через `verifyPassword()`
3. Хеширует новый пароль через `hashPassword()`
4. Обновляет `passwordHash` в БД через `upsertUser()`

**Результат:**
- На `/account` кнопка "Change password" ведет на `/account/change-password`
- Требуется ввод старого пароля (защита от несанкционированной смены)
- Подтверждение нового пароля (защита от опечаток)

---

### 4. ✅ Подтверждение пароля на /register

**Изменено:**
- `src/app/register/page.tsx` — добавлено поле "Confirm Password"

**Валидация:**
```typescript
<Form.Item
  label="Confirm Password"
  name="confirmPassword"
  dependencies={["password"]}
  rules={[
    { required: true, message: "Confirm your password" },
    ({ getFieldValue }) => ({
      validator(_, value) {
        if (!value || getFieldValue("password") === value) {
          return Promise.resolve();
        }
        return Promise.reject(new Error("Passwords do not match"));
      },
    }),
  ]}
>
  <Input.Password size="large" placeholder="••••••••" />
</Form.Item>
```

**Результат:** При регистрации пользователь вводит пароль дважды. Если пароли не совпадают, форма показывает ошибку "Passwords do not match".

---

## Визуальная структура

### До изменений:

```
/account
├─ [Inline form: Nickname input + Save button]
└─ [Button: "Change password (email code)" → /forgot-password]
   [Button: "Sign out"]
```

### После изменений:

```
/account
├─ Signed in as: Nickname
├─ Email: email@domain.com
├─ [Button: "Change nickname" → /account/change-nickname]
├─ [Button: "Change password" → /account/change-password]
└─ [Button: "Sign out"]

/account/change-nickname
├─ Form: New Nickname
└─ [Button: "Save nickname"]
   [Button: "Back to Account"]

/account/change-password
├─ Form: Current Password
├─ Form: New Password
├─ Form: Confirm New Password
└─ [Button: "Change password"]
   [Button: "Back to Account"]

/register
├─ Form: Nickname
├─ Form: Email
├─ Form: Password
├─ Form: Confirm Password ← NEW
└─ [Button: "Create account"]
```

---

## Deployment

### Website (Vercel):

```bash
cd KaplaoGamesWebsite
git add .
git commit -m "feat: improve account UX - separate panels for nickname and password change"
git push origin main
```

Vercel задеплоит автоматически.

**SQL миграций не требуется!** Все работает с существующими таблицами.

---

## Тестирование

### Тест 1: Redirect при активной сессии
1. Войти в аккаунт на `/login`
2. Открыть `/register` в новой вкладке
3. **Ожидается:** автоматический редирект на `/account`
4. Открыть `/login` в новой вкладке
5. **Ожидается:** автоматический редирект на `/account`

### Тест 2: Смена никнейма через отдельную панель
1. На `/account` нажать "Change nickname"
2. Открывается `/account/change-nickname` с формой
3. Ввести новый ник, нажать "Save nickname"
4. **Ожидается:** редирект на `/account`, показан новый ник

### Тест 3: Смена пароля с проверкой старого
1. На `/account` нажать "Change password"
2. Открывается `/account/change-password`
3. Ввести **неправильный** старый пароль → ошибка "Current password is incorrect"
4. Ввести **правильный** старый пароль + новый пароль (разные значения в New/Confirm) → ошибка "Passwords do not match"
5. Ввести правильный старый пароль + совпадающие новые пароли → успех
6. **Ожидается:** редирект на `/account`, можно войти с новым паролем

### Тест 4: Подтверждение пароля при регистрации
1. Открыть `/register`
2. Заполнить форму:
   - Password: `test123`
   - Confirm Password: `test456` (разные)
3. **Ожидается:** ошибка "Passwords do not match"
4. Исправить Confirm Password на `test123`
5. **Ожидается:** форма отправляется успешно

---

## Безопасность

### Смена пароля
- Требуется знание **текущего пароля** (защита от взлома через украденную сессию)
- Новый пароль хешируется через `hashPassword()` (bcrypt)
- Проверка старого пароля через `verifyPassword()` (constant-time comparison)

### Redirect на /account
- Проверяется валидность токена через `/api/me/quick` перед redirect
- Если токен невалиден (истек), редирект не происходит

---

## Откат (Rollback)

Если нужно вернуть старое поведение:

```bash
git revert HEAD
git push origin main
```

Vercel задеплоит откат автоматически.

---

## Структура файлов (итого)

### Новые файлы:
```
src/app/account/change-nickname/page.tsx (142 lines)
src/app/account/change-password/page.tsx (133 lines)
src/app/api/account/change-password/route.ts (67 lines)
```

### Измененные файлы:
```
src/app/account/page.tsx (упрощена, удалена форма)
src/app/register/page.tsx (добавлен redirect + confirm password)
src/app/login/page.tsx (добавлен redirect)
```

Всего: **3 новых файла**, **3 измененных файла**.
