# 👹 Kobold Worker System

## Overview
The Kobold system provides a worker pattern for managing AI agent instances in KoboldLair. Each Kobold is a dedicated worker agent that handles one specific task at a time, with full lifecycle tracking.

**Kobolds work automatically** - they're created and managed by Drake supervisors via the DrakeExecutionService (runs every 30s), assigned tasks automatically, and execute code generation without user interaction.

## Architecture

### Components

1. **Kobold** - Worker agent wrapper
2. **KoboldStatus** - Status enumeration
3. **KoboldFactory** - Factory for creating and managing Kobolds
4. **KoboldStatistics** - Statistics about Kobold instances

## Kobold Class

### Properties

```csharp
public class Kobold
{
    public Guid Id { get; }                        // Unique Kobold identifier
    public Agent Agent { get; }                    // Underlying AI agent
    public string AgentType { get; }               // Type: "csharp", "react", etc.
    public Guid? TaskId { get; }                   // Assigned task ID
    public KoboldStatus Status { get; }            // Current status
    public DateTime CreatedAt { get; }             // Creation timestamp
    public DateTime? AssignedAt { get; }           // Task assignment time
    public DateTime? StartedAt { get; }            // Work start time
    public DateTime? CompletedAt { get; }          // Completion time
    public string? ErrorMessage { get; }           // Error message if any
    public Action<EscalationAlert>? OnEscalation { get; set; } // Callback when Kobold escalates
}
```

### Lifecycle

```
Unassigned → Assigned → Working → Done
                              ↘
                               Failed
```

1. **Unassigned**: Kobold created but not assigned to a task
2. **Assigned**: Task assigned but work not started
3. **Working**: Actively processing the task
4. **Done**: Task completed successfully
5. **Failed**: Task failed due to errors (network errors, provider issues, etc.)

### Error Handling

**Network and Provider Errors** (v2.5.1+):
- When LLM providers encounter errors (network failures, timeouts, configuration issues), tasks are now **properly marked as Failed**
- **Fix in v2.5.1**: Previously, network errors after retry exhaustion could incorrectly mark tasks as "Done"
  - Issue: Providers returned `StopReason = "error"` with empty content, which bypassed error detection
  - Solution: `Agent.cs` now injects error messages into conversation when stop reason indicates error:
    - `"error"` stop reason → "Error: An error occurred during LLM request."
    - `"NotConfigured"` stop reason → "Error: Provider not properly configured."
- Error detection checks the last assistant message for error patterns:
  - "Error occurred during LLM request" - Network/API errors
  - "Provider not properly configured" - Configuration errors
- All error types properly set `ErrorMessage` and transition to `Failed` status
- Applies to all 10 LLM providers (OpenAI, Claude, Gemini, Z.AI, etc.)
- Applies to all agent types (Dragon, Wyvern, Drake, Kobold)

### Execution Rules (v2.6.0)

Kobold execution now includes mandatory rules injected into the execution prompt:

**1. Read Before Write (Mandatory)**
- Every file modification must be preceded by reading the current file contents
- Prevents overwriting changes made by other tasks

**2. No Duplicate Declarations (Mandatory)**
- When modifying shared files (types, configs), Kobolds must not add duplicate interfaces, classes, or functions
- Must extend or update existing declarations instead

**3. Import Consistency (Mandatory)**
- Imports must use actual exported names from target modules
- Function signatures must match exactly (parameter count, types, return types)

**4. Integration Task Protocol**
- Tasks with 4+ dependencies trigger the integration protocol
- Forces reading ALL dependency files before writing any code
- Prevents the #1 source of integration bugs: assumed API signatures

**5. Constraints Enforcement**
- Project constraints (from Wyrm/Wyvern) displayed as prominent "⛔ PROJECT CONSTRAINTS" block
- Out-of-scope features listed to prevent accidental implementation

### Methods

