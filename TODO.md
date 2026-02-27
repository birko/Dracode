# TODO - Planned Enhancements

This file tracks planned enhancements and their implementation status.
**Last updated: 2026-02-26 - Database Migration Plan added**

---

## 🟢 COMPLETED - Wyrm Pre-Analysis Workflow (Option B)

All Wyrm workflow changes have been implemented and tested as of 2026-02-09.

### Implementation Details

- [x] **Project Status Changes** *(Completed 2026-02-09)*
  - Added `WyrmAssigned` status for post-Wyrm, pre-Wyvern state
  - Deprecated `WyvernAssigned` status (backward compatible)
  - Workflow: New → WyrmAssigned → Analyzed → InProgress

- [x] **WyrmRecommendation Model** *(Completed 2026-02-09)*
  - Created `Models/Agents/WyrmRecommendation.cs`
  - Properties: ProjectId, ProjectName, RecommendedLanguages, RecommendedAgentTypes, TechnicalStack, SuggestedAreas, Complexity, AnalysisSummary, Notes
  - Saved to `wyrm-recommendation.json` in project workspace

- [x] **WyrmFactory Updates** *(Completed 2026-02-09)*
  - Added ProviderConfigurationService dependency
  - Implemented `CreateWyrm(Project project)` method
  - Uses `KoboldLairAgentFactory` for consistency
  - Creates coding agent for specification pre-analysis

- [x] **WyrmProcessingService** *(Completed 2026-02-09)*
  - New background service (60s interval)
  - Monitors `New` projects
  - Runs Wyrm pre-analysis on specifications
  - Generates recommendations JSON
  - Transitions projects to `WyrmAssigned`

- [x] **WyvernProcessingService Updates** *(Completed 2026-02-09)*
  - Changed to process `WyrmAssigned` instead of `New` projects
  - Loads `wyrm-recommendation.json` (if exists)
  - Passes recommendations to Wyvern as guidance

- [x] **Wyvern Orchestrator Updates** *(Completed 2026-02-09)*
  - `AnalyzeProjectAsync` accepts optional `WyrmRecommendation` parameter
  - Includes recommendations in analysis prompt as "hints"
  - Thread-safe with `_analysisLock`

- [x] **ProjectService Updates** *(Completed 2026-02-09)*
  - Loads Wyrm recommendations from JSON
  - Passes to Wyvern during analysis
  - Uses `WyrmAssigned` status instead of deprecated `WyvernAssigned`

- [x] **DI Registration** *(Completed 2026-02-09)*
  - Updated `WyrmFactory` registration with ProviderConfigurationService
  - Registered `WyrmProcessingService` as hosted service
  - Both services run on 60-second intervals

- [x] **Documentation Updates** *(Completed 2026-02-09)*
  - Updated `CLAUDE.md` with new workflow diagram
  - Updated `Background-Services.md` with WyrmProcessingService details
  - Updated workflow documentation with Wyrm pre-analysis phase

**Impact**: Provides initial analysis guidance before Wyvern's detailed task breakdown, improving task delegation accuracy.

---

## 🟢 COMPLETED - Agent Creation Pattern Audit

All agent creation patterns audited and fixed as of 2026-02-09.

### Audit Results

- [x] **Factory Pattern Enforcement** *(Completed 2026-02-09)*
  - Audited all agent creation in DraCode.KoboldLair
  - Fixed WyrmFactory to use `KoboldLairAgentFactory` instead of `DraCode.Agent.Agents.AgentFactory`
  - Verified 4 factories use consistent pattern:
    - DrakeFactory ✅
    - WyvernFactory ✅
    - KoboldFactory ✅
    - WyrmFactory ✅ (fixed)

- [x] **Acceptable Exceptions Documented** *(Completed 2026-02-09)*
  - Dragon Council sub-agents (Sage, Seeker, Sentinel, Warden) use `new`
  - Justified: Custom constructors with callbacks and dependencies
  - Not standard agents - internal council members

**Impact**: Ensures consistent provider configuration and agent instantiation across all KoboldLair components.

---

## 🟢 COMPLETED - Data Integrity & Context Loss Fixes

All critical blocking operations and race conditions have been resolved as of 2026-02-09.

### Critical Priority (P0) - File Write Race Conditions

- [x] **Thread-Safe File Operations** - All file writes now protected *(Completed 2026-02-09)*
  - `DragonService.cs` - Added `_historyLock` to DragonSession for MessageHistory thread-safety
    - SaveHistoryToFileAsync: Snapshot under lock before serialization
    - TrackMessage: Lock when adding/trimming messages
    - Session resume: Lock when replaying messages
    - LoadHistoryFromFileAsync: Lock when loading messages
    - Context clear: Lock when clearing history
  - `Specification.cs` - Added internal locking with thread-safe methods
    - GetFeaturesCopy(): Returns snapshot of features
    - WithFeatures(Action): Execute action under lock
    - WithFeatures<T>(Func): Execute function under lock
  - `FeatureManagementTool.cs` - Uses Specification's thread-safe methods
    - SaveFeatures: Takes snapshot for serialization
    - CreateFeature: Uses WithFeatures for modifications
    - UpdateFeature: Uses WithFeatures for modifications
    - ListFeatures: Uses GetFeaturesCopy for reads
  - `SpecificationManagementTool.cs` - Added `_specificationsLock` for dictionary
    - CreateSpecification: File I/O outside lock, dictionary update inside
    - UpdateSpecification: Read path under lock, write file, update under lock
    - LoadSpecification: Read and update dictionary under lock
  - `Wyvern.cs` - Added defensive `_analysisLock`
    - SaveAnalysisAsync: Snapshot under lock before serialization
  - **Pattern**: Snapshot under lock, serialize/write outside lock
  - **Impact**: Prevents race conditions where older state overwrites newer state

### Critical Priority (P0) - Blocking Operation Fixes

- [x] **Dragon Tools (8 files)** - Converted to async File operations *(Completed 2026-02-09)*
  - `AddExistingProjectTool.cs` - File.ReadAllText → File.ReadAllTextAsync
  - `FeatureManagementTool.cs` - File.WriteAllText → File.WriteAllTextAsync
  - `SpecificationManagementTool.cs` - File.ReadAllText/WriteAllText → Async
  - `UpdatePlanStepTool.cs` - Kept GetAwaiter().GetResult() (Tool.Execute constraint)
  - `DelegateToCouncilTool.cs` - Kept GetAwaiter().GetResult() (Tool.Execute constraint)
  - `GitMergeTool.cs` - Kept GetAwaiter().GetResult() (Tool.Execute constraint)
  - `GitStatusTool.cs` - Kept GetAwaiter().GetResult() (Tool.Execute constraint)
  - `ModifyPlanTool.cs` - Changed .Wait() → GetAwaiter().GetResult()

