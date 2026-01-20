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

// Read tasks from config (can be single string or array)
var tasks = new List<string>();
if (agentEl.ValueKind == JsonValueKind.Object && agentEl.TryGetProperty("Tasks", out var tasksEl))
{
    if (tasksEl.ValueKind == JsonValueKind.Array)
    {
        foreach (var task in tasksEl.EnumerateArray())
        {
            var taskStr = task.ValueKind == JsonValueKind.String ? task.GetString() : task.ToString();
            if (!string.IsNullOrWhiteSpace(taskStr)) tasks.Add(taskStr);
        }
    }
    else if (tasksEl.ValueKind == JsonValueKind.String)
    {
        var taskStr = tasksEl.GetString();
        if (!string.IsNullOrWhiteSpace(taskStr)) tasks.Add(taskStr);
    }
}

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
        var task = arg["--task=".Length..];
        if (!string.IsNullOrWhiteSpace(task))
        {
            // Support comma-separated tasks
            if (task.Contains(','))
            {
                tasks.AddRange(task.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            else
            {
                tasks.Add(task);
            }
        }
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
infoTable.AddRow("[cyan]Type[/]", $"[yellow]{Markup.Escape(type)}[/]");
infoTable.AddRow("[cyan]Working Directory[/]", $"[dim]{Markup.Escape(workingDirectory)}[/]");
infoTable.AddRow("[cyan]Verbose[/]", verbose ? "[green]Yes[/]" : "[red]No[/]");

AnsiConsole.Write(infoTable);
AnsiConsole.WriteLine();

// If no tasks are provided via config or CLI, request user input
if (tasks.Count == 0)
{
    AnsiConsole.MarkupLine("[bold cyan]Enter tasks (one per line, empty line to finish):[/]");
    while (true)
    {
        var task = AnsiConsole.Ask<string>($"[green]Task {tasks.Count + 1}:[/]", "");
        if (string.IsNullOrWhiteSpace(task)) break;
        tasks.Add(task);
    }
}

// Process each task with file path resolution
var resolvedTasks = new List<string>();
foreach (var task in tasks)
{
    var candidate = task.Trim();
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
            var fileContent = File.ReadAllText(filePath);
            resolvedTasks.Add(fileContent);
            AnsiConsole.MarkupLine($"[dim]📄 Loaded task from file: {Markup.Escape(filePath)}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Failed to read task file '{Markup.Escape(filePath)}': {Markup.Escape(ex.Message)}[/]");
            resolvedTasks.Add(task); // Use original task as fallback
        }
    }
    else
    {
        resolvedTasks.Add(task);
    }
}

// Execute all tasks
if (resolvedTasks.Count > 0)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[bold green]Starting Execution of {resolvedTasks.Count} Task{(resolvedTasks.Count > 1 ? "s" : "")}[/]")
    {
        Justification = Justify.Left
    });
    AnsiConsole.WriteLine();
    
    for (int i = 0; i < resolvedTasks.Count; i++)
    {
        var taskNumber = i + 1;
        var currentTask = resolvedTasks[i];
        
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold yellow]Task {taskNumber}/{resolvedTasks.Count}[/]")
        {
            Justification = Justify.Left
        });
        AnsiConsole.WriteLine();
        
        // Show task preview
        var preview = currentTask.Length > 100 ? currentTask.Substring(0, 100) + "..." : currentTask;
        AnsiConsole.MarkupLine($"[dim]📝 {Markup.Escape(preview)}[/]");
        AnsiConsole.WriteLine();
        
        // Create new agent instance for this task
        var agent = AgentFactory.Create(type, workingDirectory, verbose, providerConfig);
        
        try
        {
            await agent.RunAsync(currentTask);
            AnsiConsole.MarkupLine($"[green]✓ Task {taskNumber} completed successfully[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Task {taskNumber} failed: {Markup.Escape(ex.Message)}[/]");
        }
    }
    
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[bold green]All Tasks Complete ({resolvedTasks.Count}/{resolvedTasks.Count})[/]")
    {
        Justification = Justify.Left
    });
}
