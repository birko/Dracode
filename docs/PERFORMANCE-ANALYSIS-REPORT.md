# KoboldLair Server Performance Analysis Report

**Date:** 2026-02-05
**Scope:** Client-server communication latency investigation

---

## Executive Summary

Analysis identified **8 critical performance bottlenecks** causing laggy client-server communication. The primary culprits are:

1. **Synchronous file I/O in hot paths** - Task status updates write to disk synchronously hundreds of times per minute
2. **Blocking async calls** (`GetAwaiter().GetResult()`) - Risk of deadlocks and thread starvation
3. **WebSocket message serialization** - Reflection used in every tracked message
4. **Excessive logging** - 4+ log statements per Dragon message exchange

---

## Critical Issues

### 1. Synchronous File I/O (Highest Impact)

| File | Method | Line | Frequency |
|------|--------|------|-----------|
| `Drake.cs` | `SaveTasksToFile()` | 619 | Every task state change |
| `TaskTracker.cs` | `SaveToFile()` | 201 | Called by Drake |
| `TaskTracker.cs` | `LoadFromFile()` | 229 | Drake initialization |
| `ProjectRepository.cs` | `LoadProjects()` | 47 | Startup + updates |
| `ProjectRepository.cs` | `SaveProjects()` | 140 | Every project update |
| `ProjectConfigurationService.cs` | `SaveConfigurations()` | 485 | Every config change |
| `Wyvern.cs` | `TryLoadAnalysis()` | 110 | Constructor (blocking) |
| `Drake.cs` | Specification loading | 189 | Every Kobold summon |

**Impact:** With 4 parallel Kobolds per project, `SaveTasksToFile()` can be called 100+ times per minute, each blocking the thread.

### 2. Blocking Async Calls (Deadlock Risk)

| File | Method | Line | Issue |
|------|--------|------|-------|
| `ProjectService.cs` | `CreateProjectFolder()` | 89 | `InitializeGitRepositoryAsync().GetAwaiter().GetResult()` |
| `ProjectService.cs` | `ApproveProject()` | 582 | `CreateInitialGitCommitAsync().GetAwaiter().GetResult()` |
| `ProjectService.cs` | `GetProjectInfoList()` | 714 | `IsRepositoryAsync().GetAwaiter().GetResult()` in LINQ |
| `Drake.cs` | `SyncTaskFromKobold()` | 305 | `CommitTaskCompletionAsync().GetAwaiter().GetResult()` |
| `Wyvern.cs` | `AssignFeatures()` | 189 | Blocking git ops **in a loop** |

**Impact:** Thread starvation and potential deadlocks when called from async contexts.

### 3. WebSocket Inefficiencies

**Reflection in hot path** (`DragonService.cs:613-629`):
```csharp
foreach (var prop in data.GetType().GetProperties())  // ❌ REFLECTION
{
    var value = prop.GetValue(data);
    // ...
}
```

Called for every:
- Dragon response
- Thinking update
- Error message
- Session event

**Message history replay** (Lines 187-207):
- Serializes up to 100 messages sequentially on reconnect
- No batching or streaming

### 4. Excessive Logging

**DragonService.cs** logs 4+ statements per message exchange:
- Line 216: Every tool call
- Line 227: Every welcome message
- Line 475: Every user message
- Line 481: Every response

**Background services** log at INFO level:
- Every task start/deferral
- Full Drake stats every 60 seconds with string interpolation

### 5. Background Service Storm

All three services fire simultaneously with unbounded parallelism:

| Service | Interval | Operations |
|---------|----------|------------|
| `DrakeExecutionService` | 30s | Creates Drakes, spawns Kobolds |
| `DrakeMonitoringService` | 60s | Updates ALL task files synchronously |
| `WyvernProcessingService` | 60s | Processes all projects in parallel |

```csharp
// No parallelism limit!
await Task.WhenAll(projectTasks);  // Could spawn 50+ concurrent operations
```

