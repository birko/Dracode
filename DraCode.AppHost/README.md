# DraCode.AppHost - .NET Aspire Orchestration

This project uses .NET Aspire to orchestrate and run the DraCode WebSocket server and Web client together.

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
1. Start the WebSocket API server (DraCode.WebSocket) on port 5000
2. Start the Web Client (DraCode.Web) on port 5001
3. Launch the Aspire Dashboard in your browser

### Aspire Dashboard

The dashboard (typically at `http://localhost:15888`) provides:
- **Resources**: View all running services and their status
- **Console Logs**: Real-time logs from all services
- **Metrics**: Performance metrics and resource usage
- **Traces**: Distributed tracing across services
- **Environment Variables**: View configuration for each service

## Architecture

```
┌──────────────────────┐
│  DraCode.AppHost     │  ← Orchestrator
│  (Aspire Host)       │
└──────────┬───────────┘
           │
           ├─────────────────────────┐
           │                         │
           ▼                         ▼
┌────────────────────┐    ┌────────────────────┐
│ DraCode.WebSocket  │    │   DraCode.Web      │
│ Port: 5000         │◄───│   Port: 5001       │
│ (API Server)       │    │   (Web Client)     │
└────────────────────┘    └────────────────────┘
           │
           ▼
┌────────────────────┐
│  DraCode.Agent     │
│  (Agent Library)   │
└────────────────────┘
```

## Service Defaults

The `DraCode.ServiceDefaults` project provides shared configuration for:
- **Health Checks**: `/health` and `/alive` endpoints
- **Telemetry**: OpenTelemetry integration
- **Service Discovery**: Automatic service communication
- **Resilience**: Retry policies and circuit breakers

## Configuration

### AppHost Configuration

The orchestration is configured in `AppHost.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add WebSocket API service
var websocket = builder.AddProject<Projects.DraCode_WebSocket>("dracode-websocket");

// Add Web Client with reference to WebSocket
var web = builder.AddProject<Projects.DraCode_Web>("dracode-web")
    .WithReference(websocket);

builder.Build().Run();
```

Ports are configured in each project's `Properties/launchSettings.json`:
- **DraCode.WebSocket**: Port 5000
- **DraCode.Web**: Port 5001

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

### 1. **Single Command Startup**
Instead of:
```bash
# Terminal 1
dotnet run --project DraCode.WebSocket

# Terminal 2
dotnet run --project DraCode.Web
```

Just run:
```bash
dotnet run --project DraCode.AppHost
```

### 2. **Service Discovery**
The Web client automatically knows where the WebSocket server is running.

### 3. **Unified Logging**
View logs from all services in one dashboard.

### 4. **Health Monitoring**
Automatic health checks for all services with visual status indicators.

### 5. **Development Experience**
- Live reload for configuration changes
- Easy environment variable management
- Integrated debugging

## Development vs Production

### Development (with Aspire)
```bash
dotnet run --project DraCode.AppHost
```
- Runs both services locally
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
```

## Visual Studio Integration

1. Set `DraCode.AppHost` as the startup project
2. Press F5 to run with debugging
3. Aspire Dashboard opens automatically
4. All services start together

## Troubleshooting

### Dashboard doesn't open
- Check if port 15888 is available
- Look for the dashboard URL in console output

### Service fails to start
- Check the Resources tab in Aspire Dashboard
- View console logs for the specific service
- Verify port availability (5000, 5001)

### Services can't communicate
- Ensure ServiceDefaults is referenced by both projects
- Check service discovery configuration
- Verify CORS settings in WebSocket project

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Service Discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)
- [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard)
