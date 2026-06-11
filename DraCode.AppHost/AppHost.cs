var builder = DistributedApplication.CreateBuilder(args);

// ====================================================================================
// RESOURCE GROUPS CONFIGURATION
// ====================================================================================
//
// KoboldLair (dracode-koboldlair-server + dracode-koboldlair-client)
//   - Autonomous multi-agent coding system
//   - Server: WebSocket API with authentication
//   - Client: Web UI for monitoring and interaction
//
// USAGE:
//   - Run 'dotnet run --project DraCode.AppHost' to start the Aspire Dashboard
//   - All services are initially stopped
//   - Use the dashboard to manually start the services you need
//
// ====================================================================================

// ===== KoboldLair Group =====
var koboldlairServer = builder.AddProject<Projects.DraCode_KoboldLair_Server>("dracode-koboldlair-server")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithExplicitStart();

var koboldlairClient = builder.AddProject<Projects.DraCode_KoboldLair_Client>("dracode-koboldlair-client")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithReference(koboldlairServer)
    .WithExplicitStart();

builder.Build().Run();
