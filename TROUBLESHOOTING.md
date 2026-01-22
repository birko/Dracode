# Troubleshooting Guide - Provider List Not Showing

## Issue
When connecting to the WebSocket server, the available providers list doesn't display.

## Debug Steps

### 1. Check Browser Console
Open browser DevTools (F12) and check the Console tab for these messages:

**Expected messages after connecting:**
```
✨ DraCode Client initialized
[SUCCESS] ✅ Connected to WebSocket server
[INFO] 📋 Requesting provider list...
📨 Received message: {status: "success", message: "Found X provider(s)", data: "[...]"}
📋 Received providers: [Array of providers]
🎨 Displaying providers: [Array of providers]
✅ Provider cards added to grid. Grid element: <div id="providersGrid">
[SUCCESS] 📋 Found X providers
```

### 2. Check Network Tab
In DevTools Network tab:
- Filter by "WS" (WebSocket)
- Click on the WebSocket connection
- Check "Messages" tab
- Verify you see:
  - Sent: `{"command":"list"}`
  - Received: Provider list JSON response

### 3. Check WebSocket Server
Ensure the WebSocket server is running and responding:

```bash
# Terminal 1: Start WebSocket server
dotnet run --project DraCode.WebSocket

# Should see:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://localhost:5000
```

Test with wscat:
```bash
wscat -c ws://localhost:5000/ws
> {"command":"list"}
# Should receive JSON response with providers
```

### 4. Check Element Visibility
In DevTools Console, run:
```javascript
// Check if element exists
console.log(document.getElementById('providersSection'));

// Check display style
console.log(document.getElementById('providersSection').style.display);

// Check if hidden by CSS
console.log(getComputedStyle(document.getElementById('providersSection')).display);

// Check grid
console.log(document.getElementById('providersGrid').children.length);
```

### 5. Common Issues

#### Issue: providersSection is hidden
**Symptom:** Element exists but `display: none`

**Fix:** Click "Connect to Server" button again. The section should show after connection.

#### Issue: No provider cards in grid
**Symptom:** `providersGrid.children.length === 0`

**Possible causes:**
1. Server didn't send provider list
2. Response format is wrong
3. JavaScript error during rendering

**Check server logs:**
```bash
# In WebSocket server terminal
# Should see log when list command received
```

#### Issue: Server not responding
**Symptom:** No messages in Network tab

**Fix:**
1. Check server is running on port 5000
2. Check CORS is enabled
3. Verify WebSocket URL: `ws://localhost:5000/ws` (not `http://`)

### 6. Manual Fix

If automatic listing doesn't work, try manual list:

1. Connect to server
2. Click "List Providers" button manually
3. Check console for debug output

### 7. Server Configuration

Ensure `appsettings.json` has providers configured:

```json
{
  "Agent": {
    "Providers": {
      "openai": {
        "Type": "openai",
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o"
      }
    }
  }
}
```

### 8. Full Reset

If nothing works:

```bash
# Stop all services
# Clear browser cache (Ctrl+Shift+Delete)
# Rebuild

cd C:\Source\DraCode
dotnet clean
dotnet build
dotnet run --project DraCode.AppHost

# Open fresh browser tab: http://localhost:5001
```

## Expected Behavior

After clicking "Connect to Server":
1. ✅ Status changes to "Connected"
2. ✅ Provider section appears
3. ✅ After ~500ms, provider cards appear in grid
4. ✅ Console shows debug messages
5. ✅ Can click provider cards to connect

## Still Not Working?

Share the following:
1. Browser console output (full log)
2. Network tab WebSocket messages
3. Server terminal output
4. Output of: `dotnet --version` and `node --version`
