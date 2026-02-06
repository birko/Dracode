# DraCode.AppHost - .NET Aspire Orchestration

This project uses .NET Aspire to orchestrate and run the DraCode services.

## Resource Groups

The AppHost organizes DraCode services into two logical groups:

### 1. WebSocket System
- **dracode-websocket**: Multi-provider WebSocket API service (OpenAI, Anthropic, Ollama, Gemini)
- **dracode-web**: Interactive web client for WebSocket-based agent interactions

**Purpose**: Provides real-time, interactive AI agent communication through WebSocket connections.

**When to use**: 
- Testing WebSocket connectivity with different providers
- Interactive agent conversations through the web UI
- Developing/debugging WebSocket-based features

### 2. KoboldLair
- **dracode-koboldlair-server**: Autonomous multi-agent coding orchestration system backend
- **dracode-koboldlair-client**: Web UI for monitoring and interaction

**Purpose**: Fully autonomous coding system where Dragon creates requirements, Wyvern analyzes projects and creates task breakdowns, Wyrm handles agent selection, Drake supervisors manage execution, and Kobolds execute coding tasks.

**When to use**:
- Automated project analysis and development
- Large-scale refactoring tasks
- Background coding agent orchestration
- Multi-agent task distribution

## Startup Configuration

### Auto-Start Disabled by Default
All services are configured to **NOT** start automatically when the AppHost launches. This gives you full control over which services to run.

To start services:
1. Run the AppHost: `dotnet run --project DraCode.AppHost`
2. The Aspire Dashboard will open in your browser
3. Manually start the service groups you need from the dashboard

### Recommended Startup Sequence

**For WebSocket System**:
1. Start `dracode-websocket` first
2. Wait for it to be running (check dashboard status)
3. Start `dracode-web` (it depends on websocket)

**For KoboldLair**:
- Start `dracode-koboldlair-server` first
- Wait for it to be running (check dashboard status)
- Start `dracode-koboldlair-client` (it depends on server)

## What is .NET Aspire?

.NET Aspire is an opinionated, cloud-ready stack for building distributed applications. It provides:
- **Service Orchestration**: Run multiple services together with a single command
- **Service Discovery**: Automatic service-to-service communication
- **Telemetry**: Built-in logging, metrics, and distributed tracing
- **Dashboard**: Web-based dashboard to monitor your services
- **Health Checks**: Automatic health monitoring for all services

## Running with Aspire

### Start All Services with Dashboard

```bash
dotnet run --project DraCode.AppHost
```

This will:
1. Launch the Aspire Dashboard in your browser
2. Display all available services (initially stopped)
3. Allow you to manually start the services you need

### Aspire Dashboard

The dashboard (typically at `https://localhost:17094`) provides:
- **Resources**: View all services and their status (stopped/running)
- **Console Logs**: Real-time logs from running services
- **Metrics**: Performance metrics and resource usage
- **Traces**: Distributed tracing across services
- **Environment Variables**: View configuration for each service
- **Control Panel**: Start/stop individual services

## Architecture

```
┌────────────────────────────────┐
│     DraCode.AppHost            │  ← Aspire Orchestrator
│     (Service Coordinator)      │
└────────────┬───────────────────┘
             │
             ├──────────────────────────┬───────────────────────┐
             │                          │                       │
             ▼                          ▼                       ▼
┌──────────────────────┐    ┌──────────────────────┐   ┌──────────────────────┐
│ dracode-websocket    │    │   dracode-web        │   │ dracode-koboldlair   │
│ WebSocket API        │◄───│   Web Client         │   │ -server              │
│ (Group: WebSocket)   │    │   (Group: WebSocket) │   │ (Group: KoboldLair)  │
└──────────────────────┘    └──────────────────────┘   └──────────┬───────────┘
         │                                                         │
         │                                                         ▼
         │                                              ┌──────────────────────┐
         │                                              │ dracode-koboldlair   │
         │                                              │ -client              │
         │                                              │ (Group: KoboldLair)  │
         │                                              └──────────────────────┘
         └─────────────────┬───────────────────────────────────┘
                           ▼
                 ┌───────────────────┐
                 │  DraCode.Agent    │
                 │  (Agent Library)  │
                 │  21 Agent Types   │
                 └───────────────────┘
```

## Service Defaults

The `DraCode.ServiceDefaults` project provides shared configuration for:
- **Health Checks**: `/health` and `/alive` endpoints
- **Telemetry**: OpenTelemetry integration
- **Service Discovery**: Automatic service communication
- **Resilience**: Retry policies and circuit breakers

