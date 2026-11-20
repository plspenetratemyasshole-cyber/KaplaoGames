# Nickname presets (Easy Text Effects)

Полная документация админки (в т.ч. ники): [`../KaplaoGamesWebsite/ADMIN.md`](../../KaplaoGamesWebsite/ADMIN.md)

Nickname visuals are configured on the **website admin** (Nickname presets tab).  
Unity only maps **font names** → TMP font assets.


## Unity (one-time)

1. Menu: **Kaplao → Setup Nickname Font Registry**
2. In `Assets/Resources/KaplaoNicknameFontRegistry`, add entries:
   - `name` — string you will type in admin (e.g. `fancy`)
   - `font` — TMP Font Asset
3. Optional: set fallback font / default color for preset `none`
4. Ensure package `com.qiaozhilei.easy-text-effects` is in `Packages/manifest.json`
5. Game server secret + website URL must match the site (`KaplaoAccountConfig` / catalog config) so the game can download presets from `/api/game/nickname-presets`

## Website / DB

1. Run migration `supabase/migrations/20260905_nickname_preset_styles.sql` in Supabase SQL Editor  
   (adds `style` jsonb, default preset `none`, seeds empty `mod`/`creator` if needed)
2. Deploy the Next.js site (admin + `/api/game/nickname-presets` + verify-player)
3. If you use the Supabase Edge `verify-player` function, redeploy it too (`DEFAULT_NICKNAME_PRESET = none`)

## Admin workflow

1. Open admin → **Nickname presets**
2. Add a preset name (e.g. `rainbow`)
3. **Edit style**:
   - Font name string (must match Unity registry)
   - Base color hex
   - Effects: color/gradient, move, rotate, scale, composite, per-vertex (+ timing)
4. Players tab: assign that preset to a player
5. Player rejoins a game — nameplate loads the preset catalog and applies Easy Text Effects

Built-in preset **`none`** = plain text (no effects). Legacy **`own`** is treated as **`none`**.
