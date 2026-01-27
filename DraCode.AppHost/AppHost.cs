var builder = DistributedApplication.CreateBuilder(args);

// ====================================================================================
// RESOURCE GROUPS CONFIGURATION
// ====================================================================================
// 
// This AppHost defines two logical resource groups:
//
// 1. WebSocket System (dracode-websocket + dracode-web)
//    - Multi-provider WebSocket API service
//    - Web client for WebSocket interactions
//    - Start BOTH services together from the Aspire Dashboard
//
// 2. KoboldLair (dracode-koboldlair-server + dracode-koboldlair-client)
//    - Autonomous multi-agent coding system
//    - Server: WebSocket API with authentication
//    - Client: Web UI for monitoring and interaction
//    - Can run independently of WebSocket System
//    - Start from the Aspire Dashboard when needed
//
// USAGE:
//   - Run 'dotnet run --project DraCode.AppHost' to start the Aspire Dashboard
//   - All services are initially stopped
//   - Use the dashboard to manually start the service groups you need
//   - WebSocket System services should be started together (websocket first, then web)
//
// ====================================================================================

// ===== WebSocket System Group =====
var websocket = builder.AddProject<Projects.DraCode_WebSocket>("dracode-websocket")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

var web = builder.AddProject<Projects.DraCode_Web>("dracode-web")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithReference(websocket);

// ===== KoboldLair Group =====
var koboldlairServer = builder.AddProject<Projects.DraCode_KoboldLair_Server>("dracode-koboldlair-server")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

var koboldlairClient = builder.AddProject<Projects.DraCode_KoboldLair_Client>("dracode-koboldlair-client")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithReference(koboldlairServer);

builder.Build().Run();
