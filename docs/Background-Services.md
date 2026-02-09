# 🔄 Background Services Overview

## Introduction

KoboldLair runs **5 background services** that continuously monitor and process work automatically. These services operate independently, running on fixed intervals to ensure projects progress from specification to completed code without manual intervention.

**Key Concept**: Once Dragon creates a specification and Sage approves it, these background services take over and run the entire workflow automatically.

---

## Service Summary

| Service | Interval | Priority | Purpose |
|---------|----------|----------|---------|
| **WyrmProcessingService** | 60s | High | Pre-analyzes specifications, creates recommendations |
| **WyvernProcessingService** | 60s | High | Analyzes specifications (guided by Wyrm), creates task files |
| **DrakeExecutionService** | 30s | High | Creates Drakes, assigns Kobolds to tasks |
| **DrakeMonitoringService** | 60s | Medium | Monitors Kobold progress, handles stuck workers |
| **WyrmService** | WebSocket | Low | WebSocket handler for real-time Wyrm task delegation |

**Note**: WyrmService is a WebSocket handler, not a periodic background service. It's included here for completeness but operates on-demand rather than on an interval.

---

## Workflow: New → WyrmAssigned → Analyzed → InProgress

```
1. Dragon creates specification (status: New)
2. WyrmProcessingService picks up New projects
   → Wyrm analyzes specification
   → Creates wyrm-recommendation.json
   → Status: WyrmAssigned
3. WyvernProcessingService picks up WyrmAssigned projects
   → Wyvern analyzes specification (guided by Wyrm recommendations)
   → Creates task files
   → Status: Analyzed
4. DrakeExecutionService picks up Analyzed projects
   → Drake creates Kobolds
   → Status: InProgress
```

---

## 1. WyrmProcessingService (NEW)

**File**: `DraCode.KoboldLair.Server/Services/WyrmProcessingService.cs`  
**Interval**: 60 seconds (configurable via constructor parameter)  
**Priority**: High - First step in the automation chain

### Purpose

Monitors for **approved project specifications** (status = "New") and runs Wyrm pre-analysis to provide recommendations for Wyvern. This provides initial guidance on languages, agent types, and technical stack before detailed task breakdown.

### What It Does

1. **Scans projects** - Checks `projects.json` for projects with status "New"
2. **Creates Wyrm agent** - Uses WyrmFactory to create a coding agent
3. **Runs pre-analysis** - Analyzes specification for languages, tech stack, complexity
4. **Saves recommendations** - Creates `wyrm-recommendation.json` with:
   - Recommended languages
   - Recommended agent types per area
   - Technical stack components
   - Suggested task areas
   - Estimated complexity
5. **Updates status** - Changes project status to "WyrmAssigned"

### Workflow

```
Every 60 seconds:
    → Check for projects with status = "New"
    → For each new project:
        ├─ Create Wyrm agent (via WyrmFactory)
        ├─ Read specification.md
        ├─ Run pre-analysis (languages, agents, stack, complexity)
        ├─ Save wyrm-recommendation.json
        └─ Update project status to "WyrmAssigned"
```

### Configuration

```csharp
public WyrmProcessingService(
    ILogger<WyrmProcessingService> logger,
    ProjectService projectService,
    WyrmFactory wyrmFactory,
    int checkIntervalSeconds = 60)  // Default: 60 seconds
```

### Concurrency Control

- **Max concurrent projects**: 5 (prevents overwhelming LLM providers)
- **Mutex protection**: Prevents overlapping runs with `_isRunning` flag
- **Parallel processing**: Projects processed in parallel (up to limit)

### Key Features

- ✅ **Provider configuration** - Uses ProviderConfigurationService for Wyrm agent
- ✅ **Parallel processing** - Processes up to 5 projects simultaneously
- ✅ **Skip protection** - Skips cycle if previous run still active
- ✅ **Graceful fallback** - Creates default recommendation if analysis fails

### Logging

