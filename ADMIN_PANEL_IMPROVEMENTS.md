# Admin Panel & Sign-In Improvements

## Что было сделано

### 1. ✅ /admin проверяет активную сессию и редиректит на /login

**Проблема:** `/admin` использовал отдельный localStorage ключ `kaplao.admin.token`, который не синхронизировался с `kaplao.account.token`. Если пользователь выходил на `/account`, админ-панель продолжала работать.

**Решение:**

#### Website:
- `src/app/admin/page.tsx`:
  - Добавлен `router.push("/login")` если нет токена
  - При невалидном токене также редиректит на `/login`
  - При логине в админ-панели теперь сохраняется токен в **оба** localStorage ключа:
    ```typescript
    localStorage.setItem("kaplao.admin.token", data.token);
    localStorage.setItem("kaplao.account.token", data.token);
    ```

- `src/app/login/page.tsx` и `src/app/verify/page.tsx`:
  - После успешного логина/верификации проверяется `isSuperAdmin`
  - Если true → токен сохраняется также в `kaplao.admin.token`
  - Теперь админы могут войти через `/login` и сразу иметь доступ к `/admin`

- `src/app/account/page.tsx`:
  - При загрузке проверяется `isSuperAdmin`
  - Если true → токен синхронизируется в `kaplao.admin.token`

**Результат:** 
- Если нет токена → редирект на `/login`
- Если токен есть, но невалиден → редирект на `/login`
- Админы теперь имеют синхронизированную сессию между `/account` и `/admin`

---

### 2. ✅ Кнопка "Admin panel" на /account для админов

**Изменено:**
- `src/app/account/page.tsx`:
  - Добавлено состояние `isSuperAdmin`
  - При загрузке проверяется `/api/me` → если `isSuperAdmin === true`, показывается кнопка
  - Кнопка **"Admin panel"** с `type="primary"` ведет на `/admin`

**Код:**
```typescript
{isSuperAdmin ? (
  <Link href="/admin" style={{ display: "block" }}>
    <Button size="large" block type="primary">
      Admin panel
    </Button>
  </Link>
) : null}
```

**Результат:** Админы видят кнопку "Admin panel" на странице `/account`, обычные пользователи — нет.

---

### 3. ✅ Сообщение об отсутствии прав при ручном переходе на /admin

**Изменено:**
- `src/app/admin/page.tsx` — обновлено сообщение об ошибке:

**Было:**
```
Logged in as email@domain.com, but you need Web Console right to open the admin panel.
```

**Стало:**
```
Your account (email@domain.com) does not have admin rights. Contact the site administrator to grant Web Console permission.
```

**Результат:** Если пользователь вручную прописывает `/admin` в URL, но не имеет прав, показывается понятное сообщение.

---

### 4. ✅ Отмена ожидания браузера при повторном входе в Settings (Unity)

**Проблема:** Если игрок нажал "Sign in", открылся браузер, но игрок **случайно закрыл окно**, игра продолжала ждать вечно. Нужно было перезапускать игру.

**Решение:**

#### A. Добавлен публичный метод `CancelSignIn()`:
- `Assets/Scripts/Shared/KaplaoAccountClient.cs`:
  ```csharp
  /// <summary>
  /// Cancel active sign-in flow (e.g., if user closed the browser window).
  /// Allows starting a new sign-in attempt without restarting the game.
  /// </summary>
  public void CancelSignIn()
  {
      if (!IsBusy)
          return;

      Debug.Log("[KaplaoAccount] Sign-in cancelled by user");
      CancelActiveFlow(clearStatus: true);
      signInGeneration++;
      IsBusy = false;
      PlayerPrefs.DeleteKey(PendingCodeKey);
      PlayerPrefs.DeleteKey(PendingUrlKey);
      PlayerPrefs.Save();
      SetStatus("Sign-in cancelled. Click Sign in to try again.");
  }
  ```

#### B. Автоматическая отмена при reopening Settings:
- `Assets/Scripts/UI/MainMenuUI.cs` → `ShowSettingsPanel()`:
  ```csharp
  void ShowSettingsPanel()
  {
      // Cancel any active sign-in flow when reopening settings
      // (e.g., user closed the browser window and wants to try again)
      if (accountClient != null && accountClient.IsBusy)
      {
          accountClient.CancelSignIn();
      }

      SyncSettingsControls();
      ShowOnlyPanels(settingsPanel);
      RefreshOwnedSkinsFromServer();
  }
  ```

**Как это работает:**
1. Игрок нажимает "Sign in" → открывается браузер
2. Игрок случайно закрывает браузер
3. Игрок нажимает **Back** → возвращается в главное меню
4. Игрок снова нажимает **Settings**
5. **Автоматически:** `CancelSignIn()` сбрасывает ожидание
6. Кнопка "Sign in" снова активна → можно попробовать еще раз

**Результат:** Игроку не нужно перезапускать игру, если он закрыл браузер. Достаточно вернуться в Settings (Back → Settings) и кнопка снова станет доступной.

---

## Визуальная структура

### До изменений:

```
/admin
└─ Использует отдельную сессию (kaplao.admin.token)
   Не синхронизируется с /account

/account
└─ Нет кнопки Admin panel для админов
```

### После изменений:

```
/admin
├─ Проверяет kaplao.admin.token
├─ Если нет токена → redirect /login
├─ Если невалиден → redirect /login
└─ Если нет прав → ошибка "Your account does not have admin rights"

/account (для админов)
├─ [Button: "Change nickname"]
├─ [Button: "Change password"]
├─ [Button: "Admin panel" (PRIMARY)] ← NEW
└─ [Button: "Sign out"]

/account (для обычных пользователей)
├─ [Button: "Change nickname"]
├─ [Button: "Change password"]
└─ [Button: "Sign out"]
```