- [x] **Core Services (6 files)** - Converted to async File operations *(Completed 2026-02-09)*
  - `TaskTracker.cs` - SaveToFile/LoadFromFile → Async internally
  - `KoboldPlanService.cs` - File.Delete → Task.Run wrapper
  - `ProjectConfigurationService.cs` - File.ReadAllText → Async
  - `ProjectRepository.cs` - File.ReadAllText/WriteAllText → Async
  - `ProviderConfigurationService.cs` - File.ReadAllText/WriteAllText → Async
  - `DragonService.cs` - Kept GetAwaiter().GetResult() (LINQ projection constraint)

- [x] **Orchestrators (3 files)** - Converted to async File operations *(Completed 2026-02-09)*
  - `Drake.cs` - File.ReadAllText → File.ReadAllTextAsync
  - `Wyvern.cs` - File.ReadAllText → File.ReadAllTextAsync
  - `ProjectService.cs` - Removed ConfigureAwait(false) from sync wrappers

- [x] **Agent Tools (1 file)** - Optimized async usage *(Completed 2026-02-09)*
  - `AskUser.cs` - Removed ConfigureAwait(false) from sync wrapper

**Impact**: Eliminated all blocking synchronous I/O operations, reducing server lag and preventing context loss under load.

### Previous Server Lag Fixes (Completed 2026-02-05)

- [x] **Async File Operations** - Convert synchronous I/O to async
  - Added `SaveToFileAsync()` and `LoadFromFileAsync()` to `TaskTracker.cs`
  - Added `SaveProjectsAsync()`, `AddAsync()`, `UpdateAsync()`, `DeleteAsync()` to `ProjectRepository.cs`
  - Added debounced `SaveConfigurations()` to `ProjectConfigurationService.cs`
  - Impact: 100+ blocking writes/minute with parallel Kobolds - now non-blocking

- [x] **Debounced Task File Writes** - Coalesce rapid writes *(Completed 2026-02-05)*
  - Implemented Channel-based write debouncing in `Drake.cs`
  - 2-second debounce interval coalesces rapid status updates
  - Added `ProcessSaveQueueAsync()` for background write processing
  - Impact: 50x reduction in file I/O achieved

- [x] **Remove GetAwaiter().GetResult()** - Eliminate deadlock risk *(Completed 2026-02-05)*
  - `Drake.cs` - Added `SyncTaskFromKoboldAsync()`, `MonitorTasksAsync()`, `HandleStuckKoboldsAsync()`
  - `Wyvern.cs` - Added `AssignFeaturesAsync()`
  - `ProjectService.cs` - Added `CreateProjectFolderAsync()`, `ApproveProjectAsync()`
  - Updated `DrakeMonitoringService.cs` to use async methods

### High Priority (P1) - Additional Optimizations

- [x] **WebSocket Message Optimization** - Remove reflection from hot path *(Completed 2026-02-05)*
  - `DragonService.cs` - `SendTrackedMessageAsync()` now uses `JsonSerializer.SerializeToNode`
  - Eliminates reflection by using JSON DOM manipulation

- [x] **Background Service Throttling** - Add parallelism limits *(Completed 2026-02-05)*
  - `DrakeExecutionService.cs` - Added SemaphoreSlim with max 5 concurrent projects
  - `DrakeMonitoringService.cs` - Added SemaphoreSlim with max 5 concurrent Drakes
  - `WyvernProcessingService.cs` - Added SemaphoreSlim with max 5 concurrent projects

- [x] **Cache Directory Enumeration** - Reduce filesystem calls *(Completed 2026-02-05)*
  - `DragonService.cs` - Added 30-second cache for specification file enumeration
  - `GetCachedSpecificationFiles()` method with thread-safe caching

---

## Phase A - Quick Wins & Foundation

### High Priority

- [x] **Stuck Kobold Detection** - `DrakeMonitoringService.cs:148` *(Completed 2026-02-01)*
  - Added `GetStuckKobolds()` method to KoboldFactory
  - Added `MarkAsStuck()` method to Kobold model
  - Added `HandleStuckKobolds()` method to Drake orchestrator
  - Configurable timeout via `AgentLimits.StuckKoboldTimeoutMinutes` (default: 30)
  - Monitoring interval via `AgentLimits.MonitoringIntervalSeconds` (default: 60)

- [x] **Retry Logic for API Calls** - Agent/Providers *(Completed 2026-02-03)*
  - Added `RetryPolicy` class with configurable settings (MaxRetries, InitialDelayMs, BackoffMultiplier, AddJitter)
  - Added `SendWithRetryAsync` method to `LlmProviderBase` with exponential backoff
  - Handles 429 (rate limiting), 5xx errors, timeouts, and network failures
  - Respects `Retry-After` header when present
  - Updated all 10 providers: OpenAI, Claude, Gemini, Azure OpenAI, GitHub Copilot, Ollama, Z.AI, and OpenAI-compatible base (LlamaCpp, vLLM, SGLang)

- [x] **Proper Logging System** - Solution-wide *(Completed 2026-02-06)*
  - [x] Replace Console.WriteLine in `Kobold.cs`, `UpdatePlanStepTool.cs` (✓ Done)
  - [x] Add logging configuration to appsettings.json (per-namespace levels) (✓ Done)
  - [x] Inject ILogger into Kobold via KoboldFactory (✓ Done)
  - [x] Add structured logging with datetime timestamps via OpenTelemetry (✓ Done)
  - [x] Updated DI registration in Program.cs (✓ Done)
  - [x] Build verification passed (✓ Done)
  - [x] Application runs successfully with Aspire dashboard (✓ Done)
  - Note: DraCode CLI uses Spectre.Console for UI rendering (not logging)
  - Note: Existing services already use ILogger extensively (20+ files)
  - Note: LLM providers use SendMessage callback for user-facing messages
  - Effort: 1 day (Completed)

---

### From Phase B - Testing & Quality

- [ ] **Unit Tests for All Tools** - `DraCode.Agent/Tools/`
  - Add tests for all 7 built-in tools
  - Mock file system and command execution
  - Effort: Medium (~1 week)

- [ ] **Rate Limiting** - Agent/Providers
  - Per-provider quota enforcement
  - Configurable limits per provider
  - Effort: Medium (~2-3 days)

---

### From Phase C - User Experience

- [ ] **Encrypted Token Storage**
  - Windows DPAPI, macOS Keychain, Linux secret managers
  - Migration path from plain text storage
  - Effort: Medium (~2-3 days)

### From Phase D - Extensibility

- [ ] **Plugin System for Custom Tools**
  - Assembly loading for external tools
  - Tool marketplace concept
  - Effort: High (~2 weeks)

- [ ] **Web UI Improvements**
  - Drag-and-drop tab reordering
  - Save/load workspace configurations
  - Export conversation histories
  - Side-by-side comparison view
  - Agent performance metrics
  - Effort: Medium (ongoing)

