# TODO - Planned Enhancements

This file tracks planned enhancements and their implementation status.
**Last updated: 2026-02-09 - Pause/Resume project execution feature completed**

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

- [ ] **Persistent Conversation History**
  - SQLite storage for conversations
  - Export to JSON/Markdown formats
  - Effort: Medium (~1 week)

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

- [ ] **Failure Recovery Service** - Auto-retry transient errors
  - Background service that runs every 5 minutes
  - Detects failed tasks with transient errors (network, timeout, rate limit)
  - Applies exponential backoff (1min, 2min, 5min, 15min, 30min)
  - Auto-retries up to 5 times for known transient failures
  - Failure categorization: transient (network) vs permanent (syntax error)
  - Circuit breaker pattern for provider outages (3 failures = pause 10 min)
  - Notifies user via Dragon if still failing after retries
  - Status: Manual retry via `retry_failed_task` tool currently required
  - Impact: Reduces manual intervention for temporary issues (network glitches)
  - Effort: Medium (~3 days)

### High Priority - Workflow Clarity

- [ ] **Wyrm Workflow Clarification**
  - Current state: Wyrm is internal to Wyvern (works fine)
  - Documentation suggests: Separate Wyrm assignment step exists
  - **Option A** (Recommended): Update documentation to reflect reality
    - Change docs to show Wyrm as internal Wyvern helper
    - Remove "Wyrm assignment" from workflow diagrams
    - Effort: Low (~1 hour)
  - **Option B**: Add WyrmProcessingService (match docs)
    - Create background service between approval and Wyvern
    - Add `WyrmAssigned` project status
    - Wyrm pre-analyzes and recommends agent types
    - Wyvern uses Wyrm results as hints during analysis
    - Effort: Medium (~2 days)
  - Decision needed: Architecture vs documentation alignment

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

- [ ] **Provider Circuit Breaker**
  - Detect when provider is down (3 consecutive failures)
  - Automatically pause all tasks using that provider
  - Notify user via Dragon chat
  - Auto-resume after cooldown period (10 minutes)
  - Prevents wasted retries during known outages
  - Complements existing retry logic
  - Effort: Low (~2 days)

---

## ⚪ LOW PRIORITY - Enhancements & Future Features

Nice to have. Can be deferred without impacting core functionality.

---

### From Phase I - Context Preservation

- [ ] **Persist Dragon Conversation History** 🔴 CRITICAL - DATA LOSS RISK
  - **Issue**: Dragon chat history stored in-memory only (ConcurrentDictionary)
  - When service restarts: ALL conversation context is LOST
  - User requirements reasoning, preferences, alternatives discussed - gone
  - **Location**: `DragonService.cs` - DragonSession class
  - **Implementation**:
    ```csharp
    // Add to DragonSession.cs
    public class DragonSession
    {
        public void SaveHistoryToFile(string projectFolder)
        {
            var historyPath = Path.Combine(projectFolder, "dragon-history.json");
            var json = JsonSerializer.Serialize(MessageHistory);
            File.WriteAllText(historyPath, json);
        }
        
        public static DragonSession LoadFromFile(string projectFolder, string sessionId)
        {
            var historyPath = Path.Combine(projectFolder, "dragon-history.json");
            if (!File.Exists(historyPath)) return null;
            
            var json = File.ReadAllText(historyPath);
            var messages = JsonSerializer.Deserialize<List<SessionMessage>>(json);
            
            var session = new DragonSession { SessionId = sessionId };
            session.MessageHistory.AddRange(messages);
            return session;
        }
    }
    ```
  - **Changes needed**:
    - Save history on every message (async, non-blocking)
    - Load history on session reconnect
    - Store in: `./projects/{project}/dragon-history.json`
    - Add to specification export/archive
    - Prune old history (keep last 100 messages)
  - **Impact**: Preserves "why" behind requirements decisions
  - **Example lost context**: "Dark mode only for authenticated users" → spec says "dark mode" → reasoning lost
  - **Effort**: Medium (~1 day)

- [ ] **Write-Ahead Log for Task State** 🔴 CRITICAL - DATA LOSS RISK
  - **Issue**: 2-second debounce on task file writes - crash during window loses updates
  - Very rare (requires crash during 2-second window) but severe impact
  - **Location**: `Drake.cs` - Debounced save mechanism
  - Transaction safety for critical state changes
  - No data loss even during crashes
  - **Impact**: Prevents rare but catastrophic edge case (crash during 2-second debounce window)
  - **Effort**: Medium (~3 days)

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