---

## Flow синхронизации сессий

### Логин через /login:
```
1. POST /api/login → получить token
2. localStorage.setItem("kaplao.account.token", token)
3. Проверить /api/me → если isSuperAdmin === true:
   localStorage.setItem("kaplao.admin.token", token)
4. Redirect /account
```

### Логин через /admin:
```
1. POST /api/login → получить token
2. Проверить /api/me → если isSuperAdmin === true:
   localStorage.setItem("kaplao.admin.token", token)
   localStorage.setItem("kaplao.account.token", token)
3. Загрузить админ-панель
```

### Загрузка /account:
```
1. Получить token из localStorage.getItem("kaplao.account.token")
2. Проверить /api/me → если isSuperAdmin === true:
   - setIsSuperAdmin(true)
   - localStorage.setItem("kaplao.admin.token", token)
   - Показать кнопку "Admin panel"
```

### Загрузка /admin:
```
1. Получить token из localStorage.getItem("kaplao.admin.token")
2. Если нет токена → redirect /login
3. Проверить /api/me → если !isSuperAdmin:
   - Показать ошибку "Your account does not have admin rights"
4. Если все OK → загрузить админ-панель
```

---

## Unity Sign-In Flow

### Было:
```
1. Нажать "Sign in" → открыть браузер
2. Игра ждет...
3. Если закрыл браузер → игра ждет ВЕЧНО
4. Нужно перезапустить игру
```

### Стало:
```
1. Нажать "Sign in" → открыть браузер
2. Игра ждет...
3. Если закрыл браузер:
   - Нажать Back
   - Нажать Settings снова
   - Автоматически: CancelSignIn()
   - Кнопка "Sign in" снова активна
4. Попробовать еще раз БЕЗ ПЕРЕЗАПУСКА ИГРЫ
```

---

## Deployment

### Website (Vercel):

```bash
cd KaplaoGamesWebsite
git add .
git commit -m "feat: admin panel session sync and sign-in improvements"
git push origin main
```

Vercel задеплоит автоматически.

**SQL миграций не требуется!**

### Unity:

1. Скомпилировать проект с обновленными `KaplaoAccountClient.cs` и `MainMenuUI.cs`
2. Создать новый билд

---

## Тестирование

### Тест 1: Admin panel redirect
1. Открыть `/admin` без логина
2. **Ожидается:** автоматический редирект на `/login`

### Тест 2: Синхронизация сессий
1. Войти через `/login` с админ-аккаунтом
2. Открыть `/admin`
3. **Ожидается:** админ-панель открывается сразу (не просит логин)
4. Выйти на `/account` (Sign out)
5. Открыть `/admin`
6. **Ожидается:** редирект на `/login`

### Тест 3: Кнопка Admin panel
1. Войти с **админ-аккаунтом**
2. Открыть `/account`
3. **Ожидается:** кнопка "Admin panel" (синяя, primary)
4. Выйти и войти с **обычным аккаунтом**
5. Открыть `/account`
6. **Ожидается:** кнопки "Admin panel" нет

### Тест 4: Недостаточно прав
1. Войти с **обычным аккаунтом**
2. Вручную прописать в URL `/admin`
3. **Ожидается:** сообщение "Your account does not have admin rights. Contact the site administrator..."

### Тест 5: Отмена sign-in в Unity
1. Запустить игру
2. Settings → Sign in
3. Браузер открылся → **закрыть браузер сразу**
4. В игре: Back → Settings снова
5. **Ожидается:** кнопка "Sign in" снова активна (не "I've signed in")
6. Нажать "Sign in" снова → должно открыться окно браузера

---

## Безопасность

### Синхронизация токенов
- Токен хранится в **двух** ключах localStorage: `kaplao.account.token` и `kaplao.admin.token`
- Оба ключа используют **один и тот же JWT токен**
- При выходе (Sign out) оба ключа очищаются

### Проверка прав
- `/admin` проверяет `isSuperAdmin` через `/api/me`
- Если права отсутствуют → показывает ошибку (не редиректит на `/login`)
- Логика проверки находится на **сервере** (`/api/me` → `resolvePlayerRole()`)

---

## Структура файлов (итого)

### Измененные файлы:

**Website:**
```
src/app/admin/page.tsx
  - Добавлен redirect на /login если нет токена
  - Синхронизация обоих localStorage ключей при логине
  - Улучшено сообщение об ошибке

src/app/account/page.tsx
  - Добавлена проверка isSuperAdmin
  - Показывается кнопка "Admin panel" для админов
  - Синхронизация admin токена при загрузке

src/app/login/page.tsx
  - Синхронизация admin токена после успешного логина (если админ)

src/app/verify/page.tsx
  - Синхронизация admin токена после верификации email (если админ)
```

**Unity:**
```
Assets/Scripts/Shared/KaplaoAccountClient.cs
  - Добавлен публичный метод CancelSignIn()

Assets/Scripts/UI/MainMenuUI.cs
  - Автоматическая отмена sign-in при reopening Settings
```

Всего: **6 измененных файлов**.

---

## Откат (Rollback)

### Website:
```bash
git revert HEAD
git push origin main
```

### Unity:
- Откатить изменения в `KaplaoAccountClient.cs` и `MainMenuUI.cs`
- Пересобрать билд
