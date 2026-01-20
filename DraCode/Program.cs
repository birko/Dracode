using DraCode.Agent.Agents;
using Spectre.Console;
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

// Check if provider or verbose was set via command-line
bool providerSetViaArgs = false;
bool verboseSetViaArgs = false;
foreach (var arg in Environment.GetCommandLineArgs())
{
    if (arg.StartsWith("--provider=", StringComparison.OrdinalIgnoreCase))
    {
        provider = arg["--provider=".Length..];
        providerSetViaArgs = true;
    }
    else if (arg.StartsWith("--task=", StringComparison.OrdinalIgnoreCase))
    {
        taskPrompt = arg["--task=".Length..];
    }
    else if (arg.StartsWith("--verbose=", StringComparison.OrdinalIgnoreCase))
    {
        verbose = bool.Parse(arg["--verbose=".Length..]);
        verboseSetViaArgs = true;
    }
    else if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
    {
        verbose = true;
        verboseSetViaArgs = true;
    }
    else if (arg.Equals("--no-verbose", StringComparison.OrdinalIgnoreCase) || arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase))
    {
        verbose = false;
        verboseSetViaArgs = true;
    }
}

// Get list of configured providers
var configuredProviders = new List<string>();
if (agentEl.ValueKind == JsonValueKind.Object && agentEl.TryGetProperty("Providers", out var providersElement) && providersElement.ValueKind == JsonValueKind.Object)
{
    foreach (var prop in providersElement.EnumerateObject())
    {
        configuredProviders.Add(prop.Name);
    }
}

// If provider not set via args and we have multiple providers configured, let user choose
if (!providerSetViaArgs && configuredProviders.Count > 1)
{
    AnsiConsole.Clear();
    
    var selectionPrompt = new SelectionPrompt<string>()
        .Title("[bold cyan]Select an AI Provider:[/]")
        .PageSize(10)
        .MoreChoicesText("[grey](Move up and down to reveal more providers)[/]")
        .HighlightStyle(new Style(Color.Cyan1));

    // Add providers with icons
    var defaultProviderIndex = 0;
    for (int i = 0; i < configuredProviders.Count; i++)
    {
        var providerName = configuredProviders[i];
        var icon = providerName.ToLowerInvariant() switch
        {
            "openai" => "🤖",
            "claude" or "anthropic" => "🧠",
            "gemini" or "google" => "✨",
            "githubcopilot" => "🐙",
            "azureopenai" => "☁️",
            "ollama" => "🦙",
            _ => "🔧"
        };
        
        var displayName = providerName == provider 
            ? $"{icon} {providerName} [dim](default)[/]" 
            : $"{icon} {providerName}";
        
        selectionPrompt.AddChoice(providerName);
        
        // Track the default provider index
        if (providerName == provider)
        {
            defaultProviderIndex = i;
        }
    }

    provider = AnsiConsole.Prompt(selectionPrompt);
    AnsiConsole.WriteLine();
}

// If verbose not set via args, let user choose
if (!verboseSetViaArgs)
{
    var verboseChoice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold cyan]Enable verbose output?[/]")
            .AddChoices(new[] { "Yes - Show detailed execution info", "No - Show only results" })
            .HighlightStyle(new Style(Color.Cyan1)));
    
    verbose = verboseChoice.StartsWith("Yes");
    AnsiConsole.WriteLine();
}

// Load provider-specific config from Agent.Providers[{provider}]
var providerConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
if (agentEl.ValueKind == JsonValueKind.Object && agentEl.TryGetProperty("Providers", out var providersConfig) && providersConfig.ValueKind == JsonValueKind.Object)
{
    if (providersConfig.TryGetProperty(provider, out var selected) && selected.ValueKind == JsonValueKind.Object)
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

// Display nice banner
AnsiConsole.Clear();
var banner = new FigletText("DraCode")
    .Centered()
    .Color(Color.Cyan1);
AnsiConsole.Write(banner);

var infoTable = new Table()
    .Border(TableBorder.Rounded)
    .BorderColor(Color.Grey)
    .AddColumn(new TableColumn("[bold cyan]Setting[/]").Centered())
    .AddColumn(new TableColumn("[bold white]Value[/]").LeftAligned());

infoTable.AddRow("[cyan]Provider[/]", $"[yellow]{Markup.Escape(provider)}[/]");
infoTable.AddRow("[cyan]Model[/]", $"[yellow]{Markup.Escape(agent.ProviderName)}[/]");
infoTable.AddRow("[cyan]Working Directory[/]", $"[dim]{Markup.Escape(workingDirectory)}[/]");
infoTable.AddRow("[cyan]Verbose[/]", verbose ? "[green]Yes[/]" : "[red]No[/]");

AnsiConsole.Write(infoTable);
AnsiConsole.WriteLine();

// If no task prompt is provided via config or CLI, request user input
if (string.IsNullOrWhiteSpace(taskPrompt))
{
    taskPrompt = AnsiConsole.Ask<string>("[bold green]Enter task prompt:[/]");
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
            AnsiConsole.MarkupLine($"[dim]📄 Loaded task from file: {Markup.Escape(filePath)}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Failed to read task file '{Markup.Escape(filePath)}': {Markup.Escape(ex.Message)}[/]");
        }
    }
}

if (!string.IsNullOrWhiteSpace(taskPrompt))
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[bold green]Starting Task Execution[/]")
    {
        Justification = Justify.Left
    });
    AnsiConsole.WriteLine();
    
    await agent.RunAsync(taskPrompt);
    
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[bold green]Task Complete[/]")
    {
        Justification = Justify.Left
    });
}