- [ ] **Multi-Agent Collaboration**
  - Agent-to-agent communication protocol
  - Shared task broadcasting
  - Effort: High (~2-3 weeks)

- [ ] **Cost Tracking**
  - Token usage monitoring per provider
  - Budgeting and alerts
  - Per-user/project tracking
  - Effort: Medium (~1 week)

### From Phase E - Enterprise Features

- [ ] **Team Collaboration**
  - Shared agents and workspaces
  - Role-based access control (RBAC)
  - Effort: High

- [ ] **Audit Logging**
  - Track all agent actions
  - Compliance reporting
  - Effort: Medium

- [ ] **CI/CD Integration**
  - GitHub Actions workflows
  - Docker containerization
  - Effort: Medium

### Future Enhancements (Deferred)

- [ ] **Parallel Step Execution** - From Phase G
  - `StepDependencyAnalyzer` created but not integrated
  - Execute independent steps in parallel with separate Kobold instances
  - Potential speedup: 2-3x for large plans
  - Effort: High (~2 weeks)

- [ ] **Intelligent Step Reordering** - From Phase G
  - Use `StepDependencyAnalyanalyzer.SuggestOptimalOrder()`
  - Suggest optimal execution order to planner
  - Effort: High (~2 weeks)

---

## ✅ COMPLETED - For Reference

### Phase 0 - Performance Critical (Completed 2026-02-05)

- [x] **Async File Operations** - Non-blocking I/O
- [x] **Debounced Task File Writes** - 50x reduction in file I/O
- [x] **Remove GetAwaiter().GetResult()** - Eliminate deadlock risk
- [x] **WebSocket Message Optimization** - Remove reflection from hot path
- [x] **Background Service Throttling** - Parallelism limits
- [x] **Cache Directory Enumeration** - Reduce filesystem calls

### Phase A - Quick Wins & Foundation (Completed 2026-02-06)

- [x] **Stuck Kobold Detection** - Configurable timeout monitoring
- [x] **Retry Logic for API Calls** - Exponential backoff for all providers
- [x] **Proper Logging System** - OpenTelemetry with structured logging

### Phase B - State & Reliability (Completed 2026-02-01)

- [x] **Markdown Parser for Task Persistence** - Drake task loading

### Phase C - User Experience (Completed 2026-02-06)

- [x] **Streaming Response Support** - All 10 providers support streaming

### Phase D - Extensibility (Completed 2026-02-06)

- [x] **Custom Agent Types** - DebugAgent, RefactorAgent, TestAgent

### Phase G - Kobold Execution Strategy (Completed 2026-02-06)

#### Phase 1: Quick Wins (2-3 hours) ✅ COMPLETED
- [x] **Automatic File Validation** - `Kobold.cs` *(Completed 2026-02-06)*
  - Added `ValidateStepCompletionAsync()` method to check if expected files exist
  - Verifies `FilesToCreate` items were created
  - Verifies `FilesToModify` items were modified (timestamp check)
  - Added `ValidateAndLogStepCompletionAsync()` for advisory logging
  - Effort: Low (~1 hour)

- [x] **Step-Aware Iteration Budget** - `Kobold.cs`, `AgentOptions.cs` *(Completed 2026-02-06)*
  - Added `MaxIterationsPerStep` configuration option (default: 10)
  - Calculates dynamic per-step budget based on total steps
  - Formula: `Math.Min(MaxIterations / totalSteps + 2, MaxIterationsPerStep)`
  - Prevents one step from consuming entire iteration budget
  - Integrated into `StartWorkingWithPlanAsync` execution loop
  - Effort: Low (~1 hour)

- [x] **Step Reflection Prompts** - `Kobold.cs` *(Completed 2026-02-06)*
  - Added reflection reminder to plan prompt in `BuildFullPromptWithPlan()`
  - Guidance: "Did I create/modify files? Is step complete? Call update_plan_step"
  - Appears in initial prompt for all plan-based execution
  - Effort: Low (~1 hour)

#### Phase 2: Robustness (1 day) ✅ COMPLETED
- [x] **Automatic Step Completion Detection** - `Kobold.cs` *(Completed 2026-02-06)*
  - Added `RunWithStepDetectionAsync()` custom execution loop
  - Checks `ValidateStepCompletionAsync()` after each iteration if agent didn't call `update_plan_step`
  - Tracks current step index and monitors step transitions
  - Serves as safety net for forgetful agents
  - Effort: Medium (~4 hours)

- [x] **Fallback Auto-Advancement** - `Kobold.cs` *(Completed 2026-02-06)*
  - Auto-advances if validation passes but step not marked
  - Logs warning when auto-advancement occurs (agent reliability metric)
  - Updates `CurrentStepIndex` and saves plan
  - Maintains resumability even without agent cooperation
  - Effort: Medium (~2 hours)

- [x] **Enhanced Step Boundary Logging** - `Kobold.cs` *(Completed 2026-02-06)*
  - Added structured logs at step start with file lists
  - Added structured logs at step completion with iteration counts
  - Includes: step index, title, status, files touched, iterations used
  - Enables better debugging and telemetry
  - Effort: Low (~2 hours)

#### Phase 3: Optimization (2 days) ✅ COMPLETED
- [x] **Progressive Detail Reveal** - `Kobold.cs` *(Completed 2026-02-06)*
  - Modified `BuildFullPromptWithPlan()` to show different detail levels
  - Current step: Full details (title, description, files)
  - Next 1-2 steps: Medium details (title + description only)
  - Remaining steps: Summary only (titles with status icons)
  - Reduces token usage for large plans (10+ steps)
  - Configuration options: `UseProgressiveDetailReveal`, `MediumDetailStepCount`
  - Effort: Medium (~4 hours)

- [x] **Step-Level Telemetry** - `KoboldImplementationPlan.cs`, `Kobold.cs` *(Completed 2026-02-06)*
  - Created `StepExecutionMetrics` model for tracking
  - Track: iterations used, time taken, estimated tokens, success/failure
  - Added metrics collection to `RunWithStepDetectionAsync()`
  - Store metrics in `ImplementationStep.Metrics`
  - Created `PlanExecutionMetrics` for aggregated stats
  - Added `GetAggregatedMetrics()` method to plan
  - Effort: Medium (~4 hours)

- [x] **Step Completion Validation Framework** - New validation framework *(Completed 2026-02-06)*
  - Created `IStepValidator` interface for pluggable validators
  - Implemented `FileCreationValidator` (checks files created)
  - Implemented `FileModificationValidator` (checks files modified)
  - Created `StepValidationService` to manage validators
  - Updated `Kobold.cs` to use validation service
  - Validators are extensible for future types (compilation, tests, etc.)
  - Effort: Medium (~1 day)

