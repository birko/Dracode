var builder = DistributedApplication.CreateBuilder(args);

// Add WebSocket API service
var websocket = builder.AddProject<Projects.DraCode_WebSocket>("dracode-websocket");

// Add Web Client and reference WebSocket service
var web = builder.AddProject<Projects.DraCode_Web>("dracode-web")
    .WithReference(websocket);

builder.Build().Run();
