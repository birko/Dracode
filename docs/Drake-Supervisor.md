# Drake Supervisor System

## Overview
The **Drake** is a supervisor class that orchestrates the lifecycle of Kobolds based on task status. It acts as the central coordinator between tasks (TaskTracker) and workers (KoboldFactory), automatically managing Kobold summoning, task execution, and status synchronization.

## Concept

```
        Drake (Supervisor)
             |
    ┌────────┴────────┐
    |                 |
TaskTracker    KoboldFactory
 (Tasks)         (Workers)
```

The Drake:
- **Monitors** tasks from TaskTracker
- **Summons** Kobolds via KoboldFactory
- **Assigns** Kobolds to tasks
- **Executes** tasks through Kobolds
- **Updates** task status based on Kobold progress
- **Unsummons** completed Kobolds
- **Saves** updates to markdown file

## Key Features

✅ **Lifecycle Management** - Complete Kobold lifecycle from summon to unsummon  
✅ **Auto Status Sync** - Task status updates automatically with Kobold progress  
✅ **Markdown Output** - Continuously updates task report file  
✅ **Task-Kobold Mapping** - Tracks which Kobold is working on which task  
✅ **Statistics** - Real-time statistics on tasks and Kobolds  
✅ **Error Handling** - Captures and reports Kobold failures  

## Drake Class

### Constructor

```csharp
public Drake(
    KoboldFactory koboldFactory,
    TaskTracker taskTracker,
    string outputMarkdownPath,
    string defaultProvider = "openai",
    Dictionary<string, string>? defaultConfig = null,
    AgentOptions? defaultOptions = null)
```

### Core Methods

#### Summon Kobold
```csharp
// Creates a Kobold and assigns it to a task
Kobold SummonKobold(
    TaskRecord task, 
    string agentType, 
    string? provider = null)
```

#### Unsummon Kobold
```csharp
// Removes a Kobold from the factory
bool UnsummonKobold(Guid koboldId)
```

#### Start Work
```csharp
// Starts a Kobold working on its assigned task
void StartKoboldWork(Guid koboldId)
```

#### Complete Work
```csharp
// Marks Kobold's work as complete and updates task
void CompleteKoboldWork(
    Guid koboldId, 
    string? errorMessage = null)
```

#### Execute Task (High-Level)
```csharp
// Complete workflow: summon → work → complete
Task<(List<Message> messages, Kobold kobold)> ExecuteTaskAsync(
    TaskRecord task,
    string agentType,
    int maxIterations = 30,
    string? provider = null,
    Action<string, string>? messageCallback = null)
```

### Monitoring Methods

```csharp
// Monitor all tasks and sync status
void MonitorTasks()

// Unsummon all completed Kobolds
int UnsummonCompletedKobolds()

// Get Kobold for specific task
Kobold? GetKoboldForTask(string taskId)

// Get statistics
DrakeStatistics GetStatistics()

// Force save to markdown
void UpdateTasksFile()
```

## DrakeStatistics

```csharp
public class DrakeStatistics
{
    public int TotalKobolds { get; init; }
    public int UnassignedKobolds { get; init; }
    public int AssignedKobolds { get; init; }
    public int WorkingKobolds { get; init; }
    public int DoneKobolds { get; init; }
    public int TotalTasks { get; init; }
    public int UnassignedTasks { get; init; }
    public int WorkingTasks { get; init; }
    public int DoneTasks { get; init; }
    public int ActiveAssignments { get; init; }
}
```

## Usage Examples

### Example 1: Simple Task Execution

```csharp
// Setup
var koboldFactory = new KoboldFactory();
var taskTracker = new TaskTracker();
var Drake = new Drake(
    koboldFactory, 
    taskTracker, 
    "./tasks.md",
    defaultProvider: "openai"
);

// Add task
var task = taskTracker.AddTask("Create a C# hello world program");

// Execute through Drake
var (messages, kobold) = await Drake.ExecuteTaskAsync(
    task, 
    "csharp",
    messageCallback: (type, msg) => Console.WriteLine(msg)
);

// Check results
Console.WriteLine($"Completed with {messages.Count} messages");
Console.WriteLine($"Kobold: {kobold}");
```

### Example 2: Manual Control

```csharp
var task = taskTracker.AddTask("Write React component");

// 1. Summon Kobold
var kobold = Drake.SummonKobold(task, "react");
Console.WriteLine($"Summoned: {kobold}");

// 2. Start work
Drake.StartKoboldWork(kobold.Id);
Console.WriteLine($"Working...");

// 3. Execute manually
var messages = await kobold.Agent.RunAsync(task.Task);

// 4. Complete
Drake.CompleteKoboldWork(kobold.Id);
Console.WriteLine($"Done!");
```

### Example 3: Batch Processing

```csharp
var tasks = new[]
{
    taskTracker.AddTask("Task 1"),
    taskTracker.AddTask("Task 2"),
    taskTracker.AddTask("Task 3")
};

// Process in parallel
var executionTasks = tasks.Select(task =>
    Drake.ExecuteTaskAsync(task, "csharp", maxIterations: 10)
);

await Task.WhenAll(executionTasks);

// Check statistics
var stats = Drake.GetStatistics();
Console.WriteLine(stats);

// Cleanup
int cleaned = Drake.UnsummonCompletedKobolds();
Console.WriteLine($"Cleaned up {cleaned} Kobolds");
```