```
🐲 Wyrm Processing Service started. Interval: 60s
🔍 Found 2 projects awaiting Wyrm pre-analysis
✅ Wyrm pre-analysis completed for project: my-app. Complexity: Medium, Languages: C#, TypeScript
❌ Failed to run Wyrm analysis for project: my-app
```

### Output Format (wyrm-recommendation.json)

```json
{
  "projectId": "uuid",
  "projectName": "my-app",
  "createdAt": "2026-02-09T10:00:00Z",
  "recommendedLanguages": ["csharp", "typescript"],
  "recommendedAgentTypes": {
    "backend": "csharp",
    "frontend": "react",
    "database": "documentation"
  },
  "technicalStack": ["ASP.NET Core", "React", "PostgreSQL"],
  "suggestedAreas": ["backend", "frontend", "database"],
  "complexity": "Medium",
  "analysisSummary": "Full-stack web application with API backend",
  "notes": "Consider using Entity Framework for data access"
}
```

---

## 2. WyvernProcessingService

**File**: `DraCode.KoboldLair.Server/Services/WyvernProcessingService.cs`  
**Interval**: 60 seconds (configurable via constructor parameter)  
**Priority**: High - Detailed analysis phase

### Purpose

Monitors for **pre-analyzed projects** (status = "WyrmAssigned") and assigns Wyvern orchestrators to create detailed task breakdowns. Uses Wyrm recommendations as guidance for analysis.

### What It Does

1. **Scans projects** - Checks `projects.json` for projects with status "WyrmAssigned"
2. **Loads recommendations** - Reads `wyrm-recommendation.json` (if exists)
3. **Assigns Wyvern** - Creates a Wyvern orchestrator for each project
4. **Triggers analysis** - Wyvern analyzes specification with Wyrm hints
5. **Creates tasks** - Wyvern generates task files per area
6. **Updates status** - Changes project status to "Analyzed"
7. **Persists analysis** - Saves analysis results to `analysis.json`

### Workflow

```
Every 60 seconds:
    → Check for projects with status = "WyrmAssigned"
    → For each pre-analyzed project:
        ├─ Load wyrm-recommendation.json (optional)
        ├─ Create Wyvern instance
        ├─ Wyvern analyzes specification (with Wyrm hints)
        ├─ Wyvern creates task files ({area}-tasks.md)
        ├─ Save analysis to analysis.json
        └─ Update project status to "Analyzed"
```

### Configuration

```csharp
public WyvernProcessingService(
    ILogger<WyvernProcessingService> logger,
    ProjectService projectService,
    int checkIntervalSeconds = 60)  // Default: 60 seconds
```

### Concurrency Control

- **Max concurrent projects**: 5 (prevents overwhelming LLM providers)
- **Mutex protection**: Prevents overlapping runs with `_isRunning` flag
- **Parallel processing**: Projects processed in parallel (up to limit)

### Key Features

- ✅ **Automatic recovery** - Loads existing analysis on startup
- ✅ **Wyrm guidance** - Uses Wyrm recommendations to guide analysis
- ✅ **Parallel processing** - Processes up to 5 projects simultaneously
- ✅ **Skip protection** - Skips cycle if previous run still active
- ✅ **Persistent state** - Analysis survives server restarts

### Logging

```
🐉 Wyvern Processing Service started. Interval: 60s
✨ Processing 3 projects...
📖 Loaded Wyrm recommendations for project: my-app
✅ Project 'my-app' analyzed successfully
⚠️ Previous Wyvern processing job still running, skipping this cycle
```

### Triggers

- **Wyrm analysis complete** - WyrmProcessingService transitions project to "WyrmAssigned"
- **Manual status change** - Project status manually set to "WyrmAssigned"

---

## 3. DrakeExecutionService

**File**: `DraCode.KoboldLair.Server/Services/DrakeExecutionService.cs`  
**Interval**: 30 seconds (configurable via constructor parameter)  
**Priority**: High - Core execution engine

