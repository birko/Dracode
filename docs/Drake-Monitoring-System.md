# 🦅 Drake Monitoring System

## Overview

The Drake Monitoring System provides automated background monitoring and lifecycle management for Kobolds in the KoboldLair system. **Drakes work automatically** - they're monitored by DrakeMonitoringService running every 60 seconds in the background.

Components:
- **DrakeFactory**: Creates and manages Drake instances from task file paths
- **DrakeMonitoringService**: Background service that monitors Drakes and updates task status
- **Drake**: Supervisor that manages Kobolds and synchronizes with markdown task files

**Drake is NOT interactive** - it automatically manages Kobold workers based on task files created by Wyvern.

## Architecture

```
┌─────────────────────────────────────────┐
│   DrakeMonitoringService (Background)   │
│   - Runs every 60 seconds               │
│   - Mutex prevents overlapping runs     │
└──────────────────┬──────────────────────┘
                   │
                   ▼
         ┌─────────────────┐
         │  DrakeFactory   │ 
         │  - Tracks all   │
         │    Drakes       │
         └────────┬────────┘
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
  ┌─────────┐         ┌─────────┐
  │ Drake 1 │         │ Drake 2 │
  │  (🐉)   │         │  (🐉)   │
  └────┬────┘         └────┬────┘
       │                   │
   ┌───┴───┐           ┌───┴───┐
   ▼       ▼           ▼       ▼
Kobold1 Kobold2    Kobold3 Kobold4
 (👹)    (👹)       (👹)    (👹)
```

## Components

### 1. DrakeFactory

Creates and manages Drake instances. Each Drake monitors one task file (Wyvern output).

**Features:**
- Thread-safe with lock
- Tracks all Drake instances by name
- Initializes Drakes from task file paths
- Provides statistics and queries

**Usage:**
```csharp
var drakeFactory = new DrakeFactory(
    koboldFactory,
    defaultProvider: "openai",
    defaultConfig: new Dictionary<string, string>
    {
        ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        ["model"] = "gpt-4o"
    }
);

// Create Drake for a task file (stored in project folder)
var drake = drakeFactory.CreateDrake(
    "./projects/my-project/backend-tasks.md",
    "my-drake"
);

// Query
var allDrakes = drakeFactory.GetAllDrakes();
var specificDrake = drakeFactory.GetDrake("my-drake");
Console.WriteLine($"Total Drakes: {drakeFactory.TotalDrakes}");
```

### 2. DrakeMonitoringService

Background service that runs every 60 seconds (configurable) and monitors all Drakes.

**Features:**
- Mutex pattern prevents overlapping executions
- Monitors all Drakes in parallel
- Unsummons completed Kobolds
- Logs statistics and errors

**Lifecycle:**
```
Start
  ↓
┌─────────────────┐
│ Wait 60 seconds │
└────────┬────────┘
         ↓
    Check _isRunning
         │
    ┌────┴─────┐
    │ Running? │
    └────┬─────┘
    No   │   Yes
    ▼    │    ▼
Monitor  │   Skip cycle
Tasks    │   (Log warning)
    │    │
    ▼    │
Set _isRunning = false
    │
    └──────> Loop back
```

**Monitoring Process:**
```csharp
foreach (var drake in drakeFactory.GetAllDrakes())
{
    drake.MonitorTasks();              // Update task status
    
    var stats = drake.GetStatistics();
    // Log: "Drake has 3 working, 2 done Kobolds"
    
    var unsummoned = drake.UnsummonCompletedKobolds();
    // Clean up done Kobolds
    
    drake.UpdateTasksFile();
    // Save markdown with latest status
}
```

**Configuration:**
```csharp
// In Program.cs
builder.Services.AddHostedService<DrakeMonitoringService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DrakeMonitoringService>>();
    var drakeFactory = sp.GetRequiredService<DrakeFactory>();
    
    return new DrakeMonitoringService(
        logger, 
        drakeFactory, 
        monitoringIntervalSeconds: 60  // Configurable
    );
});
```