## Configuration

### AppHost Configuration

The orchestration is configured in `AppHost.cs` with clear group definitions:

```csharp
// ===== WebSocket System Group =====
var websocket = builder.AddProject<Projects.DraCode_WebSocket>("dracode-websocket")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var web = builder.AddProject<Projects.DraCode_Web>("dracode-web")
    .WithReference(websocket);

// ===== KoboldLair Group =====
var koboldlairServer = builder.AddProject<Projects.DraCode_KoboldLair_Server>("dracode-koboldlair-server")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var koboldlairClient = builder.AddProject<Projects.DraCode_KoboldLair_Client>("dracode-koboldlair-client")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(koboldlairServer);
```

### Auto-Start Configuration

Auto-start is disabled via environment variable in `launchSettings.json`:

```json
"ASPIRE__ORCHESTRATION__AUTOSTART": "false"
```

This ensures all services start in a stopped state, giving you control over which to run.

### Changing Ports

Edit the respective project's `Properties/launchSettings.json`:

**DraCode.WebSocket/Properties/launchSettings.json:**
```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://localhost:5000"
    }
  }
}
```

**DraCode.Web/Properties/launchSettings.json:**
```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://localhost:5001"
    }
  }
}
```

## Benefits of Using Aspire

### 1. **Grouped Service Management**
Start services by logical groups rather than individual processes:
- Start WebSocket System together (websocket + web)
- Start KoboldLair independently (server + client)
- Or run all services together

### 2. **Service Discovery**
The Web client automatically knows where the WebSocket server is running.

### 3. **Unified Logging**
View logs from all running services in one dashboard.

### 4. **Health Monitoring**
Automatic health checks for all services with visual status indicators.

### 5. **Development Experience**
- Manual control over which services run
- Live reload for configuration changes
- Easy environment variable management
- Integrated debugging

## Development Workflow

### Starting a Development Session

1. **Run AppHost**:
   ```bash
   dotnet run --project DraCode.AppHost
   ```

2. **Open Dashboard**: Browser opens automatically to `https://localhost:17094`

3. **Start Services as Needed**:
   - For WebSocket development: Start `dracode-websocket` and `dracode-web`
   - For KoboldLair development: Start `dracode-koboldlair-server` and `dracode-koboldlair-client`
   - For full system: Start all services

4. **Monitor**: Use dashboard to view logs, metrics, and traces

5. **Stop Services**: Stop individual services or stop AppHost to stop all

### Visual Studio Integration

1. Set `DraCode.AppHost` as the startup project
2. Press F5 to run with debugging
3. Aspire Dashboard opens automatically
4. Manually start the services you want to debug

## Development vs Production

### Development (with Aspire)
```bash
dotnet run --project DraCode.AppHost
```
- Runs services locally with manual control
- Aspire dashboard for monitoring
- Service discovery enabled
- Full telemetry

### Production (without Aspire)
Deploy services independently:
```bash
# WebSocket API
dotnet publish DraCode.WebSocket -c Release
# Deploy to your server

# Web Client
dotnet publish DraCode.Web -c Release
# Deploy to your web server

# KoboldLair Server
dotnet publish DraCode.KoboldLair.Server -c Release
# Deploy to your orchestration server

# KoboldLair Client
dotnet publish DraCode.KoboldLair.Client -c Release
# Deploy to your web server
```

## Troubleshooting

### Dashboard doesn't open
- Check if ports 17094/15073 are available
- Look for the dashboard URL in console output
- Try the http profile if https fails

### Service fails to start
- Check the Resources tab in Aspire Dashboard
- View console logs for the specific service
- Verify port availability (5000, 5001, etc.)
- Ensure dependencies (DraCode.Agent, ServiceDefaults) built successfully

### Services can't communicate
- Ensure ServiceDefaults is referenced by both projects
- Check service discovery configuration
- Verify CORS settings in WebSocket project
- Ensure websocket starts before web client

### All services start automatically
- Verify `ASPIRE__ORCHESTRATION__AUTOSTART: "false"` in launchSettings.json
- Check that environment variable is being loaded
- Restart AppHost to apply changes

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Service Discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)
- [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard)
- [DraCode WebSocket README](../DraCode.WebSocket/README.md)
- [DraCode KoboldLair Server README](../DraCode.KoboldLair.Server/README.md)
- [DraCode KoboldLair Client README](../DraCode.KoboldLair.Client/README.md)
- [Main Project README](../README.md)