### Purpose

Bridges the gap between **Wyvern analysis** and **task execution**. Creates Drake supervisors for task files and assigns Kobolds to execute unassigned tasks.

### What It Does

1. **Detects analyzed projects** - Finds projects with status "Active" and task files
2. **Creates Drakes** - One Drake per task file (e.g., backend-tasks.md)
3. **Plans tasks** - Invokes Kobold Planner for unassigned tasks (if enabled)
4. **Assigns Kobolds** - Summons Kobold workers to execute tasks
5. **Tracks completion** - Updates project status when all tasks complete

### Workflow

```
Every 30 seconds:
    → Check for projects with status = "Active"
    → For each project:
        ├─ Find all task files (tasks/*.md)
        ├─ For each task file:
        │   ├─ Create Drake if not exists
        │   ├─ Drake finds unassigned tasks
        │   ├─ For each task:
        │   │   ├─ Invoke Kobold Planner (if enabled)
        │   │   ├─ Create implementation plan
        │   │   ├─ Summon Kobold worker
        │   │   └─ Kobold executes plan
        │   └─ Update task statuses
        └─ Check if project complete
```

### Configuration

```csharp
public DrakeExecutionService(
    ILogger<DrakeExecutionService> logger,
    ProjectService projectService,
    DrakeFactory drakeFactory,
    int executionIntervalSeconds = 30)  // Default: 30 seconds
```

### Concurrency Control

- **Max concurrent projects**: 5 (resource throttling)
- **Per-project Kobold limits**: Configured in `projects.json` (default: 4)
- **Mutex protection**: Prevents overlapping execution cycles
- **Parallel execution**: Projects and tasks processed in parallel

### Key Features

- ✅ **Automatic Drake creation** - Creates Drakes as needed per task file
- ✅ **Planning integration** - Invokes Kobold Planner before execution
- ✅ **Resumable execution** - Picks up interrupted tasks automatically
- ✅ **Completion detection** - Updates project status to "Completed"

### Logging

```
🐲 Drake Execution Service started. Interval: 30s
🔍 Processing 2 analyzed projects...
🎯 Created Drake for 'backend-tasks.md' with 5 tasks
👹 Summoned Kobold for task 'Create User model'
✅ Project 'my-app' completed (12/12 tasks done)
```

### Triggers

- **Wyvern completes analysis** - Project status changes to "Active"
- **Task created** - New task added to task file
- **Kobold completes task** - Next unassigned task picked up
- **Task failed** - Retry logic can reassign failed tasks

---

## 3. DrakeMonitoringService

**File**: `DraCode.KoboldLair.Server/Services/DrakeMonitoringService.cs`  
**Interval**: 60 seconds (configurable via constructor parameter)  
**Priority**: Medium - Health monitoring and cleanup

### Purpose

Monitors **Drake supervisors** and their **Kobold workers** to detect stuck workers, update task statuses, and ensure healthy execution.

### What It Does

1. **Monitors all Drakes** - Iterates through all active Drake instances
2. **Checks Kobold status** - Inspects each Kobold's work progress
3. **Detects stuck workers** - Identifies Kobolds exceeding timeout threshold
4. **Handles timeouts** - Marks stuck tasks as failed, releases Kobold
5. **Updates task files** - Synchronizes task status back to markdown files

### Workflow

```
Every 60 seconds:
    → Get all Drakes from DrakeFactory
    → For each Drake:
        ├─ Get all Kobolds
        ├─ For each Kobold:
        │   ├─ Check work duration
        │   ├─ If > stuck timeout (default 30 min):
        │   │   ├─ Mark task as Failed
        │   │   ├─ Set error: "Task timeout"
        │   │   ├─ Release Kobold
        │   │   └─ Log warning
        │   └─ Update task status in file
        └─ Save changes to disk
```

### Configuration