### 3. Drake

Manages Kobolds for a set of tasks and synchronizes status with markdown file.

**Key Methods:**
- `MonitorTasks()` - Updates task status from Kobolds
- `UnsummonCompletedKobolds()` - Removes done Kobolds
- `UpdateTasksFile()` - Saves status to markdown
- `ExecuteTaskAsync()` - High-level: summon → work → complete

## Workflow

### 1. Initial Setup

```csharp
// Program.cs registers services
builder.Services.AddSingleton<KoboldFactory>();
builder.Services.AddSingleton<DrakeFactory>(...);
builder.Services.AddHostedService<DrakeMonitoringService>(...);
```

### 2. Create Drake for Task File

```csharp
// Wyvern creates task files in project folder
var WyvernRunner = new WyvernRunner(...);
await WyvernRunner.RunAsync(
    "Create a web app with login",
    outputPath: "./projects/web-app/backend-tasks.md"
);

// DrakeFactory creates Drake for this file
var drake = drakeFactory.CreateDrake(
    "./projects/web-app/backend-tasks.md",
    "web-app-drake"
);
```

### 3. Background Monitoring Kicks In

```
T=0s:  MonitoringService starts
       Waits 60 seconds...

T=60s: First monitoring cycle
       - Checks all Drakes
       - Drake sees tasks in Unassigned state
       - Drake loads tasks from file
       - Updates status to NotInitialized
       - Saves file

T=120s: Second cycle
       - User has summoned Kobolds manually
       - Or Drake auto-summons based on logic
       - Kobolds are Working
       - Monitoring logs: "3 working Kobolds"

T=180s: Third cycle
       - Some Kobolds finished
       - Drake updates tasks to Done
       - Unsummons completed Kobolds
       - Saves updated markdown
```

### 4. Manual Drake Operations

You can also interact with Drakes manually:

```csharp
// Execute task through Drake
var (messages, kobold) = await drake.ExecuteTaskAsync(
    task,
    "csharp",
    maxIterations: 5
);

// Monitor manually
drake.MonitorTasks();
var stats = drake.GetStatistics();
Console.WriteLine(stats);

// Cleanup
var removed = drake.UnsummonCompletedKobolds();
drake.UpdateTasksFile();
```

## Task Status Synchronization

### Markdown Format

```markdown
# Task Assignment Report

| Task | Assigned Agent | Status |
|------|----------------|--------|
| Create C# API | csharp | Working |
| Design database | diagramming | Done |
| Write tests | csharp | NotInitialized |
```

### Status Flow

```
Unassigned         - Task in file, no Kobold
     ↓
NotInitialized     - Kobold created, not started
     ↓
Working            - Kobold executing Agent.RunAsync()
     ↓
Done               - Kobold completed successfully
```

### Synchronization

**Drake → Markdown:**
- `drake.UpdateTasksFile()` writes current status
- Called after MonitorTasks() or ExecuteTaskAsync()

**Markdown → Drake:**
- `drakeFactory.CreateDrake(path)` loads initial state
- Tasks loaded via `LoadTasksFromFile()` (TODO: implement parser)

## Mutex Pattern

Prevents overlapping monitoring cycles:

```csharp
private bool _isRunning = false;
private readonly object _lock = new object();

protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromSeconds(_monitoringIntervalSeconds), stoppingToken);
        
        // Check if previous cycle still running
        lock (_lock)
        {
            if (_isRunning)
            {
                _logger.LogWarning("Previous monitoring cycle still running, skipping...");
                continue;
            }
            _isRunning = true;
        }
        
        try
        {
            await MonitorAllDrakesAsync();
        }
        finally
        {
            lock (_lock)
            {
                _isRunning = false;
            }
        }
    }
}
```

**Why it matters:**
- Long-running Kobold tasks could take > 60 seconds
- Without mutex, multiple cycles could pile up
- Mutex ensures only one monitoring cycle at a time