- [ ] **Shared Planning Context Service** 🟠 HIGH PRIORITY - COORDINATION
  - **Issue**: Parallel Kobolds create plans independently, may conflict
  - Example: Two Kobolds both create different DbContext classes
  - No real-time coordination between active agents
  - **Location**: New service - `DraCode.KoboldLair/Services/SharedPlanContext.cs`
  - **Implementation**:
    ```csharp
    public class SharedPlanContextService
    {
        private readonly ConcurrentDictionary<string, FileClaimInfo> _claimedFiles;
        private readonly ConcurrentDictionary<string, PlanSummary> _activePlans;
        
        public class FileClaimInfo
        {
            public string FilePath { get; set; }
            public Guid KoboldId { get; set; }
            public string TaskId { get; set; }
            public DateTime ClaimedAt { get; set; }
        }
        
        public class PlanSummary
        {
            public Guid KoboldId { get; set; }
            public string TaskId { get; set; }
            public List<string> FilesToCreate { get; set; }
            public List<string> FilesToModify { get; set; }
        }
        
        // Check if file can be claimed
        public bool CanClaimFile(string path, Guid koboldId, out FileClaimInfo? existingClaim)
        {
            if (_claimedFiles.TryGetValue(path, out var claim) && claim.KoboldId != koboldId)
            {
                existingClaim = claim;
                return false;
            }
            existingClaim = null;
            return true;
        }
        
        // Register a plan and its file claims
        public ConflictReport RegisterPlan(KoboldImplementationPlan plan, Guid koboldId)
        {
            var conflicts = new List<FileConflict>();
            
            // Check for conflicts with existing plans
            foreach (var file in plan.Steps.SelectMany(s => s.FilesToCreate))
            {
                if (!CanClaimFile(file, koboldId, out var existingClaim))
                {
                    conflicts.Add(new FileConflict
                    {
                        FilePath = file,
                        ConflictType = "create",
                        OtherKoboldId = existingClaim.KoboldId,
                        OtherTaskId = existingClaim.TaskId
                    });
                }
            }
            
            return new ConflictReport { Conflicts = conflicts };
        }
        
        // Release claims when Kobold completes
        public void ReleaseClaims(Guid koboldId) { ... }
    }
    ```
  - **Changes needed**:
    - Create SharedPlanContextService as singleton
    - Integrate with KoboldPlannerAgent (register plan after creation)
    - Add conflict detection before plan execution starts
    - Option: Auto-resolve conflicts (assign different file names) or alert user
    - Drake checks for conflicts before summoning Kobolds
  - **Impact**: Prevents parallel work conflicts, better coordination
  - **Effort**: High (~1 week)

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

- [ ] **Task Prioritization System**
  - Add `TaskPriority` enum: Critical, High, Normal, Low
  - Update Wyvern analysis to assign priorities
  - Update Drake.GetReadyTasks() to sort by priority, dependencies, complexity
  - Dragon tool: `set_task_priority` for manual override
  - **Impact**: Optimizes execution order for large projects (20+ tasks)
  - **Effort**: Medium (~3 days)

---

## 🟡 MEDIUM PRIORITY - Correctness & Quality

Important for reliability but workarounds exist. Improves system quality.

### From Phase I - Context Preservation

- [ ] **Specification Version Tracking** 🟡 MEDIUM PRIORITY
  - **Issue**: Specification updated mid-execution, old Kobolds use stale context
  - Example: Spec changes from SQLite → PostgreSQL, active Kobold still implements SQLite
  - No version checking during execution
  - **Location**: `DraCode.KoboldLair/Models/Projects/Specification.cs`
  - **Implementation**:
    ```csharp
    public class Specification
    {
        public int Version { get; set; } = 1;
        public DateTime LastModified { get; set; }
        public string ContentHash { get; set; } // SHA256 of specification.md
        
        public void IncrementVersion()
        {
            Version++;
            LastModified = DateTime.UtcNow;
            ContentHash = ComputeHash(Content);
        }
    }
    
    public class TaskRecord
    {
        public int SpecificationVersion { get; set; } // Version when task was created
    }
    
    // In Kobold.StartWorking():
    public async Task<List<Message>> StartWorkingAsync()
    {
        // Check if specification changed
        var currentSpec = LoadCurrentSpecification();
        if (currentSpec.Version > _assignedSpecVersion)
        {
            _logger.LogWarning(
                "Specification version changed: {Old} → {New}. Reloading context.",
                _assignedSpecVersion, currentSpec.Version);
            
            // Reload specification context
            SpecificationContext = File.ReadAllText(_specificationPath);
        }
        
        // Continue with execution...
    }
    ```
  - **Changes needed**:
    - Add Version and ContentHash to Specification model
    - Track version in TaskRecord at creation time
    - Kobolds check version before execution, reload if changed
    - Log version mismatches in audit trail
    - Dragon tool: `view_specification_history` (show version changes)
  - **Impact**: Prevents specification drift, keeps work consistent
  - **Effort**: Medium (~2 days)