```csharp
// Assign task to Kobold
void AssignTask(Guid taskId)

// Start working on assigned task
void StartWorking()

// Mark task as done
void MarkDone()

// Set error message
void SetError(string errorMessage)

// Reset Kobold for reuse
void Reset()
```

## KoboldFactory

### Purpose
Centralized factory for creating and managing all Kobold instances in the system.

### Features
- Thread-safe Kobold registry using `ConcurrentDictionary`
- Create Kobolds with specific agent types
- Track all Kobolds by status, type, or task
- Generate statistics
- Cleanup completed Kobolds

### Usage Example

```csharp
// Create factory with default settings
var factory = new KoboldFactory(
    defaultOptions: new AgentOptions { WorkingDirectory = "./workspace" },
    defaultConfig: new Dictionary<string, string>
    {
        ["apiKey"] = "your-api-key",
        ["model"] = "gpt-4o"
    }
);

// Create a C# specialist Kobold
var kobold = factory.CreateKobold("openai", "csharp");

// Assign to task
kobold.AssignTask(taskId);

// Start working
kobold.StartWorking();

// Execute task
var result = await kobold.Agent.RunAsync("Refactor this method...");

// Mark as done
kobold.MarkDone();

// Get statistics
var stats = factory.GetStatistics();
Console.WriteLine(stats); // Total: 5, Working: 2, Done: 3...
```

### Factory Methods

#### Creation
```csharp
// Create a new Kobold with specific provider
Kobold CreateKobold(
    string provider, 
    string agentType, 
    AgentOptions? options = null,
    Dictionary<string, string>? config = null)
```

#### Retrieval
```csharp
// Get by ID
Kobold? GetKobold(Guid koboldId)

// Get by task ID
Kobold? GetKoboldByTaskId(Guid taskId)

// Get all Kobolds
IReadOnlyCollection<Kobold> GetAllKobolds()

// Get by status
IReadOnlyCollection<Kobold> GetKoboldsByStatus(KoboldStatus status)

// Get unassigned Kobolds
IReadOnlyCollection<Kobold> GetUnassignedKobolds()

// Get working Kobolds
IReadOnlyCollection<Kobold> GetWorkingKobolds()

// Get by agent type
IReadOnlyCollection<Kobold> GetKoboldsByType(string agentType)
```

#### Management
```csharp
// Remove specific Kobold
bool RemoveKobold(Guid koboldId)

// Cleanup all done Kobolds
int CleanupDoneKobolds()

// Clear all Kobolds
void Clear()

// Get statistics
KoboldStatistics GetStatistics()
```

## KoboldStatistics

Provides aggregate statistics about Kobold instances:

```csharp
public class KoboldStatistics
{
    public int Total { get; init; }
    public int Unassigned { get; init; }
    public int Assigned { get; init; }
    public int Working { get; init; }
    public int Done { get; init; }
    public Dictionary<string, int> ByAgentType { get; init; }
}
```

## Integration Patterns

### Pattern 1: Simple Task Execution

```csharp
var factory = new KoboldFactory();

// Create Kobold for task
var kobold = factory.CreateKobold("openai", "csharp");
kobold.AssignTask(taskId);
kobold.StartWorking();

try
{
    var result = await kobold.Agent.RunAsync(taskDescription);
    kobold.MarkDone();
}
catch (Exception ex)
{
    kobold.SetError(ex.Message);
}
finally
{
    UpdatePlanStepTool.ClearContext();
    ReflectionTool.ClearContext();
}
```

### Pattern 2: Kobold Pool

```csharp
var factory = new KoboldFactory();

// Pre-create pool of Kobolds
for (int i = 0; i < 5; i++)
{
    factory.CreateKobold("openai", "csharp");
}

// Assign work to unassigned Kobolds
var availableKobold = factory.GetUnassignedKobolds().FirstOrDefault();
if (availableKobold != null)
{
    availableKobold.AssignTask(taskId);
    availableKobold.StartWorking();
    // ... do work
}
```