### 6. Directory Enumeration in Hot Paths

`CheckForNewSpecifications()` runs after **every Dragon response**:
```csharp
var specFiles = Directory.GetDirectories(_projectsPath)  // Enumerates all
    .Select(dir => Path.Combine(dir, "specification.md"))
    .Where(File.Exists)  // File.Exists for each
    .OrderByDescending(File.GetLastWriteTime)  // GetLastWriteTime for each
```

---

## Threading Analysis

### Current Architecture

| Component | Execution Model | Issue |
|-----------|----------------|-------|
| Dragon | Per-WebSocket async handler | Good |
| Dragon Council | Sequential within Dragon | Acceptable |
| Wyrm | Background service timer | Timer callback blocks on I/O |
| Wyvern | Background service timer | Blocking git calls in loops |
| Drake | Background service timer | Synchronous file writes block callbacks |
| Kobold | Task.Run with concurrency limits | Good, but sync I/O in callbacks |

### Should Agents Be Threads?

**No.** The async/Task model is correct. The problem is:
1. Synchronous I/O inside async contexts
2. `GetAwaiter().GetResult()` converting async to sync
3. Missing debouncing on frequent writes

**Recommendation:** Keep async model, fix the blocking calls.

---

## Improvement Plan

### Phase 1: Critical I/O Fixes (Highest Priority)

#### 1.1 Async File Operations
Convert all synchronous file operations to async:

**Files to modify:**
- `DraCode.KoboldLair/Orchestrators/Drake.cs`
- `DraCode.KoboldLair/Models/Tasks/TaskTracker.cs`
- `DraCode.KoboldLair/Services/ProjectRepository.cs`
- `DraCode.KoboldLair/Services/ProjectConfigurationService.cs`
- `DraCode.KoboldLair/Orchestrators/Wyvern.cs`

**Pattern:**
```csharp
// Before
File.WriteAllText(path, content);

// After
await File.WriteAllTextAsync(path, content);
```

#### 1.2 Debounced Task File Writes
Implement write coalescing for `Drake.SaveTasksToFile()`:

```csharp
private readonly Channel<bool> _saveChannel = Channel.CreateBounded<bool>(1);
private Task? _saveTask;

private void QueueSaveTasksToFile()
{
    _saveChannel.Writer.TryWrite(true);
    _saveTask ??= Task.Run(ProcessSaveQueueAsync);
}

private async Task ProcessSaveQueueAsync()
{
    while (await _saveChannel.Reader.WaitToReadAsync())
    {
        // Drain and debounce
        while (_saveChannel.Reader.TryRead(out _)) { }
        await Task.Delay(2000); // Coalesce writes within 2s window
        await SaveTasksToFileAsync();
    }
}
```

#### 1.3 Remove GetAwaiter().GetResult()
Make calling methods async or use proper async patterns.

**Files:**
- `ProjectService.cs` lines 89, 582, 714
- `Drake.cs` line 305
- `Wyvern.cs` line 189

---

### Phase 2: WebSocket Optimization

#### 2.1 Replace Reflection with Typed Messages
Create concrete message types instead of reflection copying:

```csharp
// Before: Reflection in SendTrackedMessageAsync
foreach (var prop in data.GetType().GetProperties())

// After: Use records with JsonPolymorphism
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DragonResponseMessage), "dragon_message")]
[JsonDerivedType(typeof(ThinkingMessage), "dragon_thinking")]
public abstract record TrackedMessage(string Id, DateTime Timestamp);
```

#### 2.2 Batch Message History Replay
Send messages in chunks on reconnect:

```csharp
// Instead of 100 sequential sends, batch into groups of 10
var batches = session.MessageHistory.Chunk(10);
foreach (var batch in batches)
{
    var batchMessage = new { type = "history_batch", messages = batch };
    await SendMessageAsync(webSocket, batchMessage);
}
```

