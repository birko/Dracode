# Kobold State Management Architecture

## Overview

The KoboldLair system follows a clear separation of concerns for state management:
- **Kobolds** manage their own internal state based on task execution
- **Drake** observes Kobold states and syncs them to TaskTracker
- **Drake** can request actions (start working) but cannot force state changes

This design ensures that state transitions are driven by actual work progress, not external commands.

## State Management Principles

### 1. Kobolds Control Their Own State

Kobolds autonomously transition through states based on their work:

```
Unassigned → Assigned → Working → Done
```

**State Transitions:**
- `Unassigned` → `Assigned`: When `AssignTask()` is called
- `Assigned` → `Working`: When `StartWorkingAsync()` begins execution
- `Working` → `Done`: When `StartWorkingAsync()` completes (success or error)

**Key Point:** Only the Kobold's own methods trigger state changes. No external entity can forcefully change state.

### 2. Drake Observes and Syncs

Drake's role is to:
- ✅ Summon (create) Kobolds
- ✅ Assign tasks to Kobolds
- ✅ Request Kobolds to start working
- ✅ Query Kobold status
- ✅ Sync TaskTracker from Kobold states
- ❌ **NEVER** forcefully change Kobold state

**Drake's Sync Method:**
```csharp
private void SyncTaskFromKobold(Kobold kobold)
{
    // Maps Kobold state to Task state
    var taskStatus = kobold.Status switch
    {
        KoboldStatus.Unassigned => TaskStatus.Unassigned,
        KoboldStatus.Assigned => TaskStatus.NotInitialized,
        KoboldStatus.Working => TaskStatus.Working,
        KoboldStatus.Done => TaskStatus.Done,
        _ => task.Status
    };
    
    _taskTracker.UpdateTask(task, taskStatus);
}
```

### 3. Automatic State Management in Kobold

The `StartWorkingAsync()` method manages the complete lifecycle:

```csharp
public async Task<List<Message>> StartWorkingAsync(int maxIterations = 30)
{
    // Validate: must be Assigned
    if (Status != KoboldStatus.Assigned)
        throw new InvalidOperationException($"Cannot start - not assigned (current: {Status})");
    
    // Transition to Working
    Status = KoboldStatus.Working;
    StartedAt = DateTime.UtcNow;
    
    try
    {
        // Execute task through agent
        var messages = await Agent.RunAsync(TaskDescription, maxIterations);
        
        // SUCCESS: Transition to Done
        Status = KoboldStatus.Done;
        CompletedAt = DateTime.UtcNow;
        
        return messages;
    }
    catch (Exception ex)
    {
        // FAILURE: Capture error and transition to Done
        ErrorMessage = ex.Message;
        Status = KoboldStatus.Done;
        CompletedAt = DateTime.UtcNow;
        throw;
    }
}
```

**Key Benefits:**
- State always reflects actual execution status
- No inconsistency between state and reality
- Error states are captured automatically
- No need for external "completion" calls

## API Changes

### Removed Methods (Old API)

These methods allowed external state manipulation and have been removed:

```csharp
// ❌ REMOVED: Drake forcefully changed Kobold state
public void CompleteKoboldWork(Guid koboldId, string? errorMessage = null)

// ❌ REMOVED: External state manipulation
public void MarkDone()

// ❌ REMOVED: External error setting
public void SetError(string errorMessage)
```

### New Properties (New API)

Kobolds now expose read-only state queries:

```csharp
// ✅ Check if Kobold finished (success or error)
public bool IsComplete => Status == KoboldStatus.Done;

// ✅ Check if Kobold finished successfully (no error)
public bool IsSuccess => Status == KoboldStatus.Done && string.IsNullOrEmpty(ErrorMessage);

// ✅ Check if Kobold has error
public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
```

### Updated Drake Methods

Drake methods now observe state instead of controlling it:

```csharp
// Request Kobold to start working
// Kobold manages its own state transitions
public async Task<List<Message>> StartKoboldWorkAsync(Guid koboldId, int maxIterations = 30)
{
    var messages = await kobold.StartWorkingAsync(maxIterations);
    
    // Sync task status from Kobold's final state
    SyncTaskFromKobold(kobold);
    
    return messages;
}

// Monitor all tasks and sync from Kobold states
public void MonitorTasks()
{
    foreach (var task in allTasks)
    {
        if (_taskToKoboldMap.TryGetValue(task.Id, out var koboldId))
        {
            var kobold = _koboldFactory.GetKobold(koboldId);
            if (kobold != null)
            {
                // Drake observes and syncs, doesn't control
                SyncTaskFromKobold(kobold);
            }
        }
    }
}
```

## Usage Examples

### Before (Old API - Wrong)

```csharp
// ❌ BAD: Drake forcefully controlled state
var kobold = drake.SummonKobold(task, "csharp");
await drake.StartKoboldWorkAsync(kobold.Id);
drake.CompleteKoboldWork(kobold.Id); // External state change
```

### After (New API - Correct)

```csharp
// ✅ GOOD: Kobold manages its own state
var kobold = drake.SummonKobold(task, "csharp");
await drake.StartKoboldWorkAsync(kobold.Id); // Kobold auto-transitions to Done

// Drake just observes
Console.WriteLine($"Is complete: {kobold.IsComplete}");
Console.WriteLine($"Is success: {kobold.IsSuccess}");
Console.WriteLine($"Has error: {kobold.HasError}");
```

### High-Level Execution