### Pattern 3: Specialized Agent Assignment

```csharp
var factory = new KoboldFactory();

// Create specialized Kobolds with different providers
var csharpKobold = factory.CreateKobold("openai", "csharp");
var reactKobold = factory.CreateKobold("claude", "react");
var cssKobold = factory.CreateKobold("gemini", "css");

// Assign based on task type
string taskType = DetermineTaskType(task);
var kobold = factory.GetKoboldsByType(taskType)
    .FirstOrDefault(k => k.Status == KoboldStatus.Unassigned);

if (kobold != null)
{
    kobold.AssignTask(taskId);
    // ... execute
}
```

## Per-Agent-Type Provider Configuration

Configure different LLM providers for different Kobold agent types (e.g., use Claude for `csharp` Kobolds, OpenAI for `python` Kobolds).

### Configuration Format (user-settings.json)

```json
{
  "koboldProvider": "openai",
  "koboldModel": null,
  "koboldAgentTypeSettings": [
    { "agentType": "csharp", "provider": "claude", "model": "claude-sonnet-4-20250514" },
    { "agentType": "python", "provider": "openai", "model": "gpt-4o" },
    { "agentType": "react", "provider": "gemini", "model": null }
  ]
}
```

### Resolution Precedence

When Drake summons a Kobold, the provider is resolved in this order:

1. **Explicit provider parameter** - If `SummonKobold(task, agentType, provider)` is called with a provider
2. **Agent-type-specific setting** - `koboldAgentTypeSettings[agentType]` if matching entry exists
3. **Global Kobold fallback** - `koboldProvider` / `koboldModel`
4. **System default** - `defaultProvider` from appsettings.json

### API Methods

**ProviderConfigurationService**:

```csharp
// Get provider for a specific Kobold agent type
string provider = providerConfigService.GetProviderForKoboldAgentType("csharp");

// Get full provider settings (provider, config, options)
var (provider, config, options) = providerConfigService
    .GetProviderSettingsForKoboldAgentType("csharp", "./workspace");

// Set provider for a specific agent type (persisted to user-settings.json)
providerConfigService.SetProviderForKoboldAgentType("csharp", "claude", "claude-sonnet-4-20250514");

// Remove agent-type-specific setting (falls back to global)
providerConfigService.SetProviderForKoboldAgentType("csharp", null, null);
```

### Example: Mixed Provider Strategy

```csharp
// Configure user-settings.json with mixed providers:
// - Claude for C# (strong at .NET patterns)
// - OpenAI for Python (extensive training data)
// - Gemini for frontend (good CSS/React)

// Drake automatically resolves the correct provider when summoning:
var kobold = drake.SummonKobold(csharpTask, "csharp");  // Uses Claude
var kobold2 = drake.SummonKobold(pythonTask, "python"); // Uses OpenAI
var kobold3 = drake.SummonKobold(reactTask, "react");   // Uses Gemini
```

### Pattern 4: Monitoring and Cleanup

```csharp
var factory = new KoboldFactory();

// Periodic cleanup task
async Task MonitorKobolds()
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromMinutes(5));
        
        // Get statistics
        var stats = factory.GetStatistics();
        Console.WriteLine($"Kobolds: {stats}");
        
        // Cleanup done Kobolds
        int removed = factory.CleanupDoneKobolds();
        Console.WriteLine($"Cleaned up {removed} done Kobolds");
        
        // Check for stuck Kobolds
        var workingKobolds = factory.GetWorkingKobolds();
        foreach (var kobold in workingKobolds)
        {
            if (kobold.StartedAt < DateTime.UtcNow.AddHours(-1))
            {
                Console.WriteLine($"Warning: Kobold {kobold.Id} stuck for over 1 hour");
            }
        }
    }
}
```

## Integration with WyvernService

The Kobold system can be integrated with the existing WyvernService:

