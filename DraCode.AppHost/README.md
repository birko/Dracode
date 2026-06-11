# DraCode.AppHost - .NET Aspire Orchestration

This project uses .NET Aspire to orchestrate and run the DraCode KoboldLair services.

## Services

- **dracode-koboldlair-server**: Autonomous multi-agent coding orchestration system backend (WebSocket API with authentication)
- **dracode-koboldlair-client**: Web UI for monitoring and interaction

**Purpose**: Fully autonomous coding system where Dragon gathers requirements, Wyrm pre-analyzes, Wyvern creates task breakdowns, Drake supervisors manage execution, and Kobolds execute coding tasks.

## Startup Configuration

### Auto-Start Disabled by Default
All services are configured to **NOT** start automatically when the AppHost launches. This gives you full control over which services to run.

To start services:
1. Run the AppHost: `dotnet run --project DraCode.AppHost`
2. The Aspire Dashboard will open in your browser
3. Manually start the services you need from the dashboard

### Recommended Startup Sequence
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

```bash
dotnet run --project DraCode.AppHost
```

This will:
1. Launch the Aspire Dashboard in your browser
2. Display the available services (initially stopped)
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
└────────────┬───────────────────┘
             │
             ▼
┌──────────────────────────┐
│ dracode-koboldlair-server│
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ dracode-koboldlair-client│
└──────────────────────────┘
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
// ===== KoboldLair Group =====
var koboldlairServer = builder.AddProject<Projects.DraCode_KoboldLair_Server>("dracode-koboldlair-server")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithExplicitStart();

var koboldlairClient = builder.AddProject<Projects.DraCode_KoboldLair_Client>("dracode-koboldlair-client")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(koboldlairServer)
    .WithExplicitStart();
```

`.WithExplicitStart()` keeps services stopped until you start them from the dashboard.

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
# KoboldLair Server
dotnet publish DraCode.KoboldLair.Server -c Release

# KoboldLair Client
dotnet publish DraCode.KoboldLair.Client -c Release
```

## Troubleshooting

### Dashboard doesn't open
- Check if ports 17094/15073 are available
- Look for the dashboard URL in console output
- Try the http profile if https fails

### Service fails to start
- Check the Resources tab in Aspire Dashboard
- View console logs for the specific service
- Ensure dependencies (DraCode.KoboldLair, ServiceDefaults) built successfully

### Services can't communicate
- Ensure ServiceDefaults is referenced by both projects
- Check service discovery configuration
- Ensure the server starts before the client

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Service Discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)
- [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard)
- [DraCode KoboldLair Server README](../DraCode.KoboldLair.Server/README.md)
- [DraCode KoboldLair Client README](../DraCode.KoboldLair.Client/README.md)
- [Main Project README](../README.md)