---

### Phase 3: Logging Optimization

#### 3.1 Move High-Frequency Logs to Debug
```csharp
// Before
_logger.LogInformation("[Dragon] [{Type}] {Content}", type, content);

// After
_logger.LogDebug("[Dragon] [{Type}] {Content}", type, content);
```

#### 3.2 Use LoggerMessage Source Generators
```csharp
public static partial class DragonLoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "[Dragon] [{Type}] {Content}")]
    public static partial void LogToolCall(this ILogger logger, string type, string content);
}
```

---

### Phase 4: Background Service Throttling

#### 4.1 Add Parallelism Limits
```csharp
private readonly SemaphoreSlim _throttle = new(maxConcurrency: 5);

var tasks = projects.Select(async p =>
{
    await _throttle.WaitAsync(cancellationToken);
    try { await ProcessProjectAsync(p, cancellationToken); }
    finally { _throttle.Release(); }
});
await Task.WhenAll(tasks);
```

#### 4.2 Stagger Service Intervals
Avoid all services firing at once:

| Service | Current | Proposed |
|---------|---------|----------|
| DrakeExecutionService | 30s | 30s (offset: 0s) |
| DrakeMonitoringService | 60s | 60s (offset: 20s) |
| WyvernProcessingService | 60s | 60s (offset: 40s) |

---

### Phase 5: Caching

#### 5.1 Cache Directory Enumeration
```csharp
private List<string>? _specFilesCache;
private DateTime _specFilesCacheTime;
private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(30);

private async Task<List<string>> GetSpecificationFilesAsync()
{
    if (_specFilesCache != null && DateTime.UtcNow - _specFilesCacheTime < _cacheExpiry)
        return _specFilesCache;

    _specFilesCache = await Task.Run(() => /* enumeration */);
    _specFilesCacheTime = DateTime.UtcNow;
    return _specFilesCache;
}
```

#### 5.2 Cache Specification Content
Cache specification text in Drake to avoid re-reading for every Kobold:

```csharp
private string? _cachedSpecification;

private string GetSpecificationContent()
{
    return _cachedSpecification ??= File.ReadAllText(_specificationPath);
}
```

---

## Implementation Priority

| Phase | Effort | Impact | Priority | Status |
|-------|--------|--------|----------|--------|
| 1.1 Async file ops | Medium | High | **P0** | **DONE** |
| 1.2 Debounced writes | Medium | High | **P0** | **DONE** |
| 1.3 Remove blocking calls | Low | High | **P0** | **DONE** |
| 2.1 Typed messages | Medium | Medium | P1 | **DONE** |
| 2.2 Batch history | Low | Medium | P1 | Pending |
| 3.1 Debug logging | Low | Low | P2 | Partial |
| 3.2 LoggerMessage | Medium | Low | P2 | Pending |
| 4.1 Parallelism limits | Low | Medium | P1 | **DONE** |
| 4.2 Stagger intervals | Low | Low | P2 | Pending |
| 5.1 Cache directories | Low | Medium | P1 | **DONE** |
| 5.2 Cache specs | Low | Low | P2 | Pending |

---

## Expected Results

After implementing Phase 1 (P0 items):
- **50-70% reduction** in perceived latency
- Elimination of UI freezes during task updates
- No more deadlock risk from blocking calls

After all phases:
- Smooth, responsive client experience
- Reduced CPU usage from logging and reflection
- Better resource utilization with throttled parallelism

---

---

## Implementation Log (2026-02-05)

### Phase 1 (P0) - Completed

**1. TaskTracker.cs** - Added async file operations:
- `SaveToFileAsync()` - Non-blocking file write
- `LoadFromFileAsync()` - Non-blocking file read

