using DraCode.Agent.Agents;
using System.Text.Json;

// Helpers to keep parsing simple and DRY
static string GetString(JsonElement parent, string name, string fallback = "")
    => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var el)
        ? (el.ValueKind == JsonValueKind.String ? el.GetString() ?? fallback : el.ToString())
        : fallback;

static bool GetBool(JsonElement parent, string name, bool fallback = true)
{
    if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var el))
    {
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out var v) => v,
            _ => fallback
        };
    }
    return fallback;
}

static Dictionary<string, string> GetSectionDict(JsonElement parent, string name)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var section) && section.ValueKind == JsonValueKind.Object)
    {
        foreach (var prop in section.EnumerateObject())
        {
            var val = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            dict[prop.Name] = val ?? string.Empty;
        }
    }
    return dict;
}

// Prefer local debug settings if present, allow explicit override via APPSETTINGS_PATH
var explicitPath = Environment.GetEnvironmentVariable("APPSETTINGS_PATH");
var localPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
var defaultPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var jsonPath = !string.IsNullOrWhiteSpace(explicitPath)
    ? explicitPath
    : (File.Exists(localPath) ? localPath : defaultPath);
var json = File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : "{}";
using var doc = JsonDocument.Parse(json);
var root = doc.RootElement;
var agentEl = root.TryGetProperty("Agent", out var a) ? a : default;

// Read defaults from config
string provider = GetString(agentEl, "Provider", "openai");
string workingDirectory = GetString(agentEl, "WorkingDirectory", Environment.CurrentDirectory);
bool verbose = GetBool(agentEl, "Verbose", true);
string taskPrompt = GetString(agentEl, "TaskPrompt", "");

// Command-line overrides: --provider and --task
foreach (var arg in Environment.GetCommandLineArgs())
{
    if (arg.StartsWith("--provider=", StringComparison.OrdinalIgnoreCase)) provider = arg["--provider=".Length..];
    else if (arg.StartsWith("--task=", StringComparison.OrdinalIgnoreCase)) taskPrompt = arg["--task=".Length..];
}

// Load provider-specific config from Agent.Providers[{provider}]
var providerConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
if (agentEl.ValueKind == JsonValueKind.Object && agentEl.TryGetProperty("Providers", out var providers) && providers.ValueKind == JsonValueKind.Object)
{
    if (providers.TryGetProperty(provider, out var selected) && selected.ValueKind == JsonValueKind.Object)
    {
        foreach (var prop in selected.EnumerateObject())
        {
            var val = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            providerConfig[prop.Name] = val ?? string.Empty;
        }
    }
}

// Allow environment variables to override config values (e.g., OPENAI_API_KEY)
foreach (var kv in providerConfig.ToList())
{
    var envVal = Environment.GetEnvironmentVariable(kv.Key) ?? Environment.GetEnvironmentVariable(kv.Key.ToUpperInvariant());
    if (!string.IsNullOrWhiteSpace(envVal)) providerConfig[kv.Key] = envVal;
}
var type = providerConfig.TryGetValue("type", out string? value) ? value : "openai";
var agent = AgentFactory.Create(type, workingDirectory, verbose, providerConfig);
Console.WriteLine($"Agent created with provider '{provider}' in '{workingDirectory}'.");

// If no task prompt is provided via config or CLI, request user input
if (string.IsNullOrWhiteSpace(taskPrompt))
{
    Console.Write("Enter task prompt: ");
    taskPrompt = Console.ReadLine()?.Trim() ?? string.Empty;
}

// If task prompt looks like a file path, read its contents
if (!string.IsNullOrWhiteSpace(taskPrompt))
{
    var candidate = taskPrompt.Trim();
    if ((candidate.StartsWith("\"") && candidate.EndsWith("\"")) || (candidate.StartsWith("'") && candidate.EndsWith("'")))
        candidate = candidate[1..^1];

    string? filePath = null;
    if (File.Exists(candidate))
    {
        filePath = candidate;
    }
    else if (!Path.IsPathRooted(candidate))
    {
        var wdPath = Path.Combine(workingDirectory, candidate);
        if (File.Exists(wdPath)) filePath = wdPath;
    }

    if (filePath is not null)
    {
        try
        {
            taskPrompt = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read task file '{filePath}': {ex.Message}");
        }
    }
}

if (!string.IsNullOrWhiteSpace(taskPrompt))
{
    await agent.RunAsync(taskPrompt);
}
