# Shared Planning Context Service

## Overview

The `SharedPlanningContextService` provides comprehensive context sharing and coordination across the KoboldLair multi-agent system. It enables Kobolds, Drake supervisors, and projects to share planning insights, coordinate work, and learn from past executions.

## Features

### 1. **Multi-Agent Coordination**
- Tracks active agents per project in real-time
- Detects file conflicts (which files are currently being modified)
- Finds related plans working on similar code
- Prevents concurrent modifications to the same files

### 2. **Drake Supervisor Support**
- Agent registration/unregistration lifecycle management
- Activity heartbeat monitoring for stuck agent detection
- Project-wide statistics and metrics
- Real-time agent status tracking

### 3. **Cross-Project Learning**
- Records task completion metrics (duration, steps, iterations, files touched)
- Analyzes patterns and best practices per agent type
- Provides insights from similar tasks in other projects
- Builds knowledge base of successful execution strategies

### 4. **Thread-Safe Design**
- Concurrent dictionaries for in-memory caching
- File locking for persistence operations
- Max 50 projects cached with LRU eviction
- Safe for high-concurrency scenarios

### 5. **Persistence**
- Stores context as `planning-context.json` in each project's output folder
- Auto-saves on agent completion
- Batch persist all contexts on application shutdown
- Automatic recovery on restart

## Architecture

### Data Models

#### ProjectPlanningContext
```csharp
public class ProjectPlanningContext
{
    public string ProjectId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public int ActiveAgentCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public int FailedTasksCount { get; set; }
    public ConcurrentDictionary<string, string> ActiveAgents { get; set; }
    public List<PlanningInsight> Insights { get; set; }
}
```