#### Phase 4: Advanced Features
- [x] **Agent-Suggested Plan Modifications** *(Completed 2026-02-06)*
  - Created `ModifyPlanTool` with support for 4 operations:
    * skip: Skip a step with reason
    * combine: Merge two consecutive steps
    * reorder: Move step to different position
    * add: Insert new step at position
  - Integrated into Kobold execution when `AllowPlanModifications` is enabled
  - Auto-approval configurable via `AutoApproveModifications`
  - All modifications logged and persisted in plan
  - Configuration added to `appsettings.json`
  - Effort: Medium (~3 days)



---

## Phase H - Workflow Enhancements (Version 2.6 - Feb 2026)

**Based on comprehensive workflow analysis completed 2026-02-09**  
**Reference:** `session-state/e67f3587-ef56-4f2a-823d-1696f023c4db/workflow-analysis.md`

### High Priority - Auto-Recovery

- [x] **Failure Recovery Service** - Auto-retry transient errors ✅ COMPLETED (2026-02-09)
  - Background service that runs every 5 minutes
  - Detects failed tasks with transient errors (network, timeout, rate limit)
  - Applies exponential backoff (1min, 2min, 5min, 15min, 30min)
  - Auto-retries up to 5 times for known transient failures
  - Failure categorization: transient (network) vs permanent (syntax error)
  - Circuit breaker pattern for provider outages (3 failures = pause 10 min)
  - **Details**: See "🟠 HIGH PRIORITY" section line 550 for full implementation details
  - **Effort**: Completed

### High Priority - Workflow Clarity

- [x] **Wyrm Workflow Clarification** ✅ COMPLETED (2026-02-09)
  - **Decision**: Implemented Option B - WyrmProcessingService
  - Background service between approval and Wyvern
  - Added `WyrmAssigned` project status
  - Wyrm pre-analyzes and recommends agent types
  - Wyvern uses Wyrm results as hints during analysis
  - **Details**: See "🟢 COMPLETED - Wyrm Pre-Analysis Workflow" section line 8 for full details
  - **Effort**: Completed

---

## ⚪ LOW PRIORITY - Enhancements & Future Features

Nice to have. Can be deferred without impacting core functionality.

---

### From Phase H - Observability

- [ ] **Metrics Endpoint** - `/metrics` for Prometheus
  - Expose key metrics:
    - Active agents count (Dragon, Wyvern, Drake, Kobold) by project
    - Task queue depth and throughput
    - Provider API call rates and error rates
    - Token usage per provider per project
    - Task completion rate (done/failed/blocked)
    - Average task duration
    - Kobold resource utilization (% limit used)
  - Use Prometheus.NET library
  - Configure scrape endpoint in appsettings.json
  - Add Grafana dashboard JSON template in `/docs/monitoring/`
  - Effort: Medium (~1 week)

- [ ] **Health Dashboard Web UI** - Real-time monitoring
  - New project: `DraCode.KoboldLair.Dashboard`
  - Features:
    - Live view of active agents (Drake → Kobolds hierarchy)
    - Project progress bars (tasks done/total)
    - Error log stream with filtering
    - Provider status indicators (healthy/rate-limited/down)
    - Kobold resource gauges per project
    - Task timeline visualization
  - Technology: Blazor Server or React + SignalR
  - WebSocket connection to KoboldLair.Server
  - Read-only view (control via Dragon chat)
  - Effort: High (~2 weeks)

### From Phase H - Quality of Life

- [ ] **Batch Task Operations**
  - Dragon tools for bulk operations:
    - `retry_all_failed_tasks(project_id)` - Retry all failed tasks at once
    - `reset_blocked_tasks(project_id)` - Unblock tasks waiting on failed deps
    - `reassign_task(task_id, new_agent_type)` - Change agent for stuck task
  - Status: Currently must retry one-by-one via `retry_failed_task`
  - Impact: Efficiency for large projects with cascading failures
  - Effort: Low (~2 days)

### From Phase I - Context Preservation

- [x] **Persist Dragon Conversation History** ✅ COMPLETED (2026-02-09)
  - Dragon chat history now persisted to `dragon-history.json` per project
  - Implemented `SaveHistoryToFileAsync()` and `LoadHistoryFromFileAsync()`
  - Saves automatically after every message (fire-and-forget async)
  - Loads on session reconnect if project folder set
  - Thread-safe with `_historyLock` to prevent race conditions
  - Prunes to last 100 messages before saving
  - **Location**: `DragonService.cs` - DragonSession class (lines 44-104)
  - **Impact**: Preserves "why" behind requirements decisions across restarts
  - **Details**: Documented in "COMPLETED - Data Integrity & Context Loss Fixes" section (lines 90-149)

- [x] **Specification Version Tracking** ✅ COMPLETED (2026-02-26)
  - Added `ContentHash` (SHA-256) and `VersionHistory` to Specification model
  - Added `SpecificationVersion` and `SpecificationContentHash` to TaskRecord
  - Kobolds detect specification changes and reload context automatically
  - New `view_specification_history` Dragon tool for viewing version history
  - Wyvern captures spec version when creating tasks
  - Drake passes version to Kobold on summon
  - Features file now stores version metadata in wrapped format
  - **Impact**: Prevents specification drift, keeps work consistent during execution
  - **Details**: Specification.cs, TaskRecord.cs, Wyvern.cs, Kobold.cs, Drake.cs, FeatureManagementTool.cs, SpecificationHistoryTool.cs

- [x] **Write-Ahead Log for Task State** ✅ COMPLETED (Already implemented)
  - Drake uses `TaskStateWal` to log state transitions before updates
  - Prevents data loss during 2-second debounce window
  - Implements transaction safety for critical state changes
  - **Location**: `TaskStateWal.cs` (full implementation)
  - Integrated in `Drake.cs` constructor (line 127) and state changes
  - WAL entries: Timestamp, TaskId, PreviousStatus, NewStatus, AssignedAgent, ErrorMessage
  - **Impact**: Prevents rare but catastrophic data loss during crashes
  - **Effort**: Already complete

---

## 🟠 HIGH PRIORITY - Functional Gaps & Workflow Reliability

Critical for production use and user trust. Blocks effective multi-agent workflows.

### From Phase H - Workflow Enhancements

- [x] **Failure Recovery Service** - Auto-retry transient errors ✅ COMPLETED (2026-02-09)
  - Background service that runs every 5 minutes
  - Detects failed tasks with transient errors (network, timeout, rate limit)
  - Applies exponential backoff (1min, 2min, 5min, 15min, 30min)
  - Auto-retries up to 5 times for known transient failures
  - Failure categorization: transient (network) vs permanent (syntax error)
  - Circuit breaker pattern for provider outages (3 failures = pause 10 min)
  - Implementation complete with 4 new components:
    - `ErrorClassifier` - Categorizes errors as transient/permanent
    - `ProviderCircuitBreaker` - Tracks provider health
    - `FailureRecoveryService` - Background service for auto-retry
    - Extended `TaskRecord` with retry tracking fields
  - Impact: Reduces manual intervention for temporary issues (network glitches)
  - **Effort**: Completed

### From Phase I - Context Preservation

