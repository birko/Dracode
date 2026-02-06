# DraCode Troubleshooting Guide

This guide covers common issues for the DraCode Web Client, KoboldLair, and provider configuration.

---

## Quick Diagnostic Checklist

Before diving into specific issues:

1. Server running on appropriate port (check Aspire Dashboard)
2. Browser console open (F12 → Console tab)
3. No errors in console on page load
4. At least one provider configured in appsettings.json
5. Environment variables set for API keys

---

## WebSocket Connection Issues

### WebSocket Connection Fails

**Symptoms:**
- Status stays at "Disconnected"
- Console shows connection error
- No WebSocket in Network tab

**Solutions:**

1. **Verify server is running:**
   ```bash
   # Check Aspire Dashboard or terminal
   dotnet run --project DraCode.AppHost
   ```

2. **Check WebSocket URL:** Must be `ws://localhost:PORT/ws` (not `http://`)

3. **Test connection manually:**
   ```javascript
   // In browser console:
   new WebSocket('ws://localhost:5000/ws')
   ```

4. **Common fixes:**
   - Server not started: Run `dotnet run --project DraCode.WebSocket`
   - Wrong port: Verify port in console output
   - Firewall: Add exception for the port

### "Reconnecting..." Loop

**Symptoms:**
- WebSocket connects then immediately disconnects
- Status alternates between Connected and Disconnected

**Solutions:**

1. Check Server logs for rejection reasons
2. Verify authentication token matches server config
3. Increase reconnect delay in client code
4. Disable hot reload during testing (prevents server restarts)

---

## Provider Issues

### Network Errors Marking Tasks as Complete

**Symptoms:**
- Kobold tasks marked as "Done" despite network errors
- LLM provider logs show "Network error after X attempts"
- Task appears successful but no work was completed

**Cause:**
- Fixed in version 2.5.1
- Network errors after retry exhaustion were not properly detected

**Solution:**
- Update to version 2.5.1 or later
- Network errors now properly fail tasks with error status
- Applies to all LLM providers (OpenAI, Claude, Gemini, Z.AI, etc.)

### Provider List Not Showing

**Debug Steps:**

1. **Check Browser Console for:**
   ```
   ✨ DraCode Client initialized
   [SUCCESS] ✅ Connected to WebSocket server
   📋 Received providers: [Array of providers]
   ```

2. **Check Network Tab:**
   - Filter by "WS" (WebSocket)
   - Verify `{"command":"list"}` sent
   - Verify response with provider data

3. **Test with wscat:**
   ```bash
   wscat -c ws://localhost:5000/ws
   > {"command":"list"}
   # Should receive provider list
   ```

### "Found 0 configured provider(s)"

**Causes:**
- Incorrect casing in appsettings.json
- API key environment variable not set

**Solutions:**

1. **Fix casing in appsettings.json:**
   ```json
   {
     "Agent": {
       "Providers": {
         "openai": {
           "Type": "openai",      // PascalCase
           "ApiKey": "${...}",    // PascalCase
           "Model": "gpt-4o"      // PascalCase
         }
       }
     }
   }
   ```

2. **Set environment variables:**
   ```bash
   export OPENAI_API_KEY="sk-..."
   export ANTHROPIC_API_KEY="sk-ant-..."
   ```

3. **Restart server** after config changes

### API Key Not Found

**Symptoms:**
- Warnings about missing API keys in logs
- Provider shows as "not configured"

**Solutions:**

1. **Check environment variable:**
   ```bash
   # Windows PowerShell
   echo $env:OPENAI_API_KEY

   # Linux/macOS
   echo $OPENAI_API_KEY
   ```

2. **Restart terminal** after setting variables

3. **Variable name must match exactly** (case-sensitive on Linux/macOS)

---

## KoboldLair Dragon Issues

### "Error loading projects/providers"

**Symptoms:**
- Project dropdown shows "Error loading projects"
- Provider dropdown shows "Error loading providers"

**Solutions:**

1. **Check if Server is running** in Aspire Dashboard

2. **Test API endpoints:**
   ```bash
   curl http://localhost:{client-port}/api/projects
   curl http://localhost:{client-port}/api/providers
   ```

