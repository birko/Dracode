var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

var app = builder.Build();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Serve static files
app.UseStaticFiles();

// Serve index.html as default
app.MapFallbackToFile("index.html");

app.Run();
