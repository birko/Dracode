# Dragon Page - Quick Reference

## User Workflow

1. **Open Dragon Page** → Shows project setup screen
2. **Select/Create Project** → Choose existing or enter new name
3. **Select Provider** → Choose AI provider (e.g., OpenAI GPT-4)
4. **Start Chat** → Button enables when both selections made
5. **Chat with Dragon** → Discuss requirements
6. **Change Project** → Click button in header to switch

## Architecture

```
┌─────────┐     HTTP      ┌─────────┐    Service    ┌─────────┐
│ Browser │ ────────────► │ Client  │  Discovery   │ Server  │
│         │               │ (Proxy) │ ────────────► │         │
│         │ ◄──────────── │         │               │         │
└─────────┘               └─────────┘               └─────────┘
   │                                                      │
   │ WebSocket (dynamic URL)                             │
   └─────────────────────────────────────────────────────┘
```

## Key Components

### Frontend (dragon.js)
- Loads configuration from `/api/config` on startup
- Fetches projects from `/api/projects`
- Fetches providers from `/api/providers`
- Connects to WebSocket using dynamic URL

### Client Proxy (Program.cs)
- `/api/config` → Returns server URLs (resolved by service discovery)
- `/api/{**path}` → Proxies to Server
- WebSocket → Direct to Server (no proxy)

### Server (Program.cs)
- `/api/projects` → List projects
- `/api/providers` → List LLM providers
- `/dragon` WebSocket → Dragon chat

## Debugging

**Open Browser Console (F12) and look for:**

✅ **Success:**
```
Configuration loaded: { serverUrl: "ws://localhost:12345", ... }
Connecting to WebSocket: ws://localhost:12345/dragon
```

❌ **Failure:**
```
Failed to load projects: TypeError: Failed to fetch
WebSocket connection failed
```

## Common Issues

| Issue | Check | Fix |
|-------|-------|-----|
| Projects not loading | Server running? | Start Server in Aspire Dashboard |
| Connection error | Config loaded? | Check `/api/config` endpoint |
| Wrong WebSocket URL | Using localhost:5000? | Ensure config loads dynamically |
| CORS errors | Requests going direct to Server? | Should go through Client proxy |

## Testing

```bash
# 1. Test configuration endpoint
curl http://localhost:{client-port}/api/config

# 2. Test projects endpoint  
curl http://localhost:{client-port}/api/projects

# 3. Test providers endpoint
curl http://localhost:{client-port}/api/providers

# 4. Check WebSocket (in browser console)
fetch('/api/config')
  .then(r => r.json())
  .then(c => new WebSocket(c.serverUrl + '/dragon'))
```

## File Locations

```
DraCode.KoboldLair.Client/
  ├── wwwroot/
  │   ├── dragon.html        ← UI with project selection
  │   ├── css/dragon.css     ← Styling
  │   └── js/
  │       ├── config.js      ← Static config (fallback)
  │       └── dragon.js      ← Main logic
  └── Program.cs             ← Proxy + /api/config

DraCode.KoboldLair.Server/
  └── Program.cs             ← API endpoints + WebSocket

docs/
  ├── DRAGON_PROJECT_SELECTION.md
  ├── KOBOLDLAIR_API_PROXY.md
  └── troubleshooting/
      └── DRAGON_CONNECTION_TROUBLESHOOTING.md
```

## Features

✅ Project selection (existing or new)  
✅ Provider selection (Dragon-compatible only)  
✅ Dynamic WebSocket URL (no hardcoded ports)  
✅ API proxy (no CORS issues)  
✅ Service discovery (automatic URL resolution)  
✅ Change project without refresh  
✅ Shows current project/provider in header  
✅ Validation before chat start  

## Support

See full documentation in `docs/troubleshooting/DRAGON_CONNECTION_TROUBLESHOOTING.md`
