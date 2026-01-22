# WebSocket Server Quick Start Guide

This guide explains how to use the **DraCode.WebSocket** server and **DraCode.Web** client with .NET Aspire orchestration.

## 🚀 Quick Start

### Option 1: Run with Aspire (Recommended)

```bash
# Run both WebSocket server AND web client with single command
dotnet run --project DraCode.AppHost

# Access:
# - Aspire Dashboard: https://localhost:17169 (or check console output)
# - WebSocket Server: ws://localhost:5000/ws
# - Web Client: http://localhost:5001
```

### Option 2: Run Individually

```bash
# Terminal 1: Start WebSocket server
dotnet run --project DraCode.WebSocket

# Terminal 2: Start Web client
dotnet run --project DraCode.Web
```

## 📡 WebSocket API

### Commands

The WebSocket server supports these commands (all require agentId except list):

#### 1. **list** - List Available Providers
```json
{
  \"command\": \"list\"
}
```

#### 2. **connect** - Create New Agent
```json
{
  \"command\": \"connect\",
  \"agentId\": \"agent-openai-123\",
  \"config\": {
    \"provider\": \"openai\"
  }
}
```
Creates a new agent instance. Multiple agents can exist per WebSocket connection.

#### 3. **send** - Send Task to Agent
```json
{
  \"command\": \"send\",
  \"agentId\": \"agent-openai-123\",
  \"data\": \"Explain quantum computing\"
}
```

#### 4. **reset** - Reinitialize Agent
```json
{
  \"command\": \"reset\",
  \"agentId\": \"agent-openai-123\",
  \"config\": {
    \"provider\": \"openai\",
    \"model\": \"gpt-4o\"
  }
}
```

#### 5. **disconnect** - Remove Agent
```json
{
  \"command\": \"disconnect\",
  \"agentId\": \"agent-openai-123\"
}
```
Disposes a specific agent. WebSocket connection stays open for other agents.

## 🔑 Multi-Agent Architecture

### How It Works
1. **Single WebSocket Connection**: Client connects once to ws://localhost:5000/ws
2. **Multiple Agents**: Client creates multiple agents with unique IDs
3. **Message Routing**: Server routes commands/responses using agentId
4. **Independent State**: Each agent maintains its own conversation history

## 🎨 Web Client Features

- Connect to multiple providers simultaneously
- Each provider gets its own tab
- Separate activity logs per agent
- Independent task inputs

## 💡 Example Workflow

1. Start: dotnet run --project DraCode.AppHost
2. Open: http://localhost:5001
3. Click: \"Connect to Server\"
4. Click: \"openai\" provider card → Tab opens
5. Click: \"claude\" provider card → Another tab opens
6. Send different tasks to each
7. Compare responses in separate logs

## 📚 Related Documentation

- [DraCode.WebSocket/README.md](DraCode.WebSocket/README.md)
- [DraCode.Web/MULTI_PROVIDER_GUIDE.md](DraCode.Web/MULTI_PROVIDER_GUIDE.md)
- [DraCode.AppHost/README.md](DraCode.AppHost/README.md)
