# Developer Chat - Unity Implementation

## Setup Instructions

### 1. Create Developer Chat UI

1. Open your Unity scene (usually Main Menu or Game scene)
2. In Unity Editor, go to menu: **Kaplao → Setup → Create Developer Chat UI**
3. This will create a Canvas with all necessary UI elements

### 2. Configure Open Buttons

After creating the UI, you need to assign the open buttons:

1. Select the **DeveloperChatCanvas** GameObject in hierarchy
2. In the Inspector, find the **Developer Chat Manager** component
3. Assign these fields:
   - **Open Button Pause**: Drag the button from your `PauseMenuRoot` that should open the chat
   - **Open Button Runner**: Drag the button from your `RunnerPauseMenuRoot` that should open the chat

**Example:** If you have a "Help" or "Contact Support" button in your pause menu, drag it to these fields.

### 3. Verify Settings

Check the **Settings** section in the Inspector:
- **API Base URL**: Should be `https://kaplao.ru` (or your custom domain)
- **Poll Interval**: `10` seconds (default) - how often to check for new messages
- **Max Message Length**: `5000` characters (default)

### 4. Test in Play Mode

1. Start Play Mode in Unity
2. Make sure you're logged in (use the account system)
3. Click your assigned "Contact Developer" button
4. The chat overlay should appear
5. Try sending a message

## How It Works

### For Players:

1. **Login Required**: Players MUST be logged in to use the chat
   - If not logged in, nothing happens when clicking the button
   - Check console for warning: "Player must be logged in"

2. **Opening Chat**: 
   - Click the button you assigned (from pause menu)
   - Semi-transparent overlay appears with chat window

3. **Sending Messages**:
   - Type message in input field (max 5000 characters)
   - Press Enter or click Send button
   - Message appears immediately in the chat

4. **Receiving Replies**:
   - Chat polls server every 10 seconds for new messages
   - Admin replies appear with blue background
   - Shows admin nickname: "AdminName (Dev)"

5. **Closing Chat**:
   - Click X button in top-right corner
   - Click anywhere on the semi-transparent background

### For Developers:

- Messages from players appear in the **Admin Panel** on the website
- Reply through the admin panel → reply syncs to game automatically
- Player sees your nickname when you reply

## Architecture

### Components:

**DeveloperChatManager.cs**
- Main script controlling the chat
- Handles API calls, message polling, UI updates
- Located on the DeveloperChatCanvas GameObject

**DeveloperChatSetup.cs** (Editor script)
- Creates the UI hierarchy automatically
- Located in `Assets/Scripts/Editor/`

### API Endpoints Used:

- `POST /api/dev-chat/send` - Send message from player
- `GET /api/dev-chat/messages` - Get all messages in conversation
- Uses `PlayerSession.AuthToken` for authentication

### Message Polling:

- Polls every 10 seconds when chat is open
- Immediately polls when chat first opens
- Stops polling when chat is closed
- Auto-scrolls to bottom when new messages arrive

## UI Structure

```
DeveloperChatCanvas (Canvas)
└── ChatRoot (hidden by default)
    ├── BackgroundPanel (semi-transparent overlay)
    └── ChatPanel (main chat window)
        ├── Header
        │   ├── Title ("Contact Developer")
        │   └── CloseButton
        ├── MessageScrollView
        │   └── Viewport
        │       └── Content (messages container)
        │           └── MessageTemplate (hidden, cloned for each message)
        ├── InputField (text input)
        └── SendButton
```

## Customization

### Changing Chat Size:

1. Select `ChatPanel` in hierarchy
2. Adjust **Rect Transform → Size Delta**
3. Default: 800x600

### Changing Colors:

1. Select UI element (e.g., SendButton)
2. Adjust **Image → Color** in Inspector
3. Admin messages: `#1890FF` (blue)
4. Player messages: `#666666` (gray)

### Changing Poll Interval:

1. Select `DeveloperChatCanvas`
2. Adjust **Poll Interval** in Inspector
3. Lower = more real-time, higher = less server load

## Troubleshooting

### Chat doesn't open when clicking button:

- **Check**: Is player logged in? Look for console warning.
- **Check**: Did you assign the open button in Inspector?
- **Check**: Is the button's onClick event working?

### Messages not sending:

- **Check**: Network connectivity
- **Check**: Console for error messages
- **Check**: API base URL is correct (`https://kaplao.ru`)
- **Check**: Player session token is valid

### Messages not updating:

- **Check**: Poll interval setting (default 10s)
- **Check**: Network connectivity
- **Check**: Console for API errors

### UI looks wrong:

- **Recreate**: Delete `DeveloperChatCanvas` and use menu to recreate
- **Check**: Canvas Scaler settings (should be Screen Space Overlay)
- **Check**: Sort Order = 100 (so it appears on top)

## Technical Details

### Authentication:

Uses `PlayerSession.AuthToken` from KaplaoAccountClient:
```csharp
if (!Shared.PlayerSession.IsLoggedIn)
{
    Debug.LogWarning("Player must be logged in");
    return;
}

string token = Shared.PlayerSession.AuthToken;
```

### Message Format:

```json
{
  "id": "uuid",
  "senderEmail": "player@example.com",
  "senderNickname": "PlayerName",
  "message": "Message text",
  "isFromAdmin": false,
  "adminNickname": "AdminName",
  "createdAt": "2026-09-06T19:00:00Z",
  "readByAdmin": false,
  "readByPlayer": true
}
```

### Security:

- All API calls include Bearer token authentication
- Server validates token and returns only player's own messages
- Admin replies require `web_console` right on server

## Best Practices

1. **Place in DontDestroyOnLoad**: If you want chat to persist across scenes:
   ```csharp
   DontDestroyOnLoad(DeveloperChatCanvas);
   ```

2. **Test with real account**: Create a test account and log in to verify

3. **Monitor console**: Check for network errors and warnings

4. **Rate limiting**: Don't change poll interval to less than 5 seconds

5. **Message length**: Current limit is 5000 chars, enforce on client

## Future Enhancements

Potential features to add:
- Timestamps on messages
- Read receipts (show when admin has read)
- Typing indicators
- File attachments
- Message history pagination
- Offline message queue
