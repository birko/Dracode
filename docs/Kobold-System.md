# 👹 Kobold Worker System

## Overview
The Kobold system provides a worker pattern for managing AI agent instances in KoboldTown. Each Kobold is a dedicated worker agent that handles one specific task at a time, with full lifecycle tracking.

**Kobolds work automatically** - they're created and managed by Drake supervisors, assigned tasks automatically, and execute code generation without user interaction.

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
}
```

### Lifecycle

```
Unassigned → Assigned → Working → Done
```

1. **Unassigned**: Kobold created but not assigned to a task
2. **Assigned**: Task assigned but work not started
3. **Working**: Actively processing the task
4. **Done**: Task completed

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
DraCode.KoboldTown/
├── Models/
│   ├── Kobold.cs              # Worker agent wrapper
│   └── KoboldStatus.cs        # Status enumeration
└── Factories/
    └── KoboldFactory.cs       # Factory and statistics
```

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

- [KoboldTownAgentFactory.md](./KoboldTownAgentFactory.md) - Agent factory details
- [WyvernAgent.md](./WyvernAgent.md) - Task orchestration
- [TaskTracking.md](./TaskTracking.md) - Task management system
