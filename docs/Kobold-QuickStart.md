# Kobold Worker System - Quick Start

## What is a Kobold?

A **Kobold** is a worker agent that handles one specific task at a time. Think of it as a dedicated AI assistant that:
- Works on exactly one task
- Tracks its own status (Unassigned → Assigned → Working → Done)
- Can be monitored and managed through a central factory
- Can be reused after completing a task

## Quick Example

```csharp
using DraCode.KoboldTown.Factories;
using DraCode.KoboldTown.Models;

// 1. Create factory
var factory = new KoboldFactory();

// 2. Create a C# specialist Kobold with OpenAI
var kobold = factory.CreateKobold("openai", "csharp");

// 3. Assign to a task
var taskId = Guid.NewGuid();
kobold.AssignTask(taskId);

// 4. Start working
kobold.StartWorking();

// 5. Execute the task
var result = await kobold.Agent.RunAsync("Refactor this method...");

// 6. Mark as complete
kobold.MarkDone();

// 7. Check statistics
var stats = factory.GetStatistics();
Console.WriteLine(stats); // Total: 1, Done: 1
```

## Available Agent Types

- `"csharp"` - C# specialist
- `"cpp"` - C++ specialist  
- `"assembler"` - Assembly specialist
- `"javascript"` / `"typescript"` - JS/TS specialist
- `"css"` - CSS specialist
- `"html"` - HTML specialist
- `"react"` - React specialist
- `"angular"` - Angular specialist
- `"diagramming"` - Diagram creation specialist
- `"Wyvern"` - Task orchestration specialist

## Kobold Status Lifecycle

```
┌─────────────┐
│ Unassigned  │ ← Created
└──────┬──────┘
       │ AssignTask()
       ▼
┌─────────────┐
│  Assigned   │ ← Task assigned
└──────┬──────┘
       │ StartWorking()
       ▼
┌─────────────┐
│   Working   │ ← Processing task
└──────┬──────┘
       │ MarkDone()
       ▼
┌─────────────┐
│    Done     │ ← Completed
└─────────────┘
```

## KoboldFactory Methods

### Create Kobolds
```csharp
var kobold = factory.CreateKobold("openai", "csharp");
var kobold = factory.CreateKobold("claude", "react", options, config);
```

### Find Kobolds
```csharp
// By ID
var kobold = factory.GetKobold(koboldId);

// By task
var kobold = factory.GetKoboldByTaskId(taskId);

// By status
var working = factory.GetWorkingKobolds();
var available = factory.GetUnassignedKobolds();

// By type
var csharpKobolds = factory.GetKoboldsByType("csharp");
```

### Manage Kobolds
```csharp
// Get statistics
var stats = factory.GetStatistics();

// Cleanup completed Kobolds
int removed = factory.CleanupDoneKobolds();

// Remove specific Kobold
factory.RemoveKobold(koboldId);

// Clear all
factory.Clear();
```

## Common Patterns

### Pattern 1: One-Time Task
```csharp
var factory = new KoboldFactory();
var kobold = factory.CreateKobold("openai", "csharp");

kobold.AssignTask(taskId);
kobold.StartWorking();
var result = await kobold.Agent.RunAsync(task);
kobold.MarkDone();
```

### Pattern 2: Worker Pool
```csharp
var factory = new KoboldFactory();

// Pre-create pool
for (int i = 0; i < 5; i++)
    factory.CreateKobold("openai", "csharp");

// Assign work
var kobold = factory.GetUnassignedKobolds().FirstOrDefault();
if (kobold != null)
{
    kobold.AssignTask(taskId);
    // ... do work
}
```

### Pattern 3: Reusable Kobold
```csharp
var kobold = factory.CreateKobold("openai", "react");

// First task
kobold.AssignTask(task1);
kobold.StartWorking();
await kobold.Agent.RunAsync(...);
kobold.MarkDone();

// Reset for reuse
kobold.Reset();

// Second task
kobold.AssignTask(task2);
// ... repeat
```

## Files

```
DraCode.KoboldTown/
├── Models/
│   ├── Kobold.cs           # Worker agent wrapper
│   └── KoboldStatus.cs     # Status enum
├── Factories/
│   └── KoboldFactory.cs    # Factory & management
└── Examples/
    └── KoboldExample.cs    # Usage examples
```

## Key Features

✅ **Single Task Focus** - Each Kobold handles one task at a time  
✅ **Full Lifecycle Tracking** - Timestamps for every status change  
✅ **Centralized Management** - Factory tracks all instances  
✅ **Thread-Safe** - ConcurrentDictionary for concurrent operations  
✅ **Type-Safe** - Strongly typed agent assignments  
✅ **Statistics** - Real-time monitoring of worker status  
✅ **Reusable** - Reset and reuse Kobolds after completion  

## Documentation

- **[Kobold-System.md](./Kobold-System.md)** - Complete documentation with examples
- **[KoboldTown-Summary.md](./KoboldTown-Summary.md)** - Overall KoboldTown architecture
- **[WyvernAgent.md](./WyvernAgent.md)** - Task orchestration details

## Next Steps

1. Review [Kobold-System.md](./Kobold-System.md) for detailed documentation
2. Check [KoboldExample.cs](../DraCode.KoboldTown/Examples/KoboldExample.cs) for working examples
3. Integrate with your WyvernService for task management

---

**Note:** Kobolds require LLM provider configuration (API keys, models) through KoboldFactory constructor.