**2. Drake.cs** - Added debounced writes and async methods:
- Channel-based write debouncing with 2-second coalesce interval
- `ProcessSaveQueueAsync()` - Background write processor
- `SyncTaskFromKoboldAsync()` - Non-blocking task sync
- `MonitorTasksAsync()` - Non-blocking monitoring
- `HandleStuckKoboldsAsync()` - Non-blocking stuck detection
- `SaveTasksToFileAsync()` / `UpdateTasksFileAsync()` - Immediate async saves

**3. ProjectRepository.cs** - Added async CRUD operations:
- `LoadProjectsAsync()` - Non-blocking project load
- `SaveProjectsAsync()` - Non-blocking project save
- `AddAsync()`, `UpdateAsync()`, `DeleteAsync()` - Async variants

**4. ProjectConfigurationService.cs** - Added debounced saves:
- Channel-based write debouncing with 2-second coalesce interval
- `ProcessSaveQueueAsync()` - Background write processor
- `SaveConfigurationsAsync()` - Immediate async save

**5. Wyvern.cs** - Fixed blocking call:
- `AssignFeaturesAsync()` - Non-blocking feature assignment with git branches

**6. ProjectService.cs** - Added async project operations:
- `CreateProjectFolderAsync()` - Non-blocking folder creation with git init
- `ApproveProjectAsync()` - Non-blocking project approval with git commit

**7. DrakeMonitoringService.cs** - Updated to use async methods:
- Changed `drake.MonitorTasks()` → `await drake.MonitorTasksAsync()`
- Changed `drake.HandleStuckKobolds()` → `await drake.HandleStuckKoboldsAsync()`
- Changed `drake.UpdateTasksFile()` → `await drake.UpdateTasksFileAsync()`
- Reduced logging verbosity (stats logging moved to Debug level)

### Phase 2 (P1) - Completed

**8. DragonService.cs** - WebSocket message optimization:
- Replaced reflection-based property copying with `JsonSerializer.SerializeToNode`
- `SendTrackedMessageAsync()` now uses JSON DOM manipulation to inject messageId
- Eliminates reflection overhead in hot message path

**9. DrakeExecutionService.cs** - Parallelism throttling:
- Added `SemaphoreSlim _projectThrottle` with max 5 concurrent projects
- `ExecuteCycleAsync()` now uses semaphore to limit concurrent project processing

**10. DrakeMonitoringService.cs** - Parallelism throttling:
- Added `SemaphoreSlim _drakeThrottle` with max 5 concurrent Drakes
- `MonitorDrakesAsync()` now uses semaphore to limit concurrent Drake monitoring

**11. WyvernProcessingService.cs** - Parallelism throttling:
- Added `SemaphoreSlim _projectThrottle` with max 5 concurrent projects
- All three parallel operations (Wyvern assignment, analysis, reanalysis) now throttled

**12. DragonService.cs** - Directory enumeration caching:
- Added `_specFilesCache` with 30-second expiry
- `GetCachedSpecificationFiles()` method avoids filesystem calls after every message
- `InvalidateSpecFilesCache()` method for manual cache invalidation

---

## Files to Modify

### Phase 1 (P0)
1. `DraCode.KoboldLair/Orchestrators/Drake.cs`
2. `DraCode.KoboldLair/Models/Tasks/TaskTracker.cs`
3. `DraCode.KoboldLair/Services/ProjectRepository.cs`
4. `DraCode.KoboldLair/Services/ProjectConfigurationService.cs`
5. `DraCode.KoboldLair/Services/ProjectService.cs`
6. `DraCode.KoboldLair/Orchestrators/Wyvern.cs`

### Phase 2 (P1)
7. `DraCode.KoboldLair.Server/Services/DragonService.cs`

### Phase 3 (P2)
8. All services with logging (add source generators)

### Phase 4 (P1)
9. `DraCode.KoboldLair.Server/Services/DrakeExecutionService.cs`
10. `DraCode.KoboldLair.Server/Services/DrakeMonitoringService.cs`
11. `DraCode.KoboldLair.Server/Services/WyvernProcessingService.cs`
