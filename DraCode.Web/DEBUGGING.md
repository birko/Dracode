# WebSocket Debugging Checklist

## Quick Test

1. **Open test page**: http://localhost:5001/test.html
2. Click "Connect" button
3. Click "List Providers" button
4. Check if you see the response in the log

If the test page works, the server is fine and the issue is in the main client.

## Step-by-Step Debugging

### Step 1: Check Server is Running
```bash
# Should show:
# Now listening on: http://localhost:5000
```

### Step 2: Test WebSocket Connection
Open browser console (F12) on http://localhost:5001 and check for:

```
✨ DraCode Client initialized
```

Then click "Connect to Server" and look for:

```
[SUCCESS] ✅ Connected to WebSocket server
🔌 Server connection status: true
✅ Provider section displayed: block
⏰ Auto-requesting provider list...
[INFO] 📋 Requesting provider list...
📤 Sending list command: {command: "list"}
```

### Step 3: Check WebSocket Messages
In Network tab → Filter "WS" → Click connection → Messages tab

**Expected:**
- ⬆️ Sent: `{"command":"list"}`
- ⬇️ Received: `{"status":"success","message":"Found X provider(s)","data":"[...]"}`

### Step 4: Check Message Handler
After receiving message, console should show:

```
📨 Received message: {status: "success", message: "...", data: "..."}
✅ Detected as provider list response
📋 Received providers: [{name: "openai", ...}, ...]
🎨 Displaying providers: [...]
✅ Provider cards added to grid. Grid element: <div id="providersGrid">
[SUCCESS] 📋 Found X providers
```

### Step 5: Verify DOM Elements
In console, run:

```javascript
// Check section visibility
document.getElementById('providersSection').style.display
// Should be: "block"

// Check grid has children
document.getElementById('providersGrid').children.length
// Should be: > 0

// Check provider section computed style
getComputedStyle(document.getElementById('providersSection')).display
// Should be: "block"
```

## Common Issues & Fixes

### Issue 1: No console logs at all
**Problem:** JavaScript not loading

**Check:**
```javascript
window.draCodeClient
// Should be: DraCodeClient instance
```

**Fix:** Hard refresh (Ctrl+Shift+R)

### Issue 2: "Detected as provider list response" not showing
**Problem:** Message detection failing

**Possible causes:**
- Server sending different message format
- Status field is different case
- Message missing required fields

**Check raw message:**
```javascript
// In handleServerMessage, check:
console.log('📨 Received message:', response);
```

### Issue 3: Provider cards not rendering
**Problem:** displayProviders() not working

**Check:**
```javascript
// In browser console:
document.getElementById('providersGrid').innerHTML
// Should contain provider-card divs
```

### Issue 4: WebSocket connection fails
**Problem:** Server not running or wrong URL

**Check:**
1. Server terminal shows "Now listening on: http://localhost:5000"
2. WebSocket URL is `ws://localhost:5000/ws` (not http://)
3. No firewall blocking port 5000

## Expected Full Console Output

When everything works, you should see:

```
✨ DraCode Client initialized
[SUCCESS] ✅ Connected to WebSocket server
🔌 Server connection status: true
✅ Provider section displayed: block
⏰ Auto-requesting provider list...
[INFO] 📋 Requesting provider list...
📤 Sending list command: Object { command: "list" }
📨 Received message: Object { status: "success", message: "Found 6 configured provider(s)", data: "[...]", agentId: null }
✅ Detected as provider list response
📋 Received providers: Array(6) [ {…}, {…}, {…}, {…}, {…}, {…} ]
🎨 Displaying providers: Array(6) [ {…}, {…}, {…}, {…}, {…}, {…} ]
✅ Provider cards added to grid. Grid element: <div id="providersGrid">
[SUCCESS] 📋 Found 6 providers
```

## Server-Side Check

If client shows correct logs but no providers, check server `appsettings.json`:

```json
{
  "Agent": {
    "Providers": {
      "openai": {
        "Type": "openai",
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o"
      }
      // ... more providers
    }
  }
}
```

Make sure at least one provider is configured!

## Still Not Working?

1. Copy ALL console output
2. Copy Network tab WebSocket messages
3. Copy server terminal output
4. Try the test page: http://localhost:5001/test.html
5. Share the outputs for further debugging
