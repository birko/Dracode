# Provider Grid Not Displaying - Troubleshooting Guide

## 🔧 Quick Fixes to Try

### Fix 1: Restart the Server
The appsettings.json casing was fixed (lowercase→PascalCase). Restart the server:

```bash
# Stop current server (Ctrl+C)
dotnet run --project DraCode.AppHost
```

### Fix 2: Use Diagnostic Tools
Try these diagnostic pages:

1. **http://localhost:5001/diagnostic.html** - Full diagnostic tool
   - Tests WebSocket connection
   - Shows provider data
   - Checks DOM elements
   - Inspects client object

2. **http://localhost:5001/test.html** - Simple WebSocket test
   - Minimal test client
   - Raw message display

### Fix 3: Browser Console Commands

Open http://localhost:5001 and press F12 (DevTools), then in Console:

```javascript
// Force show provider section
debugShowProviders()

// Check DOM elements
debugCheckElements()

// Manually trigger list
draCodeClient.listProviders()

// Check what providers were received
draCodeClient.availableProviders
```

## 📋 Step-by-Step Debugging

### Step 1: Check Server Configuration

**File:** `DraCode.WebSocket/appsettings.json`

Verify property names are PascalCase:
```json
{
  "Agent": {
    "Providers": {
      "openai": {
        "Type": "openai",        ← MUST be capital T
        "ApiKey": "${...}",      ← MUST be capital A and K
        "Model": "gpt-4o"        ← MUST be capital M
      }
    }
  }
}
```

**If lowercase** (type, apiKey, model): Server won't load providers!

### Step 2: Verify Server Loads Providers

In server terminal, look for startup logs. No errors about providers?

Test with curl/wscat:
```bash
wscat -c ws://localhost:5000/ws
> {"command":"list"}

# Should receive:
# {"status":"success","message":"Found 6 configured provider(s)","data":"[...]"}
```

If you get "Found 0 configured provider(s)", the server isn't loading them!

### Step 3: Check Browser Console

After clicking "Connect to Server", you should see:

```
✨ DraCode Client initialized
💡 Debug commands available:
   debugShowProviders() - Force show provider section
   debugCheckElements() - Check DOM elements
   draCodeClient - Access client instance
[SUCCESS] ✅ Connected to WebSocket server
🔌 Server connection status: true
✅ Provider section displayed: block
⏰ Auto-requesting provider list...
📤 Sending list command: {command: "list"}
📨 Received message: {...}
✅ Detected as provider list response     ← KEY MESSAGE
📋 Received providers: [...]
🎨 Displaying providers: [...]
✅ Provider cards added to grid
```

**Missing any of these?** That's where the problem is!

### Step 4: Check Network Tab

DevTools → Network → Filter "WS" → Click WebSocket connection → Messages tab

**Expected:**
- ⬆️ Sent: `{"command":"list"}`
- ⬇️ Received: `{"status":"success","message":"Found 6 configured provider(s)","data":"[{\"name\":\"openai\",...}]"}`

### Step 5: Manual DOM Inspection

In browser console:

```javascript
// Check section visibility
document.getElementById('providersSection').style.display
// Should be: "block"

// Check grid contents
document.getElementById('providersGrid').innerHTML
// Should contain: <div class="provider-card">...

// Count provider cards
document.getElementById('providersGrid').children.length
// Should be: > 0 (6 expected)

// Check computed styles
getComputedStyle(document.getElementById('providersSection')).display
// Should be: "block"
```

## 🐛 Common Issues & Solutions

### Issue 1: "Found 0 configured provider(s)"
**Problem:** Server not loading providers from appsettings.json

**Solutions:**
1. Fix casing in appsettings.json (Type, ApiKey, Model - not type, apiKey, model)
2. Restart server
3. Check file is saved
4. Verify JSON is valid (no syntax errors)

### Issue 2: No "✅ Detected as provider list response" message
**Problem:** Message detection logic not matching

**Check:**
```javascript
// In console after receiving message:
// The response should have:
// - status: "success"
// - message: contains "provider" or "configured"
// - data: exists and is a string
```

**Solution:** Message might be malformed. Check Network tab for exact response.

### Issue 3: Provider section stays hidden
**Problem:** CSS or JavaScript not setting display

**Force show:**
```javascript
debugShowProviders()
```

**Check:**
```javascript
document.getElementById('providersSection').style.display = 'block'
// Manually set it
```

### Issue 4: Grid exists but empty
**Problem:** displayProviders() not running or failing

**Check:**
```javascript
// In console:
draCodeClient.availableProviders
// Should show array of providers

// Manually trigger display:
draCodeClient.displayProviders(draCodeClient.availableProviders)
```

## 🎯 Expected Final State

After successful connection:

1. **Status badge:** 🟢 Status: Connected to Server
2. **Buttons:** "Disconnect" enabled, "List Providers" enabled
3. **Provider section:** Visible
4. **Provider grid:** Contains 6 provider cards
5. **Each card shows:** Name, model, configuration status
6. **Cards are clickable:** Can click to connect

## 🔍 Use the Diagnostic Tool

http://localhost:5001/diagnostic.html provides:

1. **Step 1:** Test WebSocket connection
2. **Step 2:** Request provider list and see response
3. **Step 3:** Check all DOM elements
4. **Step 4:** Inspect client object

Run through all 4 steps to pinpoint the exact issue!

## 📞 If Still Stuck

Share the following:

1. **Server terminal output** (when starting)
2. **Browser console output** (full log after connecting)
3. **Network tab WebSocket messages** (sent/received)
4. **Diagnostic tool results** (all 4 steps)
5. **`appsettings.json` content** (provider section)

---

**Most Common Fix:** Restart server after appsettings.json casing fix!