## Configuration

### Environment Variables

```bash
# OpenAI (default provider)
export OPENAI_API_KEY="sk-..."

# Alternative providers
export ANTHROPIC_API_KEY="sk-ant-..."
export AZURE_OPENAI_API_KEY="..."
export AZURE_OPENAI_ENDPOINT="https://..."
```

### Monitoring Interval

```csharp
// Fast monitoring (every 10 seconds)
new DrakeMonitoringService(logger, factory, monitoringIntervalSeconds: 10);

// Slow monitoring (every 5 minutes)
new DrakeMonitoringService(logger, factory, monitoringIntervalSeconds: 300);

// Default (every 60 seconds)
new DrakeMonitoringService(logger, factory);
```

### Drake Defaults

```csharp
var drakeFactory = new DrakeFactory(
    koboldFactory,
    defaultProvider: "anthropic",          // Provider for all Drakes
    defaultConfig: new Dictionary<string, string>
    {
        ["apiKey"] = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
        ["model"] = "claude-sonnet-4.5"
    },
    defaultOptions: new AgentOptions
    {
        WorkingDirectory = "./workspace",
        Verbose = true,                    // Log all agent actions
        MaxIterations = 10                 // Limit agent loops
    }
);
```

## Scaling Patterns

### Multiple Drakes for Different Teams

```csharp
// All task files are stored in project folders
// {ProjectsPath}/{project-name}/{area}-tasks.md

// Frontend team
var frontendDrake = drakeFactory.CreateDrake(
    "./projects/my-app/frontend-tasks.md",
    "frontend-drake"
);

// Backend team
var backendDrake = drakeFactory.CreateDrake(
    "./projects/my-app/backend-tasks.md",
    "backend-drake"
);

// DevOps team
var devopsDrake = drakeFactory.CreateDrake(
    "./projects/my-app/devops-tasks.md",
    "devops-drake"
);

// All monitored automatically every 60 seconds
```

### Per-Drake Configuration

```csharp
// Different providers per Drake
var drake1 = drakeFactory.CreateDrake("./projects/my-app/backend-tasks.md", "drake-gpt");
drake1.DefaultProvider = "openai";

var drake2 = drakeFactory.CreateDrake("./projects/my-app/frontend-tasks.md", "drake-claude");
drake2.DefaultProvider = "anthropic";
```

### Load Balancing Kobolds

```csharp
var stats = drake.GetStatistics();

if (stats.WorkingKobolds < 5)
{
    // Summon more Kobolds
    var tasks = drake.GetUnassignedTasks();
    foreach (var task in tasks.Take(5 - stats.WorkingKobolds))
    {
        await drake.SummonKoboldAsync(task, "csharp");
    }
}
```

## Troubleshooting

### Monitoring Service Not Running

Check registration in Program.cs:
```csharp
builder.Services.AddHostedService<DrakeMonitoringService>(...);
```

Verify logs:
```
[14:23:45] info: DrakeMonitoringService Starting monitoring service...
[14:24:45] info: DrakeMonitoringService Monitoring cycle started
```

### Overlapping Cycles

If you see:
```
[14:25:45] warn: DrakeMonitoringService Previous monitoring cycle still running, skipping...
```

This is **normal** if tasks take > 60 seconds. The mutex is working correctly.

To reduce frequency:
```csharp
new DrakeMonitoringService(logger, factory, monitoringIntervalSeconds: 120);
```

### Drake Not Finding Tasks

Check task file path (tasks are stored in project folders):
```csharp
var drake = drakeFactory.CreateDrake("./projects/my-project/backend-tasks.md", "drake");
// File must exist: ./projects/my-project/backend-tasks.md
```

Verify markdown format:
```markdown
| Task | Assigned Agent | Status |
|------|----------------|--------|
| ... | ... | ... |
```

### Kobolds Not Cleaning Up

Drake only unsummons **Done** Kobolds:
```csharp
var removed = drake.UnsummonCompletedKobolds();
// Returns count of Done Kobolds removed
```