- [x] **Pass Dependency Task Outputs to Kobolds** 🟠 HIGH PRIORITY ✅ COMPLETED
  - **Issue**: Task B depends on Task A, but Kobold executing B doesn't know what A created
  - Drake respects dependencies in scheduling, but doesn't pass output context
  - Risk: Duplicate implementations, missing reusable components
  - **Location**: `Drake.cs` - SummonKobold method
  - **Implementation**:
    ```csharp
    // In Drake.SummonKobold():
    // After line 300 (AssignTask), add dependency context
    if (task.Dependencies?.Any() == true)
    {
        var dependencyContext = BuildDependencyContext(task.Dependencies);
        kobold.UpdateSpecificationContext(
            kobold.SpecificationContext + "\n\n" + dependencyContext
        );
    }
    
    private string BuildDependencyContext(List<string> dependencyIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Completed Dependencies");
        sb.AppendLine();
        sb.AppendLine("The following tasks were completed before this one. Use their outputs:");
        sb.AppendLine();
        
        foreach (var depId in dependencyIds)
        {
            var depTask = _taskTracker.GetTask(depId);
            if (depTask?.Status == TaskStatus.Done)
            {
                sb.AppendLine($"### Task: {depTask.Task}");
                
                // Extract files created/modified from git commits
                var outputFiles = GetTaskOutputFiles(depTask.Id);
                if (outputFiles.Any())
                {
                    sb.AppendLine("Files created:");
                    foreach (var file in outputFiles)
                    {
                        sb.AppendLine($"- `{file}`");
                    }
                }
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
    ```
  - **Changes needed**:
    - Track output files in TaskRecord (new property: `OutputFiles`)
    - Extract files from git commits after task completion
    - Include dependency context in Kobold prompt
    - Show files created by dependency tasks
  - **Impact**: Better code reuse, fewer duplicate implementations
  - **Example**: Task B creating UserRepository knows User.cs exists from Task A
  - **Effort**: Medium (~2 days)

- [x] **Shared Planning Context Service** 🟠 HIGH PRIORITY - COORDINATION *(Completed 2026-02-09)*
  - **Issue**: Parallel Kobolds create plans independently, may conflict
  - Created comprehensive SharedPlanningContextService with:
    - Multi-agent coordination (file conflict detection, active agent tracking)
    - Drake supervisor support (registration, monitoring, statistics)
    - Cross-project learning (insights, patterns, best practices)
    - Thread-safe design with concurrent dictionaries
    - Automatic persistence to `planning-context.json`
  - **Implementation**:
    - Service: `DraCode.KoboldLair/Services/SharedPlanningContextService.cs`
    - Integrated with DrakeFactory and Drake constructor
    - Agent registration on Kobold summon
    - Agent unregistration on task completion/failure
    - Shutdown hook for context persistence
  - **Features**:
    - `RegisterAgentAsync` / `UnregisterAgentAsync` - Lifecycle management
    - `IsFileInUseAsync` / `GetFilesInUseAsync` - File conflict detection
    - `GetRelatedPlansAsync` - Find plans touching similar files
    - `GetSimilarTaskInsightsAsync` - Learn from past executions
    - `GetProjectStatisticsAsync` - Aggregate metrics per project
    - `GetCrossProjectInsightsAsync` - Learn from other projects
    - `GetBestPracticesAsync` - Extract patterns by agent type
  - **Documentation**: `docs/SharedPlanningContextService.md`
  - **Impact**: Prevents parallel work conflicts, enables cross-agent learning
  - **Effort**: High (~1 week) - COMPLETED

### From Phase H - Workflow Control

- [x] **Pause/Resume Project Execution** *(Completed 2026-02-09)*
  - Added `ProjectExecutionState` enum: Running, Paused, Suspended, Cancelled
  - Added `ExecutionState` field to `Project.cs` with default value `Running`
  - Updated DrakeExecutionService to filter projects by execution state
  - Added `SetExecutionState` and `GetExecutionState` methods to ProjectService with validation
  - Created 4 new Dragon tools via Warden: `pause_project`, `resume_project`, `suspend_project`, `cancel_project`
  - Updated `ListProjectsTool` to display execution state with icons (▶️⏸️⏹️❌)
  - Use cases: High system load, debugging, change of plans
  - **Impact**: Essential for production use - users now have full execution control
  - **Effort**: High (~1 week) - COMPLETED

- [x] **Task Prioritization System** ✅ COMPLETED (2026-02-09)
  - Created `TaskPriority` enum: Critical, High, Normal, Low
  - Updated Wyvern analysis prompt with priority assignment guidelines
  - Updated Drake.GetUnassignedTasks() to sort by priority → dependency → complexity
  - Added `set_task_priority` Dragon tool for manual priority override
  - Added priority logging to DrakeExecutionService
  - Updated Warden agent with new tool and documentation
  - **Impact**: Optimizes execution order for large projects (20+ tasks)
  - **Effort**: Completed in 1 session

---

## 🟡 MEDIUM PRIORITY - Correctness & Quality

Important for reliability but workarounds exist. Improves system quality.

### From Phase I - Context Preservation

- [x] **Specification Version Tracking** 🟡 MEDIUM PRIORITY ✅ COMPLETED (2026-02-26)
  - **Issue**: Specification updated mid-execution, old Kobolds use stale context
  - **Solution**: Implemented comprehensive version tracking system
  - **Files Modified**:
    - `Specification.cs` - Added ContentHash, VersionHistory, IncrementVersion(), ComputeHash()
    - `TaskRecord.cs` - Added SpecificationVersion, SpecificationContentHash
    - `Wyvern.cs` - Captures spec version when creating tasks
    - `Kobold.cs` - Detects version changes, reloads context via EnsureCurrentSpecificationAsync()
    - `Drake.cs` - Passes version to Kobold on summon
    - `FeatureManagementTool.cs` - Persist version in wrapped JSON format
    - `SpecificationHistoryTool.cs` - NEW Dragon tool for viewing history
  - **Impact**: Prevents specification drift, keeps work consistent during execution
  - **Effort**: Completed (~1 day)

### From Phase H - Workflow Clarity

- [x] **Wyrm Workflow Clarification** ✅ COMPLETED (2026-02-09)
  - **Decision**: Implemented Option B - WyrmProcessingService
  - WyrmProcessingService runs as background service (60s interval)
  - Monitors `New` projects and creates Wyrm recommendations
  - Workflow: New → WyrmAssigned → Analyzed → InProgress
  - Full implementation details in "COMPLETED - Wyrm Pre-Analysis Workflow" section above
  - **Effort**: Completed

### From Phase F - Performance Optimizations

- [ ] **Debounced Plan Step Writes** - `UpdatePlanStepTool.cs`
  - Apply Drake's debouncing pattern to Kobold plan updates
  - Add Channel-based write queue with configurable delay (2-3 seconds)
  - Coalesce rapid plan step updates from parallel Kobolds
  - Expected impact: Reduce filesystem writes during active Kobold execution
  - Similar to Drake optimization that achieved 50x I/O reduction
  - **Effort**: Medium (~2-3 days)

