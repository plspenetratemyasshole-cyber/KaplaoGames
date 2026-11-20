# Real-Time Player Data Sync Solution

## Problem
When an admin changes a player's nickname preset (color, effects, etc.) in the admin panel while the player is actively playing in a level, the changes don't take effect until the player leaves and rejoins the level. The player's display name reverts to plain white, ignoring all database updates.

## Root Cause
1. Player data (including `nicknamePreset`) is only loaded once during connection via `KaplaoAccountVerificationService.VerifyPlayerCoroutine()`
2. The game has no mechanism to refresh player data while they remain connected
3. When admin updates player settings in the database, connected players never get notified

## Solution Overview
Added a **real-time player data synchronization system** that:
1. Stores the player's auth token on the server during connection
2. Periodically (every 30 seconds by default) re-verifies connected players' data
3. Detects changes to `nicknamePreset`, admin status, permissions, and owned skins
4. Automatically updates these values via Network SyncVars, triggering client-side UI updates

## Changes Made

### 1. NetworkPlayer.cs
- **Added field**: `string serverAuthToken` - stores the auth token server-side (not synced to clients)
- **Updated method**: `ServerInit()` - now accepts `authToken` parameter

### 2. KaplaoNetworkManager.cs
- **Updated**: `SpawnPlayer()` method signature to accept auth token
- **Updated**: Call to `networkPlayer.ServerInit()` to pass the auth token
- **Updated**: `OnStartServer()` to initialize `PlayerDataSyncServer` component

### 3. PlayerDataSyncServer.cs (NEW)
A new server component that:
- Runs on a configurable interval (default: 30 seconds)
- Iterates through all connected players
- Re-verifies each player using their stored auth token
- Compares new data with current data and applies any changes
- Updates SyncVars which automatically notify clients

## How It Works

```
Flow Diagram:
┌─────────────────────────────────────────────────────────────┐
│ Admin Panel Updates Player Data in Admin Panel             │
│ (nickname preset, color, font, admin status, etc.)        │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
           Data saved to Supabase
                       │
                       ▼
         Every 30 seconds: PlayerDataSyncServer
         re-verifies all connected players via
         KaplaoAccountVerificationService.VerifyPlayerCoroutine()
                       │
                       ▼
         Detects changes in:
         - nicknamePreset
         - admin status
         - permissions
         - owned skins
                       │
                       ▼
         Updates NetworkPlayer SyncVars
                       │
                       ▼
         Mirror automatically sends updates to all clients
                       │
                       ▼
         PlayerNameplate.OnNicknamePresetChanged() triggered
         NicknameStyle.Apply() re-renders the player's nameplate
         with new colors, effects, and fonts
                       │
                       ▼
         Player sees updated nickname immediately
         WITHOUT leaving and rejoining the level
```

## Configuration

In the Unity Inspector, on the `PlayerDataSyncServer` component:
- **Refresh Interval Seconds**: How often to check for updates (default: 30)
- **Debug Logging**: Enable to see sync events in the console

## Testing the Fix

1. Start a multiplayer game with a player signed into their account
2. Leave the game running (don't close it)
3. Open the admin panel in the web browser
4. Find the player by email and change their nickname preset to something different
5. Click "Save Player" in the admin panel
6. Within 30 seconds, the player's nickname in the running game will update with the new color/effects
7. No level reload required - the change appears in real-time

## Performance Considerations

- **Auth token storage**: Minimal - just storing the auth token once per player
- **Network traffic**: Minimal - only sends updates when something actually changed
- **Verification calls**: Reuses existing `KaplaoAccountVerificationService` - hits the same `/verify-player` endpoint used during login
- **CPU cost**: Lightweight - just comparing strings, happens every 30 seconds
- **Can be tuned**: Interval is configurable (increase to 60+ seconds for more relaxed polling)

## Future Enhancements

Possible optimizations:
1. **Webhook-based**: Admin panel sends HTTP POST to game server immediately instead of polling
2. **Batch verification**: Query admin database directly instead of going through verify service
3. **Conditional verification**: Only re-verify players who have accounts, skip local players
4. **Database polling**: Have server periodically query Supabase directly (if DB connection available)
5. **Exponential backoff**: Reduce verification frequency for long-connected players, increase for suspected changes

## Files Changed

- `Assets/Scripts/Network/NetworkPlayer.cs` - Added serverAuthToken storage
- `Assets/Scripts/Network/KaplaoNetworkManager.cs` - Updated player spawn logic and server initialization
- `Assets/Scripts/Server/PlayerDataSyncServer.cs` - New component for real-time sync (NEW)

## Backwards Compatibility

✅ Fully backwards compatible:
- Old builds without `authToken` parameter still work (defaults to null)
- PlayerDataSyncServer is optional and auto-creates if missing
- No changes to client-server protocol beyond passing auth token once at join