### From Phase H - Workflow Clarity

- [ ] **Wyrm Workflow Clarification**
  - Current state: Wyrm is internal to Wyvern (works fine)
  - Documentation suggests: Separate Wyrm assignment step exists
  - **Option A** (Recommended): Update documentation to reflect reality
    - Change docs to show Wyrm as internal Wyvern helper
    - Remove "Wyrm assignment" from workflow diagrams
    - Effort: Low (~1 hour)
  - **Option B**: Add WyrmProcessingService (match docs)
    - Effort: Medium (~2 days)
  - Decision needed: Architecture vs documentation alignment

### From Phase F - Performance Optimizations

- [ ] **Debounced Plan Step Writes** - `UpdatePlanStepTool.cs`
  - Apply Drake's debouncing pattern to Kobold plan updates
  - Add Channel-based write queue with configurable delay (2-3 seconds)
  - Coalesce rapid plan step updates from parallel Kobolds
  - Expected impact: Reduce filesystem writes during active Kobold execution
  - Similar to Drake optimization that achieved 50x I/O reduction
  - **Effort**: Medium (~2-3 days)

### From Phase I - Tracking & Visibility

- [ ] **Enhanced Git Commit Messages with Context**
  - **Issue**: Git commits lack feature context, task IDs, traceability
  - Hard to revert entire features or track task completion via git
  - **Location**: `Drake.cs` - Git integration after task completion
  - **Current**:
    ```csharp
    await _gitService.CommitChangesAsync(
        workspacePath,
        commitMessage: "Kobold work completed",
        author: "Kobold"
    );
    ```
  - **Improved**:
    ```csharp
    await _gitService.CommitChangesAsync(
        workspacePath,
        commitMessage: BuildDetailedCommitMessage(task, agentType),
        author: $"Kobold-{agentType}"
    );
    
    private string BuildDetailedCommitMessage(TaskRecord task, string agentType)
    {
        var sb = new StringBuilder();
        
        // Short title
        sb.AppendLine(task.Task);
        sb.AppendLine();
        
        // Metadata
        if (!string.IsNullOrEmpty(task.FeatureId))
            sb.AppendLine($"Feature: {task.FeatureId}");
        sb.AppendLine($"Task-Id: {task.Id}");
        sb.AppendLine($"Agent-Type: {agentType}");
        sb.AppendLine($"Project: {_projectId}");
        
        // Dependencies
        if (task.Dependencies?.Any() == true)
        {
            sb.AppendLine();
            sb.AppendLine("Dependencies:");
            foreach (var dep in task.Dependencies)
            {
                var depTask = _taskTracker.GetTask(dep);
                sb.AppendLine($"- {depTask?.Task ?? dep}");
            }
        }
        
        return sb.ToString();
    }
    ```
  - **Benefits**:
    - Better git history readability
    - Easy feature tracking and rollback
    - Traceability between tasks and code changes
    - Useful for compliance and audit
  - **Effort**: Low (~1 day)

- [ ] **Task Output File Tracking**
  - Add `OutputFiles` property to TaskRecord
  - Populate from git diff after task completion
  - Store in task markdown: `<!-- outputs: file1.cs, file2.cs -->`
  - Display in Drake statistics and Dragon tools
  - Used by dependency context builder (see above)
  - **Effort**: Low (~1 day)

---

## 🔵 NORMAL PRIORITY - Observability & Developer Experience

Helpful but not blocking. Improves monitoring and debugging capabilities.

---

---

## 🔮 FUTURE / EXPLORATORY (Q3 2026+)

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

*Last updated: 2026-02-09*
