# 🦅 Drake Monitoring System

## Overview

The Drake Monitoring System provides automated background monitoring, task execution, and lifecycle management for Kobolds in the KoboldLair system. **Drakes work automatically** - they're created and managed by background services that run continuously.

Components:
- **DrakeFactory**: Creates and manages Drake instances from task file paths
- **DrakeExecutionService**: Background service that bridges Wyvern analysis to task execution (every 30s)
- **DrakeMonitoringService**: Background service that monitors Drakes and handles stuck Kobolds (every 60s)
- **ReasoningMonitorService**: Background service that checks working Kobolds for reasoning anomalies (every 45s)
- **Drake**: Supervisor that manages Kobolds, synchronizes with markdown task files, and handles escalations

**Drake is NOT interactive** - it automatically manages Kobold workers based on task files created by Wyvern.

## Architecture

```
┌─────────────────────────────────────────┐
│   DrakeExecutionService (Background)    │  ← Task execution (30s)
│   - Creates Drakes for task files       │
│   - Summons Kobolds for tasks           │
│   - Tracks project completion           │
└──────────────────┬──────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────┐
│   DrakeMonitoringService (Background)   │  ← Monitoring (60s)
│   - Monitors stuck Kobolds              │
│   - Handles timeouts                    │
│   - Mutex prevents overlapping runs     │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────┼──────────────────────┐
│  ReasoningMonitorService (Background)   │  ← Reasoning checks (45s)
│  - Detects stuck loops & stalled progress│
│  - Checks repeated errors & budget      │
│  - Creates EscalationAlerts → Drake     │
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

### 2. DrakeExecutionService (NEW in v2.4.1)

Background service that bridges Wyvern analysis to actual task execution. Runs every 30 seconds.

**Features:**
- Picks up analyzed projects and creates Drakes for each task file
- Ensures all task files have corresponding Drake supervisors
- Finds unassigned, ready tasks and summons Kobolds automatically
- Monitors project completion and updates status to `Completed`
- Thread-safe with mutex to prevent overlapping execution cycles

**Lifecycle:**
```
Start
  ↓
┌─────────────────┐
│ Wait 30 seconds │
└────────┬────────┘
         ↓
    Check _isExecuting
         │
    ┌────┴─────┐
    │ Running? │
    └────┬─────┘
    No   │   Yes
    ▼    │    ▼
Process  │   Skip cycle
Projects │   (Log warning)
    │    │
    ▼    │
1. Find analyzed projects
2. Create Drakes for task files
3. Find unassigned tasks
4. Summon Kobolds
5. Check completion
    │
    └──────> Loop back