3. **Check browser console for:**
   - "Failed to fetch"
   - "net::ERR_CONNECTION_REFUSED"
   - "404 Not Found"

### Dragon WebSocket Connection Failed

**Symptoms:**
- Connection stuck on "Disconnected"
- Can select project/provider but chat doesn't start

**Solutions:**

1. **Check /api/config endpoint:**
   ```bash
   curl http://localhost:{client-port}/api/config
   # Should return JSON with serverUrl
   ```

2. **Test WebSocket manually:**
   ```javascript
   // In browser console:
   fetch('/api/config')
     .then(r => r.json())
     .then(config => {
       const ws = new WebSocket(config.serverUrl + '/dragon');
       ws.onopen = () => console.log('Connected!');
       ws.onerror = (e) => console.error('Error:', e);
     });
   ```

3. **Check authentication** - verify token in config

---

## Agent and Task Issues

### Tab Switching Not Working

**Symptoms:**
- Can't switch between agent tabs
- Wrong tab appears active

**Solutions:**

1. **Hard refresh:** Ctrl+Shift+R
2. **Disconnect all, then reconnect**
3. **Check button type:** Tabs should have `type="button"`

### Tasks Not Sending

**Symptoms:**
- Clicking "Send Task" does nothing
- No message in activity log

**Solutions:**

1. **Check agent is connected** (status shows "Connected")
2. **Check textarea has content**
3. **Check WebSocket state:**
   ```javascript
   window.draCodeClient.ws.readyState
   // Should be 1 (OPEN)
   ```

### Agent Prompts Not Appearing

**Solutions:**

1. **Hard refresh** page
2. **Check browser popup blocker**
3. **Verify modal element exists:**
   ```javascript
   document.getElementById('generalModal')
   ```

---

## Configuration Issues

### Wrong Environment

**Check current environment:**
```bash
echo $ASPNETCORE_ENVIRONMENT
# Should be: Development or Production
```

**Run with specific environment:**
```bash
dotnet run --environment Development
dotnet run --environment Production
```

### Providers Not Loading

1. Verify environment-specific config file exists
2. Check provider is enabled (`"IsEnabled": true`)
3. Review startup logs for provider list

---

## Performance Issues

### Slow Provider List Loading

**Solutions:**
- Use provider filter to show only configured
- Check server performance

### High Memory Usage

**Solutions:**
- Use "Clear Log" button periodically
- Disconnect unused agents
- Refresh page to clear memory

---

## Advanced Debugging

### Enable Verbose Logging

**Browser:**
```javascript
localStorage.setItem('debug', 'true');
// Reload page
```

**Server (appsettings.Development.json):**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Inspect WebSocket Messages

1. Open DevTools → Network tab
2. Filter by "WS"
3. Click WebSocket connection
4. View Messages tab

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
```

---

## Browser Compatibility

**Supported:**
- Chrome 90+
- Edge 90+
- Firefox 88+
- Safari 14+

**Known Issues:**
- Older browsers may not support ES2020 features
- Check browser console for syntax errors

---

## Full Reset

If nothing works:

```bash
# Stop all services (Ctrl+C)
cd C:\Source\DraCode
dotnet clean
dotnet build
dotnet run --project DraCode.AppHost

# Clear browser cache (Ctrl+Shift+Delete)
# Open fresh browser tab
```

---

## Error Messages Reference

| Error | Meaning | Fix |
|-------|---------|-----|
| "Not connected to server" | WebSocket not open | Click "Connect to Server" |
| "Element with id 'X' not found" | DOM element missing | Hard refresh (Ctrl+Shift+R) |
| "Agent not found" | Agent ID doesn't exist | Reconnect agent or refresh |
| "Tab name is required" | Empty tab name | Enter a name for connection |
| "Found 0 configured provider(s)" | Server not loading providers | Fix appsettings.json casing |

---

## Getting Help

If still stuck, provide:

1. Browser console output (full log)
2. Network tab WebSocket messages
3. Server terminal output
4. Output of: `dotnet --version`
5. Aspire Dashboard screenshot (showing service status)
6. Configuration from `/api/config` endpoint
