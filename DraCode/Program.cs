using DraCode.Agent.Agents;
using Spectre.Console;
using System.Text.Json;

// Helper to render streaming messages with AnsiConsole
static void RenderMessage(string messageType, string content)
{
    switch (messageType)
    {
        case "info":
            if (content.StartsWith("ITERATION"))
            {
                AnsiConsole.Write(new Rule($"[bold cyan]{Markup.Escape(content)}[/]")
                {
                    Justification = Justify.Left
                });
            }
            else if (content.StartsWith("Stop reason:"))
            {
                var stopReason = content.Replace("Stop reason:", "").Trim();
                var stopReasonColor = stopReason switch
                {
                    "tool_use" => "yellow",
                    "end_turn" => "green",
                    "error" => "red",
                    _ => "grey"
                };
                AnsiConsole.MarkupLine($"[dim]Stop reason:[/] [{stopReasonColor}]{Markup.Escape(stopReason)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(content)}[/]");
            }
            break;

        case "tool_call":
            var toolPanel = new Panel(new Markup($"[white]{Markup.Escape(content)}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Header = new PanelHeader("🔧 [yellow]Tool Call[/]", Justify.Left)
            };
            AnsiConsole.Write(toolPanel);
            break;

        case "tool_result":
            var isError = content.Contains("Error:", StringComparison.OrdinalIgnoreCase);
            var resultPanel = new Panel(new Markup($"[{(isError ? "red" : "green")}]{Markup.Escape(content)}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(isError ? Color.Red : Color.Green),
                Header = new PanelHeader("📋 [white]Result[/]", Justify.Left)
            };
            AnsiConsole.Write(resultPanel);
            break;

        case "assistant":
            var assistantPanel = new Panel(new Markup($"[white]{Markup.Escape(content)}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Header = new PanelHeader("💬 [cyan]Assistant[/]", Justify.Left)
            };
            AnsiConsole.Write(assistantPanel);
            break;

        case "assistant_final":
            var finalPanel = new Panel(new Markup($"[white]{Markup.Escape(content)}[/]"))
            {
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green),
                Header = new PanelHeader("💬 [green]Assistant[/]", Justify.Left)
            };
            AnsiConsole.Write(finalPanel);
            break;

        case "display":
            var lines = content.Split('\n', 2);
            var displayPanel = new Panel(new Markup($"[white]{Markup.Escape(lines.Length > 1 ? lines[1] : content)}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Blue),
                Padding = new Padding(2, 1)
            };
            if (lines.Length > 1)
            {
                displayPanel.Header = new PanelHeader($"📋 [bold blue]{Markup.Escape(lines[0])}[/]", Justify.Left);
            }
            AnsiConsole.Write(displayPanel);
            AnsiConsole.WriteLine();
            break;

        case "warning":
            AnsiConsole.MarkupLine($"[yellow]⚠️  {Markup.Escape(content)}[/]");
            break;

        case "error":
            AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(content)}[/]");
            break;

        case "prompt_console":
            // This is handled by the PromptCallback, just log that we received it
            AnsiConsole.MarkupLine($"[dim]Prompt request: {Markup.Escape(content)}[/]");
            break;

        default:
            AnsiConsole.MarkupLine($"[dim][{messageType}] {Markup.Escape(content)}[/]");
            break;
    }
}

// Helpers to keep parsing simple and DRY
static string GetString(JsonElement parent, string name, string? fallback = "")
    => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var el)
        ? (el.ValueKind == JsonValueKind.String ? el.GetString() ?? fallback ?? "" : el.ToString())
        : fallback ?? "";

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
bool interactive = GetBool(agentEl, "Interactive", true);
int maxIterations = agentEl.ValueKind == JsonValueKind.Object && agentEl.TryGetProperty("MaxIterations", out var maxIterEl) 
    ? (maxIterEl.ValueKind == JsonValueKind.Number ? maxIterEl.GetInt32() : 10) 
    : 10;
bool verbose = GetBool(agentEl, "Verbose", true);
int promptTimeout = agentEl.ValueKind == JsonValueKind.Object && agentEl.TryGetProperty("PromptTimeout", out var timeoutEl) 
    ? (timeoutEl.ValueKind == JsonValueKind.Number ? timeoutEl.GetInt32() : 300) 
    : 300;
string? defaultPromptResponse = GetString(agentEl, "DefaultPromptResponse", null);
int modelDepth = agentEl.ValueKind == JsonValueKind.Object && agentEl.TryGetProperty("ModelDepth", out var depthEl) 
    ? (depthEl.ValueKind == JsonValueKind.Number ? depthEl.GetInt32() : 5) 
    : 5;
string workingDirectory = GetString(agentEl, "WorkingDirectory", Environment.CurrentDirectory);

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

// Check if provider, verbose or other options were set via command-line
bool providerSetViaArgs = false;
bool verboseSetViaArgs = false;
bool interactiveSetViaArgs = false;
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
    else if (arg.StartsWith("--interactive=", StringComparison.OrdinalIgnoreCase))
    {
        interactive = bool.Parse(arg["--interactive=".Length..]);
        interactiveSetViaArgs = true;
    }
    else if (arg.Equals("--interactive", StringComparison.OrdinalIgnoreCase))
    {
        interactive = true;
        interactiveSetViaArgs = true;
    }
    else if (arg.Equals("--no-interactive", StringComparison.OrdinalIgnoreCase) || arg.Equals("--non-interactive", StringComparison.OrdinalIgnoreCase))
    {
        interactive = false;
        interactiveSetViaArgs = true;
    }
    else if (arg.StartsWith("--max-iterations=", StringComparison.OrdinalIgnoreCase))
    {
        maxIterations = int.Parse(arg["--max-iterations=".Length..]);
    }
    else if (arg.StartsWith("--prompt-timeout=", StringComparison.OrdinalIgnoreCase))
    {
        promptTimeout = int.Parse(arg["--prompt-timeout=".Length..]);
    }
    else if (arg.StartsWith("--default-prompt-response=", StringComparison.OrdinalIgnoreCase))
    {
        defaultPromptResponse = arg["--default-prompt-response=".Length..];
    }
    else if (arg.StartsWith("--model-depth=", StringComparison.OrdinalIgnoreCase))
    {
        modelDepth = int.Parse(arg["--model-depth=".Length..]);
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

// If interactive not set via args, let user choose
if (!interactiveSetViaArgs)
{
    var interactiveChoice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold cyan]Enable interactive mode?[/]")
            .AddChoices(new[] { 
                "Yes - Agent can prompt for user input", 
                "No - Non-interactive mode (auto-respond to prompts)" 
            })
            .HighlightStyle(new Style(Color.Cyan1)));
    
    interactive = interactiveChoice.StartsWith("Yes");
    AnsiConsole.WriteLine();
    
    // If non-interactive, ask for default response
    if (!interactive && string.IsNullOrEmpty(defaultPromptResponse))
    {
        var setDefault = AnsiConsole.Confirm("[yellow]Set a default response for prompts?[/]", false);
        if (setDefault)
        {
            defaultPromptResponse = AnsiConsole.Ask<string>("[green]Default response:[/]", "I don't have that information");
        }
    }
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
infoTable.AddRow("[cyan]Interactive Mode[/]", interactive ? "[green]Yes[/]" : "[red]No[/]");
infoTable.AddRow("[cyan]Verbose[/]", verbose ? "[green]Yes[/]" : "[red]No[/]");
infoTable.AddRow("[cyan]Max Iterations[/]", $"[yellow]{maxIterations}[/]");
infoTable.AddRow("[cyan]Model Depth[/]", $"[yellow]{modelDepth}[/] [dim]({GetDepthLabel(modelDepth)})[/]");
if (!interactive && !string.IsNullOrEmpty(defaultPromptResponse))
{
    infoTable.AddRow("[cyan]Default Response[/]", $"[dim]{Markup.Escape(defaultPromptResponse)}[/]");
}

// Helper function for depth label
static string GetDepthLabel(int depth) => depth switch
{
    <= 3 => "Quick",
    >= 7 => "Deep",
    _ => "Balanced"
};

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
        
        // Create agent options
        var agentOptions = new DraCode.Agent.AgentOptions
        {
            WorkingDirectory = workingDirectory,
            Interactive = interactive,
            MaxIterations = maxIterations,
            Verbose = verbose,
            PromptTimeout = promptTimeout,
            DefaultPromptResponse = defaultPromptResponse,
            ModelDepth = modelDepth
        };
        
        // Create new agent instance for this task
        var agent = AgentFactory.Create(type, agentOptions, providerConfig);
        
        // Set up message callback to render with AnsiConsole
        agent.SetMessageCallback((messageType, content) =>
        {
            RenderMessage(messageType, content);
        });
        
        // Set up AskUser prompt handler for console mode (only in interactive mode)
        if (interactive)
        {
            var askUserTool = agent.Tools.OfType<DraCode.Agent.Tools.AskUser>().FirstOrDefault();
            if (askUserTool != null)
            {
                askUserTool.PromptCallback = async (question, context) =>
                {
                    // Display prompt with AnsiConsole
                    var panelContent = new Markup($"[bold cyan]❓ Question:[/] [white]{Markup.Escape(question)}[/]");
                    
                    if (!string.IsNullOrWhiteSpace(context))
                    {
                        panelContent = new Markup(
                            $"[dim]💡 Context:[/] [grey]{Markup.Escape(context)}[/]\n\n" +
                            $"[bold cyan]❓ Question:[/] [white]{Markup.Escape(question)}[/]");
                    }

                    var panel = new Panel(panelContent)
                    {
                        Border = BoxBorder.Double,
                        BorderStyle = new Style(Color.Cyan1),
                        Header = new PanelHeader("[bold yellow]🤔 Agent Needs Your Input[/]", Justify.Center),
                        Padding = new Padding(2, 1)
                    };

                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();

                    // Read user input
                    var userResponse = AnsiConsole.Ask<string>("[bold green]Your answer:[/]");

                    if (string.IsNullOrWhiteSpace(userResponse))
                    {
                        return "User provided no response (empty input)";
                    }

                    // Show confirmation
                    AnsiConsole.MarkupLine($"[dim]✓ Received:[/] [white]{Markup.Escape(userResponse)}[/]");
                    AnsiConsole.WriteLine();

                    return await Task.FromResult(userResponse);
                };
            }
        }
        
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