### From Phase I - Tracking & Visibility

- [x] **Enhanced Git Commit Messages with Context** ✅ COMPLETED (2026-02-11)
  - **Issue**: Git commits lack feature context, task IDs, traceability
  - **Solution**: Implemented `BuildDetailedCommitMessage()` in `Drake.cs`
  - **Commit message now includes**:
    - Conventional commit format: `feat({agent}): {description}`
    - Task-Id, Agent-Type, Priority trailers
    - Feature context (from Wyvern analysis or branch name)
    - Dependencies (parsed from task description)
    - Project ID
  - Added `GetFeatureNameById()` helper to `Wyvern.cs`
  - **Location**: `Drake.cs:867-980`, `Wyvern.cs:251-258`
  - **Benefits**:
    - Better git history readability
    - Easy feature tracking and rollback
    - Traceability between tasks and code changes
    - Useful for compliance and audit
  - **Effort**: Completed

- [x] **Task Output File Tracking** ✅ COMPLETED (Already implemented)
  - Added `OutputFiles` property to TaskRecord ✓
  - Populates from git diff after task completion ✓
  - Used by dependency context builder ✓
  - Files tracked via `CommitSha` property ✓
  - Extracted via `_gitService.GetFilesFromCommitAsync()` ✓
  - **Location**: `TaskRecord.cs` lines 21-26, `Drake.cs` lines 687-696
  - **Effort**: Already complete

---

## 🗄️ DATABASE MIGRATION - Birko.Framework Integration (2026-02-26)

**Based on analysis of current JSON storage and Birko.Framework capabilities**
**Reference**: Analysis of Birko.Data at `C:/Source/Birko.framework/`

### Overview

Migrate from file-based JSON storage to a hybrid database approach using Birko.Framework's mature data access layer.

| Current JSON Storage | Target Database | Priority |
|---------------------|-----------------|----------|
| `projects.json` | PostgreSQL (via Birko.Data.SQL) | P0 - High |
| `planning-context.json` | MongoDB or JSON (keep as-is) | P1 - Medium |
| `{area}-tasks.json` | PostgreSQL (via Birko.Data.SQL) | P0 - High |
| `kobold-plans/*.json` | PostgreSQL or MongoDB | P1 - Medium |
| `dragon-history.json` | TimescaleDB (time-series) | P2 - Low |
| `analysis.json` | PostgreSQL (via Birko.Data.SQL) | P1 - Medium |
| `wyrm-recommendation.json` | PostgreSQL (via Birko.Data.SQL) | P1 - Medium |
| `specification.features.json` | PostgreSQL (via Birko.Data.SQL) | P1 - Medium |

---

### Phase 1: Birko.Framework Integration (Foundation)

#### Week 1: Setup & PostgreSQL Connector

- [ ] **Add Birko.Data project references** ⚪ NOT STARTED
  - Add to `DraCode.KoboldLair.Server`:
    - `..\..\Birko.Data\Birko.Data.shproj`
    - `..\..\Birko.Data.SQL\Birko.Data.SQL.shproj`
    - `..\..\Birko.Data.SQL.MSSql\Birko.Data.SQL.MSSql.shproj`
    - `..\..\Birko.Data.JSON\Birko.Data.JSON.shproj`
  - Update `DraCode.slnx` with project references
  - **Effort**: 2 hours

- [ ] **Create PostgreSQL connector for Birko.Data** ⚪ NOT STARTED
  - Create new project: `C:/Source/Birko.Data.SQL.PostgreSQL/`
  - Files:
    - `Birko.Data.SQL.PostgreSQL.shproj` (shared project)
    - `Birko.Data.SQL.PostgreSQL.projitems`
    - `PostgreSqlConnector.cs` (extend `AbstractConnector`)
  - Add NuGet package: `Npgsql` v8.0.3
  - PostgreSQL-specific overrides:
    - Use `SERIAL` for auto-increment (not `IDENTITY`)
    - Use `$1, $2` for parameters (not `@p1`)
    - Use `TRUE/FALSE` for boolean (not `1/0`)
  - **Location**: Follow pattern from `Birko.Data.SQL.MSSql/MSSqlConnector.cs`
  - **Effort**: 6 hours

- [ ] **Create repository abstraction layer** ⚪ NOT STARTED
  - Create `DraCode.KoboldLair/Data/Repositories/` directory
  - Create interfaces:
    - `IProjectRepository.cs` (abstract current `ProjectRepository`)
    - `ITaskRepository.cs` (new interface for task storage)
    - `IPlanningContextRepository.cs` (new interface for planning context)
  - Create `RepositoryFactory.cs` for backend selection
  - **Effort**: 4 hours

#### Week 2: Model Updates for Birko.Data

- [ ] **Update Project model to inherit from Birko.Data.Models.AbstractModel** ⚪ NOT STARTED
  - Add `[Table("projects")]` attribute
  - Add `[Field]` attributes to properties
  - Replace `Id` string with `GUID` Guid property
  - Implement `ILoadable<T>` for ViewModel pattern
  - **Files to modify**:
    - `DraCode.KoboldLair/Models/Projects/Project.cs`
    - `DraCode.KoboldLair/Models/Projects/ProjectPaths.cs`
    - `DraCode.KoboldLair/Models/Projects/ProjectTimestamps.cs`
    - `DraCode.KoboldLair/Models/Projects/AgentConfig.cs`
    - `DraCode.KoboldLair/Models/Projects/ProjectSecurity.cs`
  - **Effort**: 6 hours

- [ ] **Update TaskRecord model for Birko.Data** ⚪ NOT STARTED
  - Add `[Table("tasks")]` attribute
  - Add `[Field]` attributes to properties
  - Replace `Id` string with `GUID` Guid property
  - Implement `ILoadable<T>` for ViewModel pattern
  - **Files to modify**:
    - `DraCode.KoboldLair/Models/Tasks/TaskRecord.cs`
    - `DraCode.KoboldLair/Models/Tasks/TaskStatus.cs`
    - `DraCode.KoboldLair/Models/Tasks/TaskPriority.cs`
  - **Effort**: 4 hours

- [ ] **Create ViewModel classes** ⚪ NOT STARTED
  - `ProjectViewModel.cs` (implements `ILoadable<Project>`)
  - `TaskViewModel.cs` (implements `ILoadable<TaskRecord>`)
  - Separate ViewModels from domain models (Birko pattern)
  - **Effort**: 3 hours

---

### Phase 2: SQL Repository Implementation

#### Week 3: Core Repositories