```

**Execution Process:**
```csharp
// For each analyzed project
foreach (var project in GetAnalyzedProjects())
{
    // Ensure Drakes exist for all task files
    foreach (var taskFile in GetTaskFiles(project))
    {
        var drake = drakeFactory.GetOrCreateDrake(taskFile);

        // Find and execute unassigned tasks
        var unassignedTasks = drake.GetUnassignedTasks();
        foreach (var (task, agentType) in unassignedTasks)
        {
            await drake.SummonKobold(task, agentType);
        }
    }

    // Check if all tasks complete
    if (AllTasksComplete(project))
    {
        project.Status = ProjectStatus.Completed;
    }
}
```

**Configuration:**
```csharp
// In Program.cs
builder.Services.AddHostedService<DrakeExecutionService>(sp =>
{
    return new DrakeExecutionService(
        sp.GetRequiredService<ILogger<DrakeExecutionService>>(),
        sp.GetRequiredService<DrakeFactory>(),
        sp.GetRequiredService<ProjectService>(),
        executionIntervalSeconds: 30  // Configurable
    );
});
```

### 3. DrakeMonitoringService

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

**New Methods (v2.6.0):**
- `RunPostTaskVerificationAsync()` - Runs Critical-priority verification steps after task completion
  - Uses verification steps from Wyrm recommendations (e.g., `tsc --noEmit`, `dotnet build`)
  - Supports build, test, lint check types with configurable timeouts
  - Logs warnings on failure but does not block task completion

**Constraints Propagation (v2.6.0):**
- Drake collects constraints from both Wyrm (`wyrm-recommendation.json`) and Wyvern (`analysis.json`)
- Constraints displayed as prominent "⛔ PROJECT CONSTRAINTS" block at top of Kobold context
- Out-of-scope features listed to prevent accidental implementation
- Ensures Kobolds never violate spec restrictions (e.g., "no frameworks", "no runtime dependencies")

## Escalation Handling

When a Kobold encounters a problem it cannot resolve on its own, it creates an `EscalationAlert` that is routed through Drake for upstream handling.

### EscalationAlert Model

```csharp
public class EscalationAlert
{
    public string Id { get; set; }
    public string ProjectId { get; set; }
    public string? TaskId { get; set; }
    public Guid KoboldId { get; set; }
    public string AgentType { get; set; }
    public EscalationSource Source { get; set; }    // ReflectionTool or ReasoningMonitor
    public EscalationType Type { get; set; }
    public string Summary { get; set; }
    public List<ReflectionEntry> ReflectionHistory { get; set; }
    public EscalationStatus Status { get; set; }    // Pending → InProgress → Resolved/Failed
    public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum EscalationType
{
    TaskInfeasible,
    MissingDependency,
    NeedsSplit,
    WrongApproach,
    WrongAgentType
}
```

### HandleEscalationAsync(EscalationAlert alert)

The `HandleEscalationAsync` method on Drake receives escalation alerts and routes them to the appropriate upstream agent based on the `EscalationType`:

| EscalationType | Routing Target | Action |
|----------------|---------------|--------|
| `WrongApproach` | **Planner** (KoboldPlannerAgent) | Calls `RevisePlanAsync()` to revise the implementation plan using reflection history. The revised plan replaces the Kobold's current plan and is saved via `PlanService`. |
| `TaskInfeasible` | **Wyvern** | Calls `RefineTaskAsync()` to re-analyze the task. If refinement produces changes, the current task is marked as `Failed` with the escalation reason. |
| `NeedsSplit` | **Wyvern** | Same as TaskInfeasible - routes to Wyvern for task refinement, which may split the task into smaller subtasks. |
| `MissingDependency` | **Wyvern** | Same as TaskInfeasible - routes to Wyvern to identify and create the missing dependency tasks. |
| `WrongAgentType` | **Task reassignment** | Resets the task to `Unassigned` status with no assigned agent, clears the error message, and saves. Drake's next execution cycle picks a different agent type for the task. |

After routing, the alert status is set to `Resolved` (or `Failed` if an exception occurs) with a resolution summary.

### Notification to Dragon Client

After every escalation is resolved (or fails), Drake invokes the `_onEscalationNotify` callback:

```csharp
_onEscalationNotify?.Invoke(projectName, alert, resolution);
```

This callback is wired to `ProjectNotificationService`, which pushes the escalation outcome to the Dragon client via WebSocket. This keeps the user informed about automated recovery actions without requiring manual intervention.

### Wiring on Kobold

In `SummonKoboldAsync`, Drake sets the `OnEscalation` callback on each Kobold:

```csharp
kobold.OnEscalation = alert =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            await HandleEscalationAsync(alert);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unhandled error in escalation handler for Kobold {KoboldId}",
                alert.KoboldId.ToString()[..8]);
        }
    });
};
```

This fire-and-forget pattern ensures that escalation handling does not block the Kobold's execution thread. Escalations can originate from two sources:
- **ReflectionTool** (`EscalationSource.ReflectionTool`): When a Kobold's self-reflection detects low confidence or blockers.
- **ReasoningMonitorService** (`EscalationSource.ReasoningMonitor`): When the background monitor detects anomalies externally.

## ReasoningMonitorService

Background service that externally monitors working Kobolds for reasoning anomalies. Unlike Kobold self-reflection (which relies on the LLM recognizing its own issues), the ReasoningMonitorService inspects execution state from the outside.

### Overview

- Extends `PeriodicBackgroundService` with a default interval of **45 seconds** and a 30-second initial delay.
- Iterates over all Kobolds with `KoboldStatus.Working` via `KoboldFactory.GetKoboldsByStatus()`.
- When an anomaly is detected, creates an `EscalationAlert` with `Source = EscalationSource.ReasoningMonitor`.
- Routes the alert through `Drake.HandleEscalationAsync` by looking up the Kobold's project Drake via `DrakeFactory.GetDrakesByProject()`.
- Includes a 5-minute deduplication window to avoid repeated alerts for the same issue type on the same Kobold.

### Detection Checks

| Check | Condition | Escalation Type |
|-------|-----------|-----------------|
| **Stuck loop** | Same file operation repeated `MaxFileWriteRepetitions`+ times in the execution log | `WrongApproach` |
| **Stalled progress** | No step completions for `NoProgressTimeoutMinutes` minutes, but LLM is still responding | `WrongApproach` |
| **Repeated errors** | Last `StallDetectionCount` reflections all report the same blocker string | `WrongApproach` |
| **Budget exhaustion** | >10 reflections with <50% step completion and confidence below 50% | `NeedsSplit` |

### Configuration

Configured via `ReflectionConfiguration` in `appsettings.json` under `KoboldLair:Reflection`:

```json
{
  "KoboldLair": {
    "Reflection": {
      "Enabled": true,
      "EscalationConfidenceThreshold": 30,
      "StallDetectionCount": 3,
      "MonitorIntervalSeconds": 45,
      "NoProgressTimeoutMinutes": 10,
      "MaxFileWriteRepetitions": 3
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Master switch for the reflection and monitoring system |
| `EscalationConfidenceThreshold` | `30` | Confidence % below which ReflectionTool triggers escalation |
| `StallDetectionCount` | `3` | Consecutive reflections with same blocker before escalation |
| `MonitorIntervalSeconds` | `45` | Interval between ReasoningMonitor check cycles |
| `NoProgressTimeoutMinutes` | `10` | Minutes without step progress before flagging stall |
| `MaxFileWriteRepetitions` | `3` | File write repetition count before flagging stuck loop |

### Service Registration

```csharp
builder.Services.AddHostedService<ReasoningMonitorService>(sp =>
{
    return new ReasoningMonitorService(
        sp.GetRequiredService<ILogger<ReasoningMonitorService>>(),
        sp.GetRequiredService<KoboldFactory>(),
        sp.GetRequiredService<DrakeFactory>(),
        sp.GetRequiredService<ReflectionConfiguration>(),
        monitorIntervalSeconds: config.MonitorIntervalSeconds
    );
});
```

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

// Private - loads tasks from markdown file on Drake creation
void LoadTasksFromFile(TaskTracker taskTracker, string filePath)
```

### TaskTracker

```csharp
// Persistence
int LoadFromFile(string filePath)           // Load tasks from markdown file
int LoadFromMarkdown(string markdown)       // Load tasks from markdown string
void SaveToFile(string filePath, string? title)  // Save tasks to markdown file
string GenerateMarkdown(string? title)      // Generate markdown report

// Task management
TaskRecord AddTask(string task)
void UpdateTask(TaskRecord task, TaskStatus status, string? assignedAgent)
void SetError(TaskRecord task, string errorMessage)
List<TaskRecord> GetAllTasks()
TaskRecord? GetTaskById(string id)
Dictionary<TaskStatus, int> GetStatusCounts()
void Clear()
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
    ├── Agents/
    │   ├── DrakeStatistics.cs         # Drake statistics model
    │   └── EscalationAlert.cs         # Escalation model and enums
    └── Configuration/
        └── KoboldLairConfiguration.cs # Includes ReflectionConfiguration

DraCode.KoboldLair.Server/             # WebSocket Server
└── Services/
    ├── DrakeExecutionService.cs       # Task execution service (30s)
    ├── DrakeMonitoringService.cs      # Background monitoring service (60s)
    └── ReasoningMonitorService.cs     # Reasoning anomaly detection (45s)
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
3. ✅ DrakeExecutionService created (v2.4.1)
4. ✅ Services registered in Program.cs
5. ✅ Documentation complete
6. ✅ Stuck Kobold detection and handling (configurable timeout)
7. ✅ LoadTasksFromFile markdown parser (TaskTracker.LoadFromFile)
8. ✅ End-to-end automation: Dragon → Wyvern → Drake → Kobold
9. ⏳ Add metrics/telemetry
10. ⏳ Dashboard for Drake statistics

## Related Documentation

- [Drake-Supervisor.md](Drake-Supervisor.md) - Drake detailed guide
- [Kobold-System.md](Kobold-System.md) - Kobold architecture
- [TaskTracking.md](TaskTracking.md) - Task status system
- [WyvernAgent.md](WyvernAgent.md) - Wyvern guide