```csharp
// ExecuteTaskAsync handles the full lifecycle
var (messages, kobold) = await drake.ExecuteTaskAsync(
    task,
    "csharp",
    maxIterations: 10
);

// Kobold automatically transitioned to Done
Console.WriteLine($"Status: {kobold.Status}"); // Done
Console.WriteLine($"Success: {kobold.IsSuccess}"); // true/false
```

### Manual Step-by-Step

```csharp
// 1. Create Kobold
var kobold = drake.SummonKobold(task, "react");
Console.WriteLine($"Status: {kobold.Status}"); // Assigned

// 2. Start work (automatic state management)
try
{
    var messages = await drake.StartKoboldWorkAsync(kobold.Id);
    
    // Kobold automatically transitioned to Done on success
    Console.WriteLine($"Status: {kobold.Status}"); // Done
    Console.WriteLine($"Success: {kobold.IsSuccess}"); // true
}
catch (Exception ex)
{
    // Kobold automatically captured error and transitioned to Done
    Console.WriteLine($"Status: {kobold.Status}"); // Done
    Console.WriteLine($"Error: {kobold.ErrorMessage}"); // ex.Message
    Console.WriteLine($"Has error: {kobold.HasError}"); // true
}
```

### Monitoring Loop

```csharp
while (true)
{
    await Task.Delay(60000); // Every minute
    
    // Drake syncs from Kobold states
    drake.MonitorTasks();
    
    // Query current state
    var stats = drake.GetStatistics();
    Console.WriteLine($"Working: {stats.WorkingKobolds}, Done: {stats.DoneKobolds}");
    
    // Cleanup completed Kobolds
    drake.UnsummonCompletedKobolds();
}
```

## Benefits

### 1. State Integrity
- State always reflects actual execution status
- No manual state management = no human errors
- Impossible to have Working status with Done Kobold

### 2. Error Handling
- Errors automatically captured during execution
- Error state preserved even if exception thrown
- Drake can query `kobold.HasError` and `kobold.ErrorMessage`

### 3. Simplified API
- No need to remember to call `CompleteKoboldWork()`
- No need to manually set error messages
- Just call `StartWorkingAsync()` and state manages itself

### 4. Clear Separation of Concerns
- Kobold = worker that manages its own lifecycle
- Drake = supervisor that observes and coordinates
- TaskTracker = persistence layer for task status

### 5. Reliable Monitoring
- Drake's `MonitorTasks()` syncs from source of truth (Kobold state)
- No risk of stale or incorrect task status
- Automatic sync ensures consistency

## Migration Guide

If you have code using the old API:

### 1. Remove `CompleteKoboldWork()` calls
```csharp
// Before
await drake.StartKoboldWorkAsync(koboldId);
drake.CompleteKoboldWork(koboldId); // ❌ Remove this

// After
await drake.StartKoboldWorkAsync(koboldId); // ✅ Done auto-synced
```

### 2. Remove `MarkDone()` calls
```csharp
// Before
await kobold.StartWorkingAsync();
kobold.MarkDone(); // ❌ Remove this

// After
await kobold.StartWorkingAsync(); // ✅ Auto-transitions to Done
```

### 3. Use new status properties
```csharp
// Before
if (kobold.Status == KoboldStatus.Done) // Still works

// After (more expressive)
if (kobold.IsComplete) // Clearer intent
if (kobold.IsSuccess) // Check success
if (kobold.HasError)  // Check error
```

### 4. Remove manual error setting
```csharp
// Before
try {
    await kobold.StartWorkingAsync();
} catch (Exception ex) {
    kobold.SetError(ex.Message); // ❌ Remove this
    kobold.MarkDone(); // ❌ Remove this
}

// After
try {
    await kobold.StartWorkingAsync(); // ✅ Auto-captures error
} catch (Exception ex) {
    // Error already captured in kobold.ErrorMessage
    Console.WriteLine($"Error: {kobold.ErrorMessage}");
}
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│                    Drake                        │
│  🐉 Supervisor - Observes & Coordinates         │
│                                                 │
│  • SummonKobold() - Create                     │
│  • StartKoboldWorkAsync() - Request action     │
│  • MonitorTasks() - Observe & sync             │
│  • SyncTaskFromKobold() - Update TaskTracker   │
└──────────────────┬──────────────────────────────┘
                   │
                   │ Observes state
                   │ (never controls)
                   ▼
┌─────────────────────────────────────────────────┐
│                  Kobold                         │
│  👹 Worker - Manages Own State                  │
│                                                 │
│  States: Unassigned → Assigned → Working → Done│
│                                                 │
│  • AssignTask() → Assigned                     │
│  • StartWorkingAsync() → Working → Done        │
│  • Automatic error capture                     │
│  • Read-only: IsComplete, IsSuccess, HasError  │
└─────────────────────────────────────────────────┘
```

## Summary

**Core Principle:** Kobolds are autonomous workers that manage their own lifecycle. Drake is a supervisor that observes, coordinates, and syncs - but never forcefully controls.

**Key Changes:**
- ✅ Kobold's `StartWorkingAsync()` now auto-transitions to Done
- ✅ Errors automatically captured in Kobold state
- ✅ Drake's `SyncTaskFromKobold()` observes and syncs
- ❌ Removed `CompleteKoboldWork()`, `MarkDone()`, `SetError()`
- ✅ Added `IsComplete`, `IsSuccess`, `HasError` properties

**Result:** More reliable, less error-prone, clearer separation of concerns.