```csharp
public DrakeMonitoringService(
    ILogger<DrakeMonitoringService> logger,
    DrakeFactory drakeFactory,
    int monitoringIntervalSeconds = 60,        // Default: 60 seconds
    int stuckKoboldTimeoutMinutes = 30)        // Default: 30 minutes
```

### Concurrency Control

- **Max concurrent Drakes**: 5 (I/O throttling)
- **Mutex protection**: Prevents overlapping monitoring runs
- **Parallel monitoring**: Drakes monitored in parallel

### Key Features

- ✅ **Stuck detection** - Identifies workers exceeding timeout
- ✅ **Automatic recovery** - Fails stuck tasks, frees resources
- ✅ **Status synchronization** - Updates markdown task files
- ✅ **Health reporting** - Logs warnings for long-running tasks

### Logging

```
🐉 Drake Monitoring Service started. Interval: 60s, Stuck timeout: 30 min
🔍 Monitoring 3 Drakes with 8 Kobolds...
⚠️ Kobold stuck on task 'Complex refactoring' (45 minutes)
❌ Marked task as Failed: timeout
⚠️ Previous monitoring job still running, skipping this cycle
```

### Stuck Kobold Criteria

A Kobold is considered "stuck" if:
- Status = "Working"
- `WorkingDuration` > `_stuckKoboldTimeout` (default 30 minutes)
- No progress indicators (implementation-dependent)

### Actions on Stuck Kobold

1. Task status → `Failed`
2. Error message → "Task execution timeout after X minutes"
3. Kobold status → `Failed`
4. Kobold released from task
5. Task file updated with failure status

---

## 4. WyrmService (WebSocket Handler)

**File**: `DraCode.KoboldLair.Server/Services/WyrmService.cs`  
**Type**: WebSocket handler (not a periodic service)  
**Endpoint**: `/wyrm` (if configured)

### Purpose

Provides **WebSocket endpoint** for Wyrm-related operations. Unlike other services, this is event-driven rather than periodic.

### What It Does

- **Task tracking** - Maintains `TaskTracker` for Wyrm operations
- **WebSocket handling** - Processes incoming Wyrm requests
- **Command routing** - Routes commands to appropriate handlers
- **Real-time updates** - Sends status updates via WebSocket

### Note

This is **not a background service** in the traditional sense. It's a WebSocket handler that responds to client requests rather than running on a fixed interval. Included here for completeness as part of the KoboldLair service architecture.

---

## Service Interactions

### Sequential Flow

```
1. WyvernProcessingService (60s)
   ↓ Creates task files
2. DrakeExecutionService (30s)
   ↓ Creates Drakes, assigns Kobolds
3. Kobolds execute tasks
   ↓ Generate code
4. DrakeMonitoringService (60s)
   ↓ Monitors progress, handles failures
```

### Parallel Processing

All services run **independently** and **in parallel**:
- WyvernProcessingService checks for new specs
- DrakeExecutionService assigns tasks
- DrakeMonitoringService monitors health
- All services respect their own intervals

### Coordination Points

1. **Project status** - Services coordinate via project status field
   - `New` → WyvernProcessingService picks up
   - `Active` → DrakeExecutionService picks up
   - `Completed` → All services skip

2. **Task status** - Services coordinate via task status in markdown files
   - `Unassigned` → DrakeExecutionService assigns Kobold
   - `Working` → DrakeMonitoringService monitors
   - `Done`/`Failed` → All services skip

3. **Drake registry** - DrakeFactory provides thread-safe registry
   - DrakeExecutionService creates Drakes
   - DrakeMonitoringService queries Drakes

---

## Configuration Summary

### appsettings.json

```json
{
  "KoboldLair": {
    "BackgroundServices": {
      "WyvernProcessingInterval": 60,      // seconds
      "DrakeExecutionInterval": 30,        // seconds
      "DrakeMonitoringInterval": 60,       // seconds
      "StuckKoboldTimeout": 30             // minutes
    },
    "Concurrency": {
      "MaxParallelProjects": 5,
      "MaxParallelDrakes": 5,
      "MaxParallelKobolds": 4              // per project
    }
  }
}
```

