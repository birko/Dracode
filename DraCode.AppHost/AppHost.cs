var builder = DistributedApplication.CreateBuilder(args);

// Add WebSocket API service
var websocket = builder.AddProject<Projects.DraCode_WebSocket>("dracode-websocket")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Web Client and reference WebSocket service
var web = builder.AddProject<Projects.DraCode_Web>("dracode-web")
    .WithReference(websocket);

// Add KoboldTown Orchestrator service
var koboldtown = builder.AddProject<Projects.DraCode_KoboldTown>("dracode-koboldtown")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

builder.Build().Run();
