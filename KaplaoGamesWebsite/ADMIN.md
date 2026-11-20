# Kaplao Admin — документация

Документация панели `/admin` на сайте KaplaoGamesWebsite.  
Здесь настраиваются права игроков, скины, **внешний вид ников** и логи.

---

## Как открыть админку

1. Зайди на сайт → `/admin` (или `/login`, затем `/admin`).
2. Войди аккаунтом, у которого есть право **Web Console**.
3. Корневой владелец `asmoloff55@gmail.com` всегда имеет полный доступ и не может его потерять.

Без **Web Console** сайт покажет ошибку вроде: *you need Web Console right*.

После входа токен хранится в браузере (`kaplao.admin.token`).

---

## Вкладки панели

| Вкладка | Зачем |
|--------|--------|
| **Players** | Права, пресет ника, monster skins, удаление аккаунта |
| **Nickname presets** | Полная настройка внешнего вида ников (главное) |
| **Logs** | Логи действий игроков + авто-очистка |

---

## Players — игроки

У каждого игрока можно независимо задать:

1. **Rights** — права (мультивыбор, как скины)
2. **Nick preset** — какой пресет ника применить
3. **Monster skins** — какие админ-скины выдать

После изменений нажми **Save**.  
Права и пресет ника в игре применяются **после перезахода** в матч/мир.  
**Web Console** действует при следующем входе в `/admin`.

### Rights (права)

Можно включить любую комбинацию:

| ID в системе | Название в UI | Что даёт |
|--------------|---------------|----------|
| `level_purge` | **Level Purge** | Удалять чужие уровни с публичных серверов (модерация каталога) |
| `game_host` | **Game Host** | Постоянная админка **внутри игры** (как у хоста): модерация, админ-команды и т.п. |
| `web_console` | **Web Console** | Доступ к этой админке на сайте |

Примеры:

- Только удалять уровни → только **Level Purge**
- Админ в игре, без сайта → только **Game Host**
- Помощник по сайту → **Web Console** (+ при необходимости другие)
- Полный доступ → все три

Корень `asmoloff55@gmail.com` при сохранении всегда получает все три права.

### Nick preset (какой ник у игрока)

В выпадающем списке:

- `none` — обычный текст без эффектов (дефолт для всех)
- любые кастомные пресеты, созданные на вкладке **Nickname presets**

Ты **не** настраиваешь градиент/шрифт в колонке игрока.  
Ты только **выбираешь имя пресета**. Сам вид пресета редактируется на вкладке Nickname presets.

Старое значение `own` больше не используется и считается как `none`.

### Monster skins

Мультивыбор админ-скинов персонажа (как раньше).  
Пусто = только обычные скины.

### Delete

Удаляет аккаунт игрока (роли, логин-данные, скины).  
Корневой аккаунт удалить нельзя.

---

## Nickname presets — ники (главное)

### Идея системы

- **Вся визуальная кастомизация ника** делается **в админке**.
- **В Unity** для ников остаётся только реестр шрифтов: имя → TMP Font Asset.
- Игра при запуске/заходе качает список пресетов с сайта и применяет **Easy Text Effects**.

Схема:

```
Админка: создаёшь пресет "rainbow"
   → fontName = "fancy"
   → градиент hex + move/rotate/…
   → Save style

Players: игроку ставишь Nick preset = rainbow

Unity: KaplaoNicknameFontRegistry
   → name "fancy" → нужный TMP font

Игрок заходит в игру
   → verify-player отдаёт nicknamePreset = "rainbow"
   → клиент качает /api/game/nickname-presets
   → на nameplate вешается Easy Text Effects по JSON пресета
```

### Встроенный пресет `none`

- Всегда доступен.
- Нельзя «отредактировать эффектами» в БД — это просто обычный ник.
- Цвет/шрифт по умолчанию берутся из Unity `KaplaoNicknameFontRegistry` (fallback).

### Создать пресет

1. Вкладка **Nickname presets**
2. Введи имя: только `a-z`, `0-9`, `_` (например `rainbow`, `mod_gold`)
3. **Add preset**
4. Нажми **Edit style** у пресета
5. Настрой стиль → **Save style**

Имена `none` / старый `own` как кастомные создавать нельзя.

### Edit style — что можно настроить

#### 1) Font name (строка)

- Просто текст, например `fancy` или `default`.
- **Не** список из Unity — ты сам пишешь название.
- В Unity в `KaplaoNicknameFontRegistry` должно быть такое же `name` и привязанный TMP-шрифт.
- Пусто = fallback-шрифт из реестра / TMP default.

#### 2) Base color (hex)

- Базовый цвет текста, например `#FFFFFF` или `#FFAA00`.
- Используется, когда эффектов нет или как база до анимаций цвета.
- Достаточно hex — без color picker.

#### 2b) Outline (TMP SDF)

- **Outline color hex** — цвет обводки, например `#000000`.
- **Outline width (жирность)** — `0`…`1` (обычно `0.15`–`0.35`). `0` = без outline.
- Работает вместе с Easy Text Effects (градиент/движение). Нужен SDF-шрифт с поддержкой outline.

#### 3) Easy Text Effects (максимум кастомизации)

Можно добавить **несколько** эффектов сразу. Типы:

| Тип | Что делает |
|-----|------------|
| **Color / Gradient** | Градиент или смена цвета / альфы |
| **Move** | Движение букв (смещение X/Y) |
| **Rotate** | Вращение букв |
| **Scale** | Масштаб букв |
| **Composite** | Стек дочерних эффектов вместе |
| **Per vertex** | Разные эффекты на углы глифа (TL/TR/BL/BR) |