#### AgentPlanningContext
```csharp
public class AgentPlanningContext
{
    public string AgentId { get; set; }
    public string ProjectId { get; set; }
    public string TaskId { get; set; }
    public string AgentType { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

#### PlanningInsight
```csharp
public class PlanningInsight
{
    public string InsightId { get; set; }
    public string ProjectId { get; set; }
    public string TaskId { get; set; }
    public string AgentType { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public double DurationSeconds { get; set; }
    public int StepCount { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalIterations { get; set; }
    public int FilesModified { get; set; }
    public int FilesCreated { get; set; }
    public string? ErrorMessage { get; set; }
}
```

#### PlanningStatistics
```csharp
public class PlanningStatistics
{
    public string ProjectId { get; set; }
    public int TotalTasksCompleted { get; set; }
    public int TotalTasksFailed { get; set; }
    public int CurrentlyActive { get; set; }
    public double AverageDurationSeconds { get; set; }
    public double AverageStepsPerTask { get; set; }
    public double AverageIterationsPerTask { get; set; }
    public double SuccessRate { get; set; }
    public string MostActiveAgentType { get; set; }
}
```

## Integration Points

### 1. Dependency Injection (Program.cs)
```csharp
// Register shared planning context service
builder.Services.AddSingleton<SharedPlanningContextService>(sp =>
{
    var planService = sp.GetRequiredService<KoboldPlanService>();
    var projectRepository = sp.GetRequiredService<ProjectRepository>();
    var logger = sp.GetRequiredService<ILogger<SharedPlanningContextService>>();
    var config = sp.GetRequiredService<IOptions<KoboldLairConfiguration>>().Value;
    return new SharedPlanningContextService(
        config.ProjectsPath ?? "./projects", 
        planService, 
        projectRepository, 
        logger);
});

// Shutdown hook for persistence
appLifetime.ApplicationStopping.Register(() =>
{
    var sharedPlanningContext = scope.ServiceProvider.GetRequiredService<SharedPlanningContextService>();
    sharedPlanningContext.PersistAllContextsAsync().GetAwaiter().GetResult();
});
```

### 2. DrakeFactory Integration
```csharp
public DrakeFactory(
    // ... other parameters
    SharedPlanningContextService? sharedPlanningContext = null)
{
    _sharedPlanningContext = sharedPlanningContext;
}

// Pass to Drake constructor
var drake = new Drake(
    // ... other parameters
    sharedPlanningContext: _sharedPlanningContext);
```

### 3. Drake Integration
```csharp
// Register agent when summoned
var kobold = _koboldFactory.CreateKobold(...);
if (_sharedPlanningContext != null)
{
    await _sharedPlanningContext.RegisterAgentAsync(
        kobold.Id.ToString(), 
        projectId, 
        task.Id.ToString(), 
        agentType);
}

// Unregister when complete or failed
if (_sharedPlanningContext != null)
{
    await _sharedPlanningContext.UnregisterAgentAsync(
        kobold.Id.ToString(), 
        success: taskStatus == TaskStatus.Done, 
        kobold.ErrorMessage);
}
```

## Usage Examples

### Drake: Register a Kobold
```csharp
await sharedContext.RegisterAgentAsync(
    koboldId, 
    projectId, 
    taskId, 
    "csharp");
```

### Kobold: Check File Conflicts
```csharp
var isInUse = await sharedContext.IsFileInUseAsync(
    projectId, 
    "src/Program.cs");

if (isInUse)
{
    // Wait or work on different file
}
```

### Kobold Planner: Get Similar Task Insights
```csharp
var insights = await sharedContext.GetSimilarTaskInsightsAsync(
    projectId, 
    "react", 
    maxResults: 10);

// Use insights to inform planning:
// - Average step count for similar tasks
// - Common iteration patterns
// - Typical file organization
```

### Drake: Get Project Statistics
```csharp
var stats = await sharedContext.GetProjectStatisticsAsync(projectId);

Console.WriteLine($"Success Rate: {stats.SuccessRate:F1}%");
Console.WriteLine($"Avg Duration: {stats.AverageDurationSeconds:F0}s");
Console.WriteLine($"Avg Steps: {stats.AverageStepsPerTask:F1}");
Console.WriteLine($"Active Agents: {stats.CurrentlyActive}");
```

### Cross-Project Learning
```csharp
// Get insights from other projects
var crossProjectInsights = await sharedContext.GetCrossProjectInsightsAsync(
    currentProjectId, 
    "python", 
    maxResults: 5);

// Get best practices learned across all projects
var bestPractices = await sharedContext.GetBestPracticesAsync("typescript");
// Returns:
// {
//   "typical_steps": "12.5 steps on average",
//   "typical_iterations": "3.2 iterations per task",
//   "typical_duration": "450 seconds average",
//   "success_rate": "87.5%"
// }
```

### Get Related Plans (for context)
```csharp
var relatedPlans = await sharedContext.GetRelatedPlansAsync(
    projectId,
    currentTaskId,
    relatedFiles: new[] { "src/api/users.ts", "src/models/User.ts" });

// Returns plans that touched similar files, ordered by relevance
foreach (var plan in relatedPlans)
{
    // Use plan context to inform current work
}
```

### Get Files Currently In Use
```csharp
var filesInUse = await sharedContext.GetFilesInUseAsync(projectId);

// Avoid these files or coordinate access
foreach (var file in filesInUse)
{
    Console.WriteLine($"⚠️ {file} is being modified");
}
```

## File Structure

### Per-Project Storage
```
{ProjectsPath}/
    {project-name}/
        workspace/                        # Kobold output
        kobold-plans/                     # Implementation plans
            frontend-1-create-user-a7f3-plan.json
            frontend-1-create-user-a7f3-plan.md
            plan-index.json
        planning-context.json             # ← Shared planning context
```

### planning-context.json Format
```json
{
  "projectId": "abc-123",
  "createdAt": "2026-02-09T11:00:00Z",
  "lastAccessedAt": "2026-02-09T11:39:00Z",
  "activeAgentCount": 2,
  "completedTasksCount": 15,
  "failedTasksCount": 2,
  "activeAgents": {
    "agent-id-1": "task-id-1",
    "agent-id-2": "task-id-2"
  },
  "insights": [
    {
      "insightId": "insight-1",
      "projectId": "abc-123",
      "taskId": "task-xyz",
      "agentType": "csharp",
      "timestamp": "2026-02-09T10:30:00Z",
      "success": true,
      "durationSeconds": 450.5,
      "stepCount": 8,
      "completedSteps": 8,
      "totalIterations": 12,
      "filesModified": 3,
      "filesCreated": 2,
      "errorMessage": null
    }
  ]
}
```

## Configuration

### Limits
- **Max Cached Projects**: 50 (LRU eviction)
- **Max Insights Per Project**: 100 (oldest removed first)
- **Persistence**: Auto on agent completion + shutdown

### Cache Management
- LRU eviction when cache exceeds 50 projects
- Contexts persist to disk before eviction
- Automatic reload on next access

## Benefits

### For Drake Supervisors
- Real-time visibility into active Kobolds
- Project-wide resource coordination
- Aggregate metrics for monitoring
- Historical success/failure patterns

### For Kobold Planners
- Learn from similar tasks in history
- Estimate reasonable step counts and durations
- Understand typical iteration patterns
- Adapt plans based on past successes

### For Kobold Workers
- Avoid file conflicts with other agents
- Access related plan context
- Benefit from cross-project learnings
- Coordinate implicitly through shared state

### For System Monitoring
- Track agent activity across projects
- Identify patterns in successful vs failed tasks
- Monitor resource utilization
- Detect stuck or slow agents

## Performance Considerations

### In-Memory Caching
- Fast O(1) lookups for project contexts
- Concurrent dictionary for thread-safety
- Minimal locking (only for file I/O)

### Persistence Strategy
- Debounced writes (not on every change)
- Batch save on shutdown
- Recovery from disk on restart
- WAL-style reliability for critical updates

### Scalability
- Designed for 50+ concurrent projects
- 100+ active agents per project
- Thousands of historical insights
- Minimal performance impact on agent execution

## Future Enhancements

1. **Pattern Recognition**: ML-based prediction of task complexity
2. **Automatic Optimization**: Suggest better agent types based on history
3. **Conflict Resolution**: Proactive file access scheduling
4. **Telemetry Dashboard**: Real-time visualization of shared context
5. **Cross-Instance Sync**: Redis-backed distributed context sharing

## Related Documentation

- [KoboldPlanService.cs](../DraCode.KoboldLair/Services/KoboldPlanService.cs)
- [Drake Monitoring System](Drake-Monitoring-System.md)
- [Kobold System](Kobold-System.md)
- [Implementation Plans](architecture/implementation-plans.md)