### Per-Project Overrides

In `projects.json`:
```json
{
  "agents": {
    "kobold": {
      "maxParallel": 4,
      "timeout": 1800
    }
  }
}
```

---

## Performance Considerations

### Intervals

**Why 30s for DrakeExecutionService?**
- Faster response to new tasks
- Core execution engine needs more frequent checks
- Balances responsiveness with resource usage

**Why 60s for WyvernProcessingService and DrakeMonitoringService?**
- Less frequent operations (new specs are rare)
- Monitoring is less time-critical
- Reduces CPU and LLM API usage

### Concurrency Limits

**Max 5 concurrent projects**:
- Prevents overwhelming LLM providers with parallel requests
- Balances throughput with API rate limits
- Adjustable based on available resources

**Max 4 Kobolds per project**:
- Default conservative limit
- Prevents too many parallel code generation tasks
- Configurable per project based on complexity

### Skip Protection

All services use **mutex locks** (`_isRunning` flag) to prevent overlapping runs:
- If previous cycle still running, skip current cycle
- Logs warning for visibility
- Prevents resource exhaustion

---

## Monitoring and Observability

### Log Patterns

**Service Start:**
```
🐉 Wyvern Processing Service started. Interval: 60s
🐲 Drake Execution Service started. Interval: 30s
🐉 Drake Monitoring Service started. Interval: 60s
```

**Normal Operation:**
```
✨ Processing 2 projects...
🎯 Created Drake for 'backend-tasks.md'
👹 Summoned Kobold for task 'Create API endpoint'
✅ Project 'my-app' completed
```

**Warnings:**
```
⚠️ Previous job still running, skipping this cycle
⚠️ Kobold stuck on task (35 minutes)
```

**Errors:**
```
❌ Error in processing cycle: [exception details]
❌ Failed to create Drake: [error message]
```

### Health Checks

Monitor service health by checking:
1. **Log frequency** - Services should log at their intervals
2. **Cycle completion** - Look for "skipping" warnings (indicates overload)
3. **Error rates** - Frequent errors indicate configuration issues
4. **Stuck Kobolds** - Regular timeout warnings indicate resource constraints

---

## Troubleshooting

### Service Not Running

**Symptom**: No log messages from service

**Solutions:**
1. Check service is registered in `Program.cs`
2. Verify service started (check startup logs)
3. Check for exceptions during initialization

### Cycles Being Skipped

**Symptom**: "Previous job still running, skipping this cycle"

**Solutions:**
1. **Increase interval** - Give service more time to complete
2. **Reduce concurrency** - Lower max parallel projects/Drakes
3. **Increase resources** - More CPU/memory for server
4. **Optimize tasks** - Break down large tasks into smaller ones

### Kobolds Getting Stuck

**Symptom**: Regular timeout warnings

**Solutions:**
1. **Increase timeout** - Raise `stuckKoboldTimeout` (default 30 min)
2. **Check LLM provider** - Verify API is responsive
3. **Review task complexity** - Large tasks may need more time
4. **Check network** - Ensure stable connection to LLM APIs

### Projects Not Processing

**Symptom**: Projects stay in "New" status

**Solutions:**
1. **Check WyvernProcessingService** - Verify service is running
2. **Check project status** - Must be exactly "New" (case-sensitive)
3. **Check logs** - Look for processing errors
4. **Manual trigger** - Restart service to force immediate check

---

## Related Documentation

- [Wyvern Project Analyzer](Wyvern-Project-Analyzer.md) - Details on Wyvern analysis
- [Drake Monitoring System](Drake-Monitoring-System.md) - Drake and Kobold management
- [Kobold System](Kobold-System.md) - Kobold worker details
- [Kobold Planner](Kobold-Planner-Agent.md) - Implementation planning
- [Dragon Requirements Agent](Dragon-Requirements-Agent.md) - Starting point for workflows