```csharp
public class WyvernService
{
    private readonly KoboldFactory _koboldFactory;
    
    public WyvernService()
    {
        _koboldFactory = new KoboldFactory();
    }
    
    private async Task HandleTaskExecution(TaskRecord task, string provider)
    {
        // Get or create appropriate Kobold
        var kobold = _koboldFactory.CreateKobold(provider, task.AssignedAgent);
        
        kobold.AssignTask(task.Id);
        kobold.StartWorking();
        
        try
        {
            var result = await kobold.Agent.RunAsync(task.Task);
            kobold.MarkDone();
            
            // Update task record
            _taskTracker.UpdateTask(task, TaskStatus.Done);
        }
        catch (Exception ex)
        {
            kobold.SetError(ex.Message);
            // Handle error...
        }
    }
}
```

## Benefits

1. **Single Responsibility**: Each Kobold handles one task
2. **Lifecycle Tracking**: Full timestamps for task progress
3. **Resource Management**: Factory tracks all instances
4. **Statistics**: Real-time monitoring of worker status
5. **Reusability**: Kobolds can be reset and reused
6. **Thread-Safe**: ConcurrentDictionary for multi-threaded scenarios
7. **Type Safety**: Strongly typed agent assignments

## File Structure

```
DraCode.KoboldLair/                    # Core Library
├── Agents/                            # Agent Implementations
│   ├── DragonAgent.cs                 # Interactive requirements gathering
│   ├── WyrmAgent.cs                   # Project analyzer
│   └── WyvernAgent.cs                 # Task delegator
├── Factories/                         # Factory Pattern - Resource Creation
│   ├── KoboldFactory.cs               # Creates Kobolds with parallel limits
│   ├── DrakeFactory.cs                # Creates Drake supervisors
│   ├── WyvernFactory.cs               # Creates Wyvern orchestrators
│   └── WyrmFactory.cs                 # Creates Wyrm analyzers
├── Orchestrators/                     # High-Level Orchestration
│   ├── Drake.cs                       # Task supervisor
│   ├── WyrmRunner.cs                  # Task running orchestrator
│   └── Wyvern.cs                      # Task delegation orchestrator
├── Models/                            # Data Models (Organized by Domain)
│   ├── Agents/                        # Agent-related models
│   │   ├── Kobold.cs                  # Worker agent wrapper
│   │   ├── KoboldStatus.cs            # Status enumeration
│   │   ├── KoboldStatistics.cs        # Statistics model
│   │   └── DrakeStatistics.cs         # Drake statistics
│   ├── Tasks/                         # Task-related models
│   │   ├── TaskRecord.cs              # Individual task record
│   │   └── TaskTracker.cs             # Task tracking
│   ├── Projects/                      # Project models
│   │   └── Project.cs                 # Project entity
│   └── Configuration/                 # Config models
│       └── ProjectConfig.cs           # Per-project settings
└── Services/                          # Core Services
    ├── ProjectService.cs              # Project management
    └── ProviderConfigurationService.cs # Provider config

DraCode.KoboldLair.Server/             # WebSocket Server
├── Agents/
│   └── AgentFactory.cs                # Creates agents with system prompts
├── Services/                          # Server-side Services
│   ├── DragonService.cs               # Dragon WebSocket service
│   ├── DrakeMonitoringService.cs      # Background monitoring (60s)
│   └── WyvernProcessingService.cs     # Background processing (60s)
└── Program.cs                         # Service registration
```

### Data Storage

All project data is stored in consolidated per-project folders:
```
{ProjectsPath}/                        # Configurable, default: ./projects
├── projects.json                      # Project registry
└── {project-name}/                    # Per-project folder
    ├── specification.md               # Project specification
    ├── specification.features.json    # Feature list
    ├── {area}-tasks.md                # Task files
    ├── analysis.md                    # Wyvern analysis
    └── workspace/                     # Generated code output
```

## Self-Reflection System

Kobolds periodically assess their own progress and can escalate problems to Drake before wasting further tokens on blocked or infeasible work.