- [ ] **Implement SqlProjectRepository** ⚪ NOT STARTED
  - Inherit from `DataBaseRepository<ProjectViewModel, Project, PostgreSqlConnector>`
  - Implement CRUD operations:
    - `GetByIdAsync(Guid id)`
    - `GetByNameAsync(string name)`
    - `GetByStatusesAsync(params ProjectStatus[] statuses)`
    - `AddAsync(Project project)`
    - `UpdateAsync(Project project)`
    - `DeleteAsync(Guid id)`
  - Implement agent configuration queries:
    - `GetAgentConfig(string projectId, string agentType)`
    - `SetAgentLimitAsync(...)`
    - `GetAllowedExternalPaths(...)`
  - **Location**: `DraCode.KoboldLair/Data/Repositories/Sql/SqlProjectRepository.cs`
  - **Effort**: 8 hours

- [ ] **Implement SqlTaskRepository** ⚪ NOT STARTED
  - Inherit from `DataBaseRepository<TaskViewModel, TaskRecord, PostgreSqlConnector>`
  - Implement task-specific queries:
    - `GetTasksByProjectAsync(string projectId)`
    - `GetTasksByStatusAsync(TaskStatus status)`
    - `GetUnassignedTasksAsync(string projectId)`
    - `GetTasksByPriorityAsync(TaskPriority priority)`
    - `GetTasksWithDependenciesAsync(string taskId)`
  - Implement bulk operations:
    - `UpdateTasksAsync(IEnumerable<TaskRecord> tasks)`
  - **Location**: `DraCode.KoboldLair/Data/Repositories/Sql/SqlTaskRepository.cs`
  - **Effort**: 8 hours

- [ ] **Implement migration system using IConfigurator** ⚪ NOT STARTED
  - Create `DraCode.KoboldLair/Data/Migrations/` directory
  - Implement migrations:
    - `ProjectMigration.cs` (Version 1)
    - `TaskMigration.cs` (Version 1)
    - `AgentConfigMigration.cs` (Version 1)
  - Use `IConfigurator.PreMigrate/PostMigrate/Seed` methods
  - Migrate existing JSON data to SQL
  - **Effort**: 6 hours

#### Week 4: Additional Repositories

- [ ] **Implement SqlPlanningContextRepository** ⚪ NOT STARTED
  - Store: `ProjectPlanningContext`, `AgentPlanningContext`, `PlanningInsight`
  - Consider: Keep as JSON (PostgreSQL JSONB column) due to complexity
  - Tables:
    - `planning_contexts` (project-level data)
    - `planning_insights` (task completion metrics)
    - `file_metadata` (file registry)
  - **Location**: `DraCode.KoboldLair/Data/Repositories/Sql/SqlPlanningContextRepository.cs`
  - **Effort**: 6 hours

- [ ] **Implement SqlKoboldPlanRepository** ⚪ NOT STARTED
  - Store: `KoboldImplementationPlan` with steps
  - Consider: Store steps as separate table or JSONB
  - Tables:
    - `kobold_plans` (plan metadata)
    - `implementation_steps` (individual steps)
  - **Location**: `DraCode.KoboldLair/Data/Repositories/Sql/SqlKoboldPlanRepository.cs`
  - **Effort**: 6 hours

---

### Phase 3: Integration & Testing

#### Week 5: Service Integration

- [ ] **Update ProjectService to use new repository** ⚪ NOT STARTED
  - Inject `IProjectRepository` via DI
  - Replace JSON file operations with repository calls
  - Keep JSON as backup/write-through cache during transition
  - **Location**: `DraCode.KoboldLair/Services/ProjectService.cs`
  - **Effort**: 4 hours

- [ ] **Update TaskTracker to use SqlTaskRepository** ⚪ NOT STARTED
  - Inject `ITaskRepository` via DI
  - Replace JSON file operations with repository calls
  - Keep dual-write (JSON + SQL) during transition
  - **Location**: `DraCode.KoboldLair/Models/Tasks/TaskTracker.cs`
  - **Effort**: 4 hours

- [ ] **Update SharedPlanningContextService** ⚪ NOT STARTED
  - Inject `IPlanningContextRepository` via DI
  - Replace file-based persistence
  - **Location**: `DraCode.KoboldLair/Services/SharedPlanningContextService.cs`
  - **Effort**: 3 hours

- [ ] **Update KoboldPlanService** ⚪ NOT STARTED
  - Inject `IKoboldPlanRepository` via DI
  - Replace file-based plan storage
  - **Location**: `DraCode.KoboldLair/Services/KoboldPlanService.cs`
  - **Effort**: 3 hours

#### Week 6: Configuration & Testing

- [ ] **Add database configuration to appsettings.json** ⚪ NOT STARTED
  ```json
  {
    "KoboldLair": {
      "Data": {
        "DefaultBackend": "PostgreSQL",
        "ConnectionStrings": {
          "PostgreSQL": "Host=localhost;Port=5432;Database=dracode;Username=dracode;Password=***",
          "SQLite": "Data Source=./data/dracode.db",
          "JsonFile": "./projects"
        },
        "EntityStorage": {
          "Projects": "PostgreSQL",
          "Tasks": "PostgreSQL",
          "PlanningContext": "JsonFile",
          "KoboldPlans": "PostgreSQL",
          "DragonHistory": "JsonFile"
        }
      }
    }
  }
  ```
  - **Effort**: 2 hours

- [ ] **Update DI registration in Program.cs** ⚪ NOT STARTED
  - Register repository implementations based on configuration
  - Register PostgreSQL connection string
  - Add health check for database connectivity
  - **Location**: `DraCode.KoboldLair.Server/Program.cs`
  - **Effort**: 3 hours

- [ ] **Create migration tool** ⚪ NOT STARTED
  - CLI command to migrate existing JSON projects to database
  - Options: `--dry-run`, `--backup`, `--verify`
  - Progress reporting and error handling
  - **Location**: `DraCode.KoboldLair/Data/Migrations/MigrationTool.cs`
  - **Effort**: 6 hours

- [ ] **Integration testing** ⚪ NOT STARTED
  - Test CRUD operations for all repositories
  - Test concurrent access (multiple Drakes/Kobolds)
  - Test migration from JSON to SQL
  - Test rollback scenarios
  - **Effort**: 8 hours

---

### Phase 4: Optional Enhancements

#### Future: Additional Storage Backends

- [ ] **Redis caching layer** ⚪ NOT STARTED
  - Cache active agent tracking (with TTL)
  - Cache file locks (auto-expire stale locks)
  - Cache project statistics
  - Use `StackExchange.Redis` package
  - **Effort**: 8 hours

- [ ] **TimescaleDB for Dragon history** ⚪ NOT STARTED
  - Migrate `dragon-history.json` to time-series database
  - Enable efficient time-range queries
  - Add automatic data retention (30-day policy)
  - **Effort**: 6 hours

- [ ] **MongoDB for Planning Context** ⚪ NOT STARTED
  - Migrate `planning-context.json` to MongoDB
  - Native support for nested structures
  - Better performance for insights aggregation
  - Use `MongoDB.Driver` package
  - **Effort**: 8 hours