### Example 4: Continuous Monitoring

```csharp
// Start long-running tasks
var task1 = taskTracker.AddTask("Long task 1");
var task2 = taskTracker.AddTask("Long task 2");

_ = Task.Run(async () => await Drake.ExecuteTaskAsync(task1, "csharp"));
_ = Task.Run(async () => await Drake.ExecuteTaskAsync(task2, "react"));

// Monitor progress
while (true)
{
    await Task.Delay(5000);
    
    Drake.MonitorTasks();
    var stats = Drake.GetStatistics();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {stats}");
    
    // Check specific Kobolds
    var kobold1 = Drake.GetKoboldForTask(task1.Id);
    if (kobold1 != null)
        Console.WriteLine($"  Task 1: {kobold1.Status}");
    
    if (stats.WorkingKobolds == 0)
        break; // All done
}
```

## Lifecycle Flow

```
1. Task Created
   └─> taskTracker.AddTask(...)
   
2. Drake Summons Kobold
   └─> Drake.SummonKobold(task, agentType)
       ├─> koboldFactory.CreateKobold(...)
       ├─> kobold.AssignTask(taskId)
       ├─> Map task → kobold
       └─> Update task status to NotInitialized
   
3. Drake Starts Work
   └─> Drake.StartKoboldWork(koboldId)
       ├─> kobold.StartWorking()
       └─> Update task status to Working
   
4. Kobold Executes
   └─> kobold.Agent.RunAsync(task)
   
5. Drake Completes Work
   └─> Drake.CompleteKoboldWork(koboldId)
       ├─> kobold.MarkDone()
       ├─> Update task status to Done
       └─> Save to markdown file
   
6. Drake Unsummons (Optional)
   └─> Drake.UnsummonKobold(koboldId)
       ├─> Remove from task mapping
       └─> Remove from factory
```

## Integration with WyvernService

```csharp
public class WyvernService
{
    private readonly Drake _Drake;
    
    public WyvernService()
    {
        var factory = new KoboldFactory();
        var tracker = new TaskTracker();
        
        _Drake = new Drake(
            factory, 
            tracker, 
            "./tasks.md",
            "openai"
        );
    }
    
    public async Task<string> HandleTaskSubmission(string taskDescription)
    {
        // Add task
        var task = _Drake.TaskTracker.AddTask(taskDescription);
        
        // Use Wyvern to select agent type
        var agentType = await SelectAgentType(taskDescription);
        
        // Execute via Drake
        var (messages, kobold) = await _Drake.ExecuteTaskAsync(
            task,
            agentType,
            messageCallback: SendToWebSocket
        );
        
        return $"Task completed by Kobold {kobold.Id}";
    }
}
```

## Markdown Output

The Drake automatically saves task updates to the specified markdown file:

```markdown
# Drake Task Report

**Summary:**
- Total Tasks: 5
- Unassigned: 📝 0
- Not Initialized: 🔄 0
- Working: ⚡ 2
- Done: ✅ 3

| Task | Assigned Agent | Status | Created | Updated |
|------|---------------|--------|---------|---------|
| Create hello world | csharp | ✅ Done | 2026-01-26 18:00:00 | 2026-01-26 18:01:30 |
| Build React app | react | ⚡ Working | 2026-01-26 18:05:00 | 2026-01-26 18:06:15 |
```

## Benefits

1. **Simplified Management** - Single point of control for tasks and Kobolds
2. **Automatic Sync** - Task status always reflects Kobold state
3. **Traceability** - Complete mapping of tasks to Kobolds
4. **Progress Tracking** - Real-time statistics and monitoring
5. **File Persistence** - Automatic markdown report generation
6. **Error Handling** - Captures and reports execution failures
7. **Resource Cleanup** - Easy cleanup of completed Kobolds

## Best Practices

1. **Use ExecuteTaskAsync** for most cases - handles complete lifecycle
2. **Monitor periodically** - Call MonitorTasks() to keep state synchronized
3. **Cleanup regularly** - Call UnsummonCompletedKobolds() to free resources
4. **Check statistics** - Use GetStatistics() for monitoring dashboards
5. **Handle errors** - Provide messageCallback to track execution issues

## Architecture

```
┌─────────────────────────────────────────┐
│            Drake Supervisor               │
│  ┌─────────────────────────────────┐   │
│  │   Task-Kobold Mapping           │   │
│  │   (Dictionary<string, Guid>)    │   │
│  └─────────────────────────────────┘   │
│                 │                        │
│     ┌───────────┴───────────┐          │
│     │                       │          │
│  ┌──▼──────────┐    ┌──────▼─────┐   │
│  │ TaskTracker │    │KoboldFactory│   │
│  │  (Tasks)    │    │  (Workers)  │   │
│  └─────────────┘    └─────────────┘   │
│                                         │
│  Output: tasks.md                       │
└─────────────────────────────────────────┘
```

## See Also

- [Kobold-System.md](./Kobold-System.md) - Kobold worker details
- [TaskTracking.md](./TaskTracking.md) - Task management
- [WyvernAgent.md](./WyvernAgent.md) - Task orchestration
