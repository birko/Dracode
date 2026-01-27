# DraCode.KoboldLair.Server

WebSocket server for the KoboldLair autonomous multi-agent coding system with token-based IP authentication.

## Features

- **WebSocket Endpoints**:
  - `/ws` - Wyvern task delegation endpoint
  - `/dragon` - Dragon requirements gathering endpoint
- **Token-based Authentication** with IP binding support
- **REST API** for project management and provider configuration
- **Multi-provider AI support** (OpenAI, Claude, Gemini, Ollama, Azure OpenAI, GitHub Copilot)

## Authentication

The server supports token-based authentication with optional IP address binding for enhanced security.

### Configuration

Authentication is configured in `appsettings.json`:

```json
{
  "Authentication": {
    "Enabled": false,
    "Tokens": [],
    "TokenBindings": []
  }
}
```

### Authentication Modes

#### 1. Disabled (Default)
```json
{
  "Authentication": {
    "Enabled": false
  }
}
```
All connections are allowed without authentication.

#### 2. Simple Token Authentication
```json
{
  "Authentication": {
    "Enabled": true,
    "Tokens": [
      "your-secret-token-here",
      "${MY_TOKEN_ENV_VAR}"
    ]
  }
}
```
Clients must provide a valid token in the query string: `ws://server/ws?token=your-secret-token-here`

#### 3. Token with IP Binding (Recommended for Production)
```json
{
  "Authentication": {
    "Enabled": true,
    "TokenBindings": [
      {
        "Token": "production-token-1",
        "AllowedIps": ["192.168.1.100", "10.0.0.50"]
      },
      {
        "Token": "${SECURE_TOKEN}",
        "AllowedIps": ["${CLIENT_IP_ADDRESS}"]
      }
    ]
  }
}
```
Tokens are bound to specific IP addresses. Only requests from allowed IPs with matching tokens are accepted.

### Environment Variables

Tokens and IP addresses can be loaded from environment variables using the `${VAR_NAME}` syntax:

```json
{
  "Token": "${KOBOLDLAIR_AUTH_TOKEN}",
  "AllowedIps": ["${TRUSTED_CLIENT_IP}"]
}
```

### Behind Proxy/Load Balancer

The server automatically detects client IP addresses from:
1. `X-Forwarded-For` header (takes the first IP for the original client)
2. `X-Real-IP` header (nginx)
3. Direct connection IP (fallback)

## Running the Server

### Development
```bash
dotnet run --project DraCode.KoboldLair.Server
```

### Production
```bash
# Set environment variables for authentication
export KOBOLDLAIR_AUTH_TOKEN="your-production-token"
export TRUSTED_CLIENT_IP="192.168.1.100"

# Run the server
dotnet run --project DraCode.KoboldLair.Server --configuration Release
```

## API Endpoints

- `GET /` - Health check
- `GET /api/hierarchy` - Get project hierarchy and statistics
- `GET /api/projects` - Get all projects
- `GET /api/stats` - Get system statistics
- `GET /api/providers` - Get provider configuration
- `POST /api/providers/configure` - Update provider settings
- `GET /api/projects/{id}/providers` - Get project-specific providers
- `POST /api/projects/{id}/providers` - Update project providers

## WebSocket Protocol

### Connection
```
ws://server:port/ws?token=your-token-here
ws://server:port/dragon?token=your-token-here
```

### Message Format
All messages are JSON:
```json
{
  "type": "message_type",
  "data": { ... }
}
```

## Security Best Practices

1. **Always enable authentication in production**
2. **Use environment variables for tokens** - never commit secrets to source control
3. **Use IP binding** when possible to restrict access to known clients
4. **Use HTTPS/WSS** in production (configure reverse proxy like nginx)
5. **Rotate tokens regularly**
6. **Monitor authentication logs** for suspicious activity

## Related

- [DraCode.KoboldLair.Client](../DraCode.KoboldLair.Client/README.md) - Web UI client