Check status:
```csharp
var stats = drake.GetStatistics();
Console.WriteLine($"Done Kobolds: {stats.DoneKobolds}");
```

## API Reference

### DrakeFactory

```csharp
// Create Drake
Drake CreateDrake(string taskFilePath, string drakeName)

// Query
Drake? GetDrake(string drakeName)
IEnumerable<Drake> GetAllDrakes()
int TotalDrakes { get; }

// Private
TaskTracker LoadTasksFromFile(string filePath)  // TODO: implement parser
```

### DrakeMonitoringService

```csharp
// Constructor
DrakeMonitoringService(
    ILogger<DrakeMonitoringService> logger,
    DrakeFactory drakeFactory,
    int monitoringIntervalSeconds = 60,
    int stuckKoboldTimeoutMinutes = 30  // Timeout before Kobold is considered stuck
)

// Background methods
protected override Task ExecuteAsync(CancellationToken stoppingToken)
private Task MonitorAllDrakesAsync()
private Task MonitorSingleDrakeAsync(Drake drake)

// Constants
static readonly TimeSpan DefaultStuckTimeout  // 30 minutes
```

### Drake

```csharp
// Monitoring
void MonitorTasks()
int UnsummonCompletedKobolds()
void UpdateTasksFile()

// Stuck Kobold Detection
List<(Guid KoboldId, string? TaskId, TimeSpan WorkingDuration)> HandleStuckKobolds(TimeSpan timeout)
IReadOnlyCollection<(Kobold Kobold, TimeSpan WorkingDuration)> GetStuckKobolds(TimeSpan timeout)

// Execution
Task<(List<AgentMessage> messages, Kobold kobold)> ExecuteTaskAsync(
    TaskRecord task,
    string agentType,
    int maxIterations = 10,
    Action<string, string>? messageCallback = null
)

// Statistics
DrakeStatistics GetStatistics()
```

## File Structure

```
DraCode.KoboldLair/                    # Core Library
├── Factories/
│   └── DrakeFactory.cs                # Creates and manages Drake instances
├── Orchestrators/
│   └── Drake.cs                       # Task supervisor implementation
└── Models/
    ├── Tasks/
    │   ├── TaskRecord.cs              # Individual task record
    │   └── TaskTracker.cs             # Task tracking
    └── Agents/
        └── DrakeStatistics.cs         # Drake statistics model

DraCode.KoboldLair.Server/             # WebSocket Server
└── Services/
    └── DrakeMonitoringService.cs      # Background monitoring service (60s)
```

### Data Storage

Task files are stored in consolidated project folders:
```
{ProjectsPath}/                        # Configurable, default: ./projects
└── {project-name}/                    # Per-project folder
    ├── specification.md               # Project specification
    ├── backend-tasks.md               # Backend task file
    ├── frontend-tasks.md              # Frontend task file
    ├── {area}-tasks.md                # Other area task files
    └── workspace/                     # Generated code output
```

## Examples

See:
- `Examples/DrakeFactoryExample.cs` - Complete usage examples
- `Examples/DrakeExample.cs` - Drake operations
- `Examples/KoboldExample.cs` - Kobold lifecycle

## Next Steps

1. ✅ DrakeFactory created
2. ✅ DrakeMonitoringService created
3. ✅ Services registered in Program.cs
4. ✅ Documentation complete
5. ✅ Stuck Kobold detection and handling (configurable timeout)
6. ⏳ Test monitoring service with real tasks
7. ⏳ Implement LoadTasksFromFile markdown parser
8. ⏳ Add metrics/telemetry

## Related Documentation

- [Drake-Supervisor.md](Drake-Supervisor.md) - Drake detailed guide
- [Kobold-System.md](Kobold-System.md) - Kobold architecture
- [TaskTracking.md](TaskTracking.md) - Task status system
- [WyvernAgent.md](WyvernAgent.md) - Wyvern guide