### Reflect Tool

The `reflect` tool is injected alongside `update_plan_step` in both `StartWorkingWithPlanAsync` and `StartWorkingWithPlanEnhancedAsync`. It follows the same static context pattern as `UpdatePlanStepTool` (static `SetContext` / `ClearContext` methods).

#### Tool Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `progress_percent` | int | Yes | Overall plan progress estimate (0-100) |
| `blockers` | string | No | Description of current blockers, if any |
| `confidence_percent` | int | Yes | Confidence the plan will succeed (0-100) |
| `decision` | string | Yes | One of: `continue`, `adjust`, `escalate` |
| `escalation_type` | string | No | Required when `decision == "escalate"` (see types below) |
| `adjustment` | string | No | Description of self-correction when `decision == "adjust"` |

### Prompt Integration

- The **CHECKPOINT** prompt (injected at intervals during plan execution) now says **"REFLECTION REQUIRED"** and instructs the Kobold to call the `reflect` tool before proceeding to the next step.
- The **SELF-REFLECTION PROTOCOL** section in `BuildFullPromptWithPlanAsync` is updated for both sequential and parallel execution modes, telling the Kobold when and how to reflect.

### Auto-Escalation Rules

The `ReflectionTool` evaluates each reflection and may auto-escalate to Drake:

1. **Stall Detection**: If the last N reflections (configurable, default 3) show no meaningful progress increase, the tool auto-escalates with a stall alert.
2. **Low Confidence**: If `confidence_percent` drops below a threshold (configurable, default 30%), the tool auto-escalates regardless of progress.
3. **Explicit Escalation**: If `decision == "escalate"`, the Kobold is requesting help directly. An `EscalationAlert` is created with the specified `escalation_type`.

### Escalation Types

| Type | Meaning |
|------|---------|
| `TaskInfeasible` | The task cannot be completed as specified |
| `MissingDependency` | A required dependency is unavailable or incomplete |
| `NeedsSplit` | The task is too large and should be broken into subtasks |
| `WrongApproach` | The current implementation approach is fundamentally flawed |
| `WrongAgentType` | A different specialist agent type would be more appropriate |

### Data Model

Reflection data is stored on the implementation plan:

```csharp
// Stored in KoboldImplementationPlan.Reflections
public class ReflectionEntry
{
    public DateTime Timestamp { get; set; }
    public int ProgressPercent { get; set; }
    public int ConfidencePercent { get; set; }
    public string Decision { get; set; }        // "continue", "adjust", "escalate"
    public string? Blockers { get; set; }
    public string? EscalationType { get; set; }
    public string? Adjustment { get; set; }
}

// Stored in KoboldImplementationPlan.Escalations
public class EscalationAlert
{
    public DateTime Timestamp { get; set; }
    public string EscalationType { get; set; }
    public string Reason { get; set; }
    public int ProgressAtEscalation { get; set; }
    public int ConfidenceAtEscalation { get; set; }
}
```

Drake receives escalation alerts via the `Kobold.OnEscalation` callback and can decide to reassign, split, or cancel the task.

## Future Enhancements

1. **Kobold Events**: Add events for status changes
2. **Kobold Metrics**: Detailed performance metrics per Kobold
3. **Priority Queues**: Assign tasks based on priority
4. **Load Balancing**: Distribute tasks across available Kobolds
5. **Kobold Health Checks**: Periodic health monitoring
6. **Kobold Scaling**: Auto-scale Kobold pool based on workload
7. **Persistent Storage**: Save Kobold state to database
8. **Kobold Clustering**: Distribute Kobolds across multiple servers

## Related Documentation

- [KoboldLair Server README](../DraCode.KoboldLair.Server/README.md) - Complete server documentation
- [Drake Monitoring System](./Drake-Monitoring-System.md) - Task supervision
- [Wyvern Project Analyzer](./Wyvern-Project-Analyzer.md) - Task organization
