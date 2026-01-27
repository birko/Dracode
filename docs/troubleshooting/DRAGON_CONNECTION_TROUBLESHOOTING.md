# Dragon Connection Troubleshooting

This guide helps troubleshoot connection issues with the Dragon page in KoboldLair.

## Common Issues

### 1. "Error loading projects/providers"

**Symptoms:**
- Project dropdown shows "Error loading projects"
- Provider dropdown shows "Error loading providers"
- Browser console shows fetch errors

**Causes:**
- KoboldLair Server not running
- API proxy not working
- CORS issues

**Solutions:**

1. **Check if Server is running:**
   ```bash
   # In Aspire Dashboard, verify dracode-koboldlair-server is running
   # Check the status - it should show a green checkmark
   ```

2. **Check browser console (F12):**
   ```
   Look for errors like:
   - "Failed to fetch"
   - "net::ERR_CONNECTION_REFUSED"
   - "404 Not Found"
   ```

3. **Test API endpoints directly:**
   ```bash
   # Get the Client URL from Aspire Dashboard (e.g., http://localhost:12345)
   curl http://localhost:{client-port}/api/projects
   curl http://localhost:{client-port}/api/providers
   ```

4. **Verify service discovery:**
   - Check Client logs for "Service discovery" messages
   - Ensure `builder.AddServiceDefaults()` is called in Client
   - Verify `WithReference(koboldlairServer)` in AppHost

### 2. "Having error connecting to the dragon" / WebSocket Connection Failed

**Symptoms:**
- Connection status stuck on "Disconnected"
- Browser console shows WebSocket errors
- Can select project/provider but chat doesn't start

**Causes:**
- Wrong WebSocket URL
- Server WebSocket endpoint not available
- Authentication issues

**Solutions:**

1. **Check browser console (F12) for:**
   ```javascript
   Configuration loaded: { serverUrl: "ws://...", ... }
   Connecting to WebSocket: ws://...
   WebSocket error: ...
   ```

2. **Verify configuration endpoint:**
   ```bash
   # Should return JSON with serverUrl, apiUrl, etc.
   curl http://localhost:{client-port}/api/config
   ```

3. **Check Server WebSocket endpoint:**
   ```bash
   # In Server Program.cs, verify:
   app.Map("/dragon", async context => { ... });
   ```

4. **Test WebSocket connection manually:**
   ```javascript
   // In browser console:
   fetch('/api/config')
     .then(r => r.json())
     .then(config => {
       console.log('Config:', config);
       const ws = new WebSocket(config.serverUrl + '/dragon');
       ws.onopen = () => console.log('Connected!');
       ws.onerror = (e) => console.error('Error:', e);
     });
   ```

5. **Check authentication:**
   - Verify `authToken` in config (empty if auth disabled)
   - Check Server's `WebSocketAuthenticationService`
   - Look for 401 Unauthorized errors

### 3. Configuration Not Loading

**Symptoms:**
- Dragon page shows no errors but doesn't connect
- Console shows "Configuration loaded: { serverUrl: 'ws://localhost:5000', ... }"
- URL is hardcoded fallback instead of dynamic

**Causes:**
- `/api/config` endpoint failing
- Service discovery not resolving server URL

**Solutions:**

1. **Check if /api/config returns data:**
   ```bash
   curl http://localhost:{client-port}/api/config
   # Should return: {"serverUrl":"ws://...","apiUrl":"http://...","authToken":"","endpoints":{...}}
   ```

2. **Check Client logs:**
   ```
   Look for errors in Client console output:
   - Service discovery errors
   - HttpClient creation errors
   ```

3. **Verify HttpClient configuration:**
   ```csharp
   // In Client Program.cs:
   builder.Services.AddHttpClient("KoboldLairServer", client => {
       client.BaseAddress = new Uri("http://dracode-koboldlair-server");
   })
   .AddServiceDiscovery();
   ```

### 4. "Reconnecting..." Loop

**Symptoms:**
- WebSocket connects then immediately disconnects
- Status alternates between Connected and Disconnected
- Console shows repeated connection attempts

**Causes:**
- Server rejecting connection
- Network issues
- Server restarting

**Solutions:**

1. **Check Server logs:**
   ```
   Look for:
   - "Dragon session started: {SessionId}"
   - "Dragon session ended: {SessionId}"
   - Error messages
   ```

2. **Check for Server restarts:**
   - Server might be restarting due to file changes
   - Disable hot reload during testing

3. **Increase reconnect delay:**
   ```javascript
   // In dragon.js, onclose handler:
   setTimeout(() => this.connect(), 5000); // Increase from 3000
   ```

## Diagnostic Checklist

Before asking for help, please check:

- [ ] Both KoboldLair Server and Client are running in Aspire Dashboard
- [ ] Server shows as "Running" with green status
- [ ] Client shows as "Running" with green status
- [ ] Browser console (F12) is open and showing messages
- [ ] Network tab shows requests to `/api/config`, `/api/projects`, `/api/providers`
- [ ] Configuration loaded message shows correct URLs (not localhost:5000)
- [ ] No CORS errors in console
- [ ] No 404, 500, or other HTTP errors

## Debug Mode

Enable detailed logging:

1. **Browser Console:**
   ```javascript
   // In browser console:
   localStorage.setItem('debug', 'true');
   // Reload page
   ```

2. **Server Logging:**
   ```json
   // In appsettings.Development.json:
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug",
         "DraCode.KoboldLair.Server": "Trace"
       }
     }
   }
   ```

3. **Network Logging:**
   - Open Browser DevTools (F12)
   - Go to Network tab
   - Filter by "Fetch/XHR" and "WS"
   - Watch for failed requests

## Expected Behavior

When everything works correctly:

1. **Page Load:**
   - Console: "Configuration loaded: { serverUrl: 'ws://localhost:XXXXX', ... }"
   - Projects dropdown populated
   - Providers dropdown populated

2. **After Selecting Project/Provider:**
   - "Start Chat" button enabled
   - Click shows chat interface

3. **Connection:**
   - Console: "Connecting to WebSocket: ws://localhost:XXXXX/dragon"
   - Status changes to "Connected" (green)
   - Welcome message from Dragon appears

4. **Chat:**
   - Can type messages
   - Dragon responds
   - Typing indicator shows while waiting

## Getting Help

If you're still stuck, provide:

1. **Browser Console Output:** (Full text from F12 Console tab)
2. **Network Tab:** (Screenshot showing failed requests)
3. **Server Logs:** (Copy from Aspire Dashboard -> Server -> Logs)
4. **Client Logs:** (Copy from Aspire Dashboard -> Client -> Logs)
5. **Aspire Dashboard Screenshot:** (Showing service status)
6. **Configuration:** (From `/api/config` endpoint)

## Related Documentation

- [Dragon Project Selection Feature](../DRAGON_PROJECT_SELECTION.md)
- [KoboldLair API Proxy](../KOBOLDLAIR_API_PROXY.md)
- [WebSocket Authentication](../../DraCode.KoboldLair.Server/README.md)