У каждого эффекта есть **timing** (как в Easy Text Effects):

- `animationType`: Loop / LoopFixedDuration / PingPong / OneTime
- `durationPerChar`, `timeBetweenChars`, `fixedDuration`
- `easing`: linear / easeIn / easeOut / easeInOut
- `timeType`: UnscaledTime / ScaledTime
- флаги: no delay, reverse char order, clamp 0..1, override tags
- `triggerWhen`: OnStart (обычно) / Manual

##### Color / Gradient подробно

- **colorType**
  - `Gradient` — градиент по тексту
  - `BetweenTwoColors` — анимация между двумя hex
  - `OnlyAlpha` — только прозрачность
  - `ColorToOriginal` / `OriginalToColor`
- **orientation** (для Gradient): Horizontal / HorizontalPerCharacter / Vertical
- **stride** — шаг для per-character / vertical
- **start/end hex**, **start/end alpha**
- **gradientStops**: список `{ t: 0..1, hex: "#RRGGBB" }`  
  Пример радуги:
  - `0` → `#FF4D6D`
  - `0.33` → `#FFD166`
  - `0.66` → `#06D6A0`
  - `1` → `#4D9FFF`

##### Move / Rotate / Scale

- Move: `startOffset` / `endOffset` (X, Y в пикселях TMP)
- Rotate: `centerOffset`, `startAngle`, `endAngle` (градусы)
- Scale: `startScale`, `endScale`

##### Composite / Per vertex

- Composite: вложи несколько эффектов внутрь одного
- Per vertex: отдельные списки эффектов на каждый угол буквы

### Назначить пресет игроку

1. Вкладка **Players**
2. Колонка **Nick preset** → выбери имя
3. **Save**
4. Игрок **перезаходит** в игру

### Что нужно в Unity (один раз)

1. Меню **Kaplao → Setup Nickname Font Registry**
2. В ассете `Assets/Resources/KaplaoNicknameFontRegistry`:
   - список `{ name, font }`
   - fallback font / fallback color для `none`
3. Пакет Easy Text Effects подключён в `Packages/manifest.json`
4. Website URL + Game Server Secret совпадают с сайтом  
   (`KaplaoAccountConfig` / Public Level Catalog config)

Без совпадения secret/URL игра **не скачает** пресеты → все ники будут как `none`.

### Что больше не делается в Unity

- Нет настройки градиентов/скорости shimmer в `KaplaoPrivilegeConfig`
- Нет role presets (`mod` / `creator`) в инспекторе Unity
- Нет per-email «own» стилей в Unity

Всё это перенесено в админку.

### Типичный сценарий «радужный ник»

1. Unity: в реестр шрифтов добавить `name = gold`, шрифт = нужный TMP.
2. Админка → Nickname presets → Add `vip_rainbow`.
3. Edit style:
   - Font name: `gold`
   - Base color: `#FFFFFF`
   - Outline color: `#000000`, width: `0.25`
   - Effect Color: Gradient + stops радуги, PingPong, duration ~0.5
   - (опционально) Effect Move: лёгкий Y bounce
4. Save style.
5. Players → игроку Nick preset = `vip_rainbow` → Save.
6. Игрок перезаходит → на nameplate виден анимированный ник.

---

## Logs — логи

Показывает действия:

- login / logout / account created
- singleplayer world / server join
- level published / level deleted

Можно:

- обновлять список
- удалять отдельные записи
- задать **auto-delete** старых логов

Нужно право **Web Console**.

---

## Как это связано с игрой (технически)

| Действие | API / место |
|----------|-------------|
| Проверка аккаунта при входе в игру | `POST /api/game/verify-player` → `rights`, `isAdmin`, `nicknamePreset`, skins |
| Каталог стилей ников | `GET /api/game/nickname-presets` (game secret) |
| Админ: игроки | `/api/admin/players` |
| Админ: пресеты ников | `/api/admin/nickname-presets` (GET/POST/PUT/DELETE) |
| Кто я / есть ли Web Console | `/api/me` → `isSuperAdmin` = Web Console (или root) |

`isAdmin` в игре = есть право **Game Host**.  
Удаление уровней с серверов = **Level Purge** (или свой уровень).

---

## Миграции БД (если ещё не гонял)

В Supabase SQL Editor по порядку:

1. `supabase/migrations/20260905_nickname_preset_styles.sql`  
   — колонка `style` у nickname presets, дефолт ника `none`
2. `supabase/migrations/20260905_player_rights.sql`  
   — колонка `rights`, миграция со старых `admin` / `level_mod`

После миграций — задеплой сайт.  
Если используешь Edge Function `verify-player` — передеплой и её.

---

## Частые проблемы

**Ник без эффектов**

- Пресет `none` или пустые effects
- Игрок не перезашёл
- Не скачался каталог (неверный website URL / game secret)
- Font name не совпал с Unity → шрифт fallback, но эффекты всё равно должны работать

**Нет доступа в /admin**

- Нет права Web Console
- Не задеплоена новая логика прав / не прогнана миграция `rights`

**Level Purge не удаляет**

- Права нет, или это не свой уровень и нет Level Purge
- Нужен перезаход / повторная verify

**Game Host не работает в игре**

- Сохранили в админке, но игрок не перезашёл
- Host смотрит старый билд без новых полей verify

---

## Краткая шпаргалка по никам

1. Unity: зарегистрируй шрифты по **именам-строкам**.
2. Админка: создай пресет и **полностью** настрой эффекты + hex + font name.
3. Админка → Players: назначь пресет игроку.
4. Игрок перезаходит в игру.
5. Готово.

Больше ничего для визуала ника в Unity настраивать не нужно.
