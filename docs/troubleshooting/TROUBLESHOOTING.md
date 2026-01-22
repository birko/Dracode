# DraCode Web Client Troubleshooting Guide

This guide helps you diagnose and fix common issues with the DraCode WebSocket web client.

---

## 🔍 Quick Diagnostic Checklist

Before diving into specific issues, run this quick check:

1. ✅ Server running on http://localhost:5000
2. ✅ Web client accessible at http://localhost:5001
3. ✅ Browser console open (F12 → Console tab)
4. ✅ No errors in console on page load
5. ✅ At least one provider configured in appsettings.json

---

## 🐛 Common Issues

### Issue 1: Provider List Not Showing

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

Share the following for further help:
1. Browser console output (full log)
2. Network tab WebSocket messages
3. Server terminal output
4. Output of: `dotnet --version` and `node --version`

---

## Issue 2: WebSocket Connection Fails

### Symptoms
- Status stays at "Disconnected"
- Console shows connection error
- No WebSocket in Network tab

### Diagnostic Steps

**Step 1: Verify server is running**
```bash
# Check terminal shows:
# Now listening on: http://localhost:5000
```

**Step 2: Check WebSocket URL**
- Must be `ws://localhost:5000/ws` (not `http://`)
- Check input field has correct URL

**Step 3: Test with browser console**
```javascript
new WebSocket('ws://localhost:5000/ws')
// If error, server issue. If opens, client issue.
```

### Common Fixes

1. **Server not started**: Run `dotnet run --project DraCode.WebSocket`
2. **Wrong port**: Check server is on 5000, not 5001
3. **Firewall blocking**: Add exception for port 5000
4. **CORS issue**: Check server has CORS enabled

---

## Issue 3: Tab Switching Not Working

### Symptoms
- Can't switch between agent tabs
- Clicking tab does nothing
- Wrong tab appears active

### Diagnostic Steps

**Check console for errors:**
```javascript
// Should see when clicking tab:
🖱️ Tab clicked: [name] agentId: [id]
🔄 Switching to agent: [id]
✅ Agent found: [name]
```

**Verify tab elements:**
```javascript
// In console:
document.querySelectorAll('.tab').length
// Should match number of connections

document.querySelectorAll('.tab.active').length
// Should be 1
```

### Common Fixes

1. **Hard refresh**: Ctrl+Shift+R to reload JavaScript
2. **Clear all agents**: Disconnect all, then reconnect
3. **Check button type**: Tabs should have `type="button"`

---

## Issue 4: Tasks Not Sending

### Symptoms
- Clicking "Send Task" does nothing
- No message in activity log
- Console shows no errors

### Diagnostic Steps

**Step 1: Check agent is connected**
```javascript
// Status should be "Connected"
// Tab should exist in agent tabs section
```

**Step 2: Check textarea has content**
```javascript
document.querySelector('#task-[agentId]').value
// Should not be empty
```

**Step 3: Check WebSocket state**
```javascript
window.draCodeClient.ws.readyState
// Should be 1 (OPEN)
```

### Common Fixes

1. **Not connected**: Connect agent first
2. **Empty task**: Enter task text
3. **WebSocket closed**: Reconnect to server

---

## Issue 5: Agent Prompts Not Appearing

### Symptoms
- Agent asks question but no modal appears
- Task hangs at "waiting for response"
- Console shows prompt message

### Diagnostic Steps

**Check modal element:**
```javascript
document.getElementById('generalModal')
// Should exist

document.getElementById('generalModal').classList.contains('active')
// Should be true when prompt shown
```

**Check for JavaScript errors:**
- Look for errors in console when prompt should appear

### Common Fixes

1. **Modal CSS not loaded**: Hard refresh
2. **Modal blocked by popup blocker**: Check browser settings
3. **Click outside modal**: Close and try again

---

## Issue 6: Provider Connection Count Wrong

### Symptoms
- Shows wrong number of connections
- Count doesn't update after connect/disconnect

### Diagnostic Steps

**Check provider filter:**
- Verify correct filter selected (Configured/All/Not Configured)
- Check if provider is configured in appsettings.json

**Refresh provider list:**
- Click "List Providers" button
- Should trigger update

### Common Fixes

1. **Click "List Providers"** after each connect/disconnect
2. **Check appsettings.json** has provider with valid API key
3. **Hard refresh** if count still wrong

---

## Advanced Debugging

### Enable Verbose Console Logging

The client already includes extensive logging. Check console for:
- 🖱️ User interactions
- 📤 Outgoing messages
- 📨 Incoming messages
- ✅/❌ Success/failure indicators

### Inspect WebSocket Messages

1. Open DevTools → Network tab
2. Filter by "WS"
3. Click WebSocket connection
4. View Messages tab
5. Check sent/received messages match expected format

### Check DOM State

```javascript
// Get client instance
window.draCodeClient

// Check connection state
window.draCodeClient.ws?.readyState
// 0=CONNECTING, 1=OPEN, 2=CLOSING, 3=CLOSED

// Check active agents
window.draCodeClient.agents.size

// Get active agent ID
window.draCodeClient.activeAgentId

// List all agent IDs
Array.from(window.draCodeClient.agents.keys())
```

### Server-Side Debugging

Check `DraCode.WebSocket` terminal for:
- Connection/disconnection logs
- Command received logs
- Error messages

---

## Browser Compatibility

**Supported Browsers:**
- ✅ Chrome 90+
- ✅ Edge 90+
- ✅ Firefox 88+
- ✅ Safari 14+

**Known Issues:**
- Older browsers may not support ES2020 features
- Check browser console for syntax errors

---

## Performance Issues

### Slow Provider List Loading

**Causes:**
- Many providers configured
- Slow server response

**Solutions:**
- Use provider filter to show only configured
- Check server performance

### High Memory Usage

**Causes:**
- Many agents with large logs
- Long-running sessions

**Solutions:**
- Use "Clear Log" button periodically
- Disconnect unused agents
- Refresh page to clear memory

---

## Error Messages Reference

### "Not connected to server"
**Meaning:** WebSocket not open  
**Fix:** Click "Connect to Server"

### "Element with id 'X' not found"
**Meaning:** DOM element missing  
**Fix:** Hard refresh (Ctrl+Shift+R)

### "Agent not found"
**Meaning:** Agent ID doesn't exist  
**Fix:** Reconnect agent or refresh page

### "Tab name is required"
**Meaning:** Empty tab name in connection modal  
**Fix:** Enter a name for the connection

---