---

### Success Criteria

| Metric | Target | Status |
|--------|--------|--------|
| JSON file I/O reduction | >90% | ⚪ Not started |
| Query performance (project lookup) | <10ms | ⚪ Not started |
| Concurrent write handling | No corruption at 50+ Kobolds | ⚪ Not started |
| Data migration accuracy | 100% (verified) | ⚪ Not started |
| Rollback capability | <5 minutes | ⚪ Not started |

---

### Notes

- **Birko.Data provides**: Repository pattern, Store pattern, LINQ filtering, bulk operations, migration system
- **Current JSON files**: Keep as backup during transition, remove after validation period
- **PostgreSQL chosen**: Open-source, mature, excellent JSONB support for complex data
- **MongoDB consideration**: Good for planning context (nested insights, file registry)
- **Redis consideration**: Excellent for ephemeral data (active agents, file locks)

---

*Last updated: 2026-02-26*

---

## 🔮 FUTURE / EXPLORATORY (Q3 2026+)

### Self-Reasoning & Meta-Cognition for Agents

**Analysis Document**: `docs/analysis/Self-Reasoning-Analysis.md`

Agents currently operate in a plan-execution loop. Adding self-reflection could transform them into adaptive, learning agents.

#### Implementation Status

| Priority | Enhancement | Agent | Status |
|----------|-------------|-------|--------|
| **P1** | Iteration checkpoints (every 3-5 iterations) | Kobold | ✅ COMPLETED |
| **P1** | Error explanation framework | Kobold | ✅ COMPLETED |
| **P2** | Plan feasibility re-evaluation | Kobold | ⏳ Future |
| **P2** | Success criteria self-assessment | Kobold | ⏳ Future |
| **P3** | Uncertainty estimation | Planner | ⏳ Future |
| **P3** | Workspace conflict reasoning | Drake | ⏳ Future |

#### ✅ Completed: Option 1 - Prompt-Based Self-Reflection (2026-02-12)

**Location**: `DraCode.KoboldLair/Models/Agents/Kobold.cs`

1. **SELF-REFLECTION PROTOCOL** (lines 1070-1082)
   - CHECKPOINT block format added to initial prompt
   - Fields: Progress %, Files done, Blockers, Confidence %, Decision
   - Decision options: continue | pivot | escalate

2. **ERROR HANDLING PROTOCOL** (lines 1084-1093)
   - ERROR ANALYSIS block format for root-cause reasoning
   - Fields: What happened, Root cause, Strategy adjustment

3. **Checkpoint Injection** (lines 810-835)
   - Injects CHECKPOINT reminder every N iterations during execution
   - Configurable via `AgentOptions.CheckpointInterval` (default: 3)
   - Forces agent to pause and reflect mid-step

#### Remaining Work (Option 2 - Structured Tools)

- [ ] **ReflectionTool** - Structured tool forcing explicit reasoning output
  - Capture progress_percent, blockers, confidence, adjustment
  - Trigger Drake intervention if confidence < 30%
  - Auto-escalate if progress stalled 3+ checkpoints
  - Effort: Medium (~1 week)

- [ ] **ReasoningMonitorService** - External service analyzing Kobold outputs
  - Detect repeated error patterns
  - Identify stuck loops (same files modified repeatedly)
  - Flag low-progress iterations
  - Recommend Drake intervention
  - Effort: Medium (~1 week)

#### Expected Benefits (from prompt-based)
- Stuck loop detection: After max retries → After 3-5 iterations
- Error retry success: ~40% → ~65% (adapted approach)
- Plan completion rate: ~70% → ~85% (mid-course corrections)

---

### Under Consideration

- [ ] **KoboldLair CLI Client** - Standalone CLI tool (similar to GitHub Copilot CLI or Claude Code)
  - **Console UI Features**:
    - ASCII art logo displayed on startup
    - Persistent top header with real-time stats (using Spectre.Console layouts)
    - Stats dashboard: Active Kobolds, Tasks (Pending/Working/Done), Token usage, Current mode
    - Color-coded status indicators and progress bars
    - Clean, modern TUI with keyboard shortcuts
  - Interactive mode selection on startup (no parameters prompts for mode choice)
  - Two primary modes:
    - **Dragon Mode**: Interactive requirements gathering chat
    - **Wyvern Mode**: Simplified analysis workflow for direct user control
  - Wyvern Mode behavior:
    - Takes user request as input
    - Analyzes and creates task breakdown
    - Stores all files in `.koboldlair/` folder in current working directory
    - Skips Drake supervision layer (user responsible for task monitoring)
    - User manually invokes Kobolds per task
  - Example usage:
    - `koboldlair` (prompts for mode selection)
    - `koboldlair chat` (direct to Dragon mode)
    - `koboldlair analyze "request"` (direct to Wyvern analysis)
    - `koboldlair run-task <task-id>` (manually execute Kobold)
  - Benefits: Lightweight, local-first, direct control over agent execution
  - Effort: High (~3-4 weeks)

- [ ] **VS Code Extension** - Developer experience
- [ ] **Python SDK** - Broader integration options
- [ ] **RAG for Codebase Understanding** - Research area
- [ ] **Fine-tuned Models** - Custom training on project codebases
- [ ] **OAuth Integration** - Google, GitHub auth providers

---

## 📊 SUCCESS METRICS

### Context Preservation (Phase I)
- **Dragon History Recovery Rate**: % of reconnected sessions with full history (target: 100%)
- **Task Dependency Awareness**: % of Kobolds that reference dependency outputs (target: 80%+)
- **Parallel Conflict Rate**: File conflicts per 100 parallel Kobolds (target: <5)
- **Specification Drift Incidents**: Tasks using wrong spec version (target: 0)
- **Context Loss Reports**: User-reported context issues per month (target: -80%)
- **Git Traceability Score**: % of commits with full metadata (target: 95%+)

### Workflow Reliability (Phase H)
- **Auto-Recovery Rate**: % of transient failures resolved without manual intervention (target: 80%+)
- **Mean Time to Recovery**: Average time from failure to resolution (target: <5 min)
- **Manual Intervention Rate**: User actions per failed task (target: -60%)
- **Observability Score**: User survey - "Can you monitor system health?" (target: 8/10)
- **Workflow Control Score**: User survey - "Can you manage project execution?" (target: 8/10)

### Kobold Execution Quality (Phase G)
- **Step Completion Accuracy**: % of steps correctly marked complete (target: 95%+)
- **Auto-Advancement Rate**: How often auto-detection saves the day (baseline metric)
- **Average Iterations Per Step**: Ensure balanced budget usage (target: even distribution)
- **Task Success Rate**: Overall improvement in task completion (target: +10%)
- **Token Efficiency**: Average tokens per task vs. baseline (target: -15%)

---

*Last updated: 2026-02-12*
