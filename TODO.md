# TODO - Planned Enhancements

This file tracks planned enhancements and their implementation status.
**Last updated: 2026-03-15 - Added execution flow gaps & architectural fixes from deep pipeline analysis**

---

## 🟢 COMPLETED - Git Workflow & Feature Branch Automation (2026-03-14)

Full automatic git workflow with parallel-safe feature branch execution.

### Implementation Details

- [x] **Git Worktree Support** *(Completed 2026-03-14)*
  - Added `CreateWorktreeAsync()` and `RemoveWorktreeAsync()` to `GitService`
  - Enables parallel Kobold execution on different feature branches without checkout conflicts
  - Worktrees are created at `.worktrees/{branch-name}/` under the project (or external source)

- [x] **Drake Feature Branch Integration** *(Completed 2026-03-14)*
  - `SetupFeatureBranchWorktreeAsync()` - Creates worktree for task's feature branch
  - `CleanupWorktreeAsync()` - Removes worktree after commit
  - `ExecuteTaskAsync()` redirects Kobold workspace to worktree
  - `CommitTaskCompletionAsync()` accepts optional worktree path for commits
  - `GetProjectFolder()` now resolves external source paths for imported projects

- [x] **Feature Completion Notifications** *(Completed 2026-03-14)*
  - `ProjectNotificationService` with disk persistence (`notifications.json`)
  - `Wyvern.UpdateFeatureStatus()` returns newly completed features
  - Drake fires `OnFeatureBranchReady` callback via `DrakeFactory`
  - Notifications survive server restarts (persisted to disk)
  - Dragon sends pending notifications on client connect/reconnect
  - Client displays notifications with icons (merge-ready, project complete)

- [x] **External Project Git Support** *(Completed 2026-03-14)*
  - `GetProjectFolder()` returns `SourcePath` for external projects
  - Git operations (branches, worktrees, commits) target the external repo
  - Worktrees created under external repo's `.worktrees/` directory

**Git Workflow Lifecycle:**
```
ProjectService → git init -b main (auto on project creation)
Wyvern → git branch feature/{id}-{name} (auto per feature)
Drake → git worktree add (auto before Kobold execution)
Kobold → works in worktree/workspace/
Drake → git add -A && git commit (auto on task Done)
Drake → git worktree remove (auto after commit)
Notification → "Branch ready for merge!" (auto on feature complete)
User → Dragon → Sentinel → git_merge (user-initiated)
```

**Files Modified**: `GitService.cs`, `Drake.cs`, `DrakeFactory.cs`, `Wyvern.cs`, `ProjectNotificationService.cs` (new), `DragonService.cs`, `Program.cs`, `dragon-view.js`

---

## 🟢 COMPLETED - New Dragon Council Tools (2026-03-14)

Added 7 new tools and expanded existing tool capabilities for the Dragon Council.

### New Tools

- [x] **ViewTaskDetailsTool** (Warden) - Drill into task details: list/detail/plan actions
- [x] **ProjectProgressTool** (Warden) - Progress analytics: overview/all actions with breakdown
- [x] **ViewWorkspaceTool** (Warden) - Browse workspace files: tree/stats/recent actions
- [x] **DeleteProjectTool** (Warden) - Remove cancelled projects with optional file cleanup
- [x] **DeleteFeatureTool** (Sage) - Delete Draft features from specifications
- [x] **GitDiffTool** (Sentinel) - View branch diffs, commit logs, merge summaries
- [x] **GitCommitTool** (Sentinel) - Create commits: commit/stage/commit_staged actions

### Updated Components
- DragonAgent system prompt: Updated council member descriptions
- DelegateToCouncilTool: Updated capability descriptions
- SentinelAgent: New git tools + expanded workflow documentation
- WardenAgent: 4 new tools with conditional wiring
- SageAgent: DeleteFeature tool + specification completeness checklist

**Total Dragon Tools**: 23 (was 16)

---

## 🟢 COMPLETED - Client Auto-Refresh (2026-03-14)

All KoboldLair client views now auto-refresh data without full page reloads.

- [x] **Dashboard**: In-place stat updates via `data-stat` attributes, green flash animation
- [x] **Projects View**: Status badges and agent stats update in-place
- [x] **Hierarchy View**: Tree HTML comparison with event listener re-attachment
- [x] **Impact View**: Metric comparison refresh when project selected

**Pattern**: `CONFIG.refreshInterval` (5000ms), `onMount()`/`onUnmount()` lifecycle, `Promise.all` for parallel fetches.

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

- [x] **Unit Tests for All Tools** - `DraCode.Agent/Tools/` ✅ COMPLETED (2026-02-27)
  - Created test project `DraCode.Agent.Tests` with xUnit + FluentAssertions
  - 144 passing tests covering all 10 built-in tools:
    - ListFiles, ReadFile, WriteFile, AppendToFile, EditFile
    - SearchCode, RunCommand, AskUser, DisplayText
    - PathHelper (security tests)
  - Mock file system with temp workspaces
  - Cross-platform compatible (Windows/Unix paths)
  - Tests added to solution: `DraCode.slnx`
  - **Location**: `DraCode.Agent.Tests/`
  - **Coverage**: Name/Description validation, Execute behavior, error handling, edge cases
  - **Effort**: Completed

- [x] **Rate Limiting** - Agent/Providers ✅ COMPLETED (2026-03-19)
  - Per-provider quota enforcement via `ProviderRateLimiter` (sliding window)
  - Configurable RPM/TPM/daily limits per provider in `appsettings.json`
  - `TrackedLlmProvider` decorator wraps all providers transparently
  - Disabled by default — zero behavior change for existing installs

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

- [ ] **Web UI Improvements** 🔜 NEXT SESSION
  - [ ] **Side-by-side comparison view** — Compare outputs from different agents/providers (both DraCode.Web and KoboldLair.Client)
  - [ ] **Agent performance metrics** — Token usage, response times, cost per task (both clients)
  - [x] ~~Export conversation histories~~ — Already implemented in KoboldLair.Client (`downloadConversation()`)
  - Drag-and-drop tab reordering (low priority, polish)
  - Save/load workspace configurations (low priority, polish)
  - Effort: Medium

- [x] **Multi-Agent Collaboration** ✅ SUPERSEDED by KoboldLair
  - Original scope: agent-to-agent communication for DraCode.WebSocket simple agent API
  - Now fully realized by KoboldLair's multi-agent hierarchy (Dragon→Wyrm→Wyvern→Drake→Kobold)
  - Agent communication: EventBus pub/sub (2026-03-16), SharedPlanningContextService coordination
  - Shared task context: cross-branch API registry, dependency context passing, file conflict detection
  - Task broadcasting: Drake supervises parallel Kobolds, Wyvern distributes tasks across areas

- [x] **Cost Tracking** ✅ COMPLETED (2026-03-19)
  - Token usage extraction from all 10 LLM providers via `TokenUsage` model
  - `CostTrackingService` with configurable pricing table and budget enforcement
  - Per-call usage records persisted to SQLite via `SqlUsageRepository`
  - Budget alerts: daily, monthly, and per-project limits with warning thresholds
  - Dragon tool: `view_cost_report` (summary, daily, project, budget, rate_limits)
  - `TrackedLlmProvider` decorator auto-records usage after every API call

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

- [x] **Parallel Step Execution** - From Phase G *(Completed 2026-02-27)*
  - Integrated `StepDependencyAnalyzer` into Drake orchestration
  - Added `ExecuteTaskParallelAsync()` method
  - Added step assignment tracking to `KoboldImplementationPlan`
  - Supports configurable `MaxParallelSteps` limit
  - Potential speedup: 2-3x for large plans

- [x] **Intelligent Step Reordering** - From Phase G *(Completed 2026-02-27)*
  - Integrated `StepDependencyAnalyzer.SuggestOptimalOrder()` into planner
  - Added `ReorderSteps()` method to `KoboldImplementationPlan`
  - Added `ValidateStepOrdering()` method for dependency violation detection
  - Automatic reordering when LLM generates steps with wrong dependencies
  - Logged to plan execution log

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

- [x] **Debounced Plan Step Writes** - `UpdatePlanStepTool.cs` ✅ COMPLETED (2026-02-27)
  - Applied Drake's debouncing pattern to Kobold plan updates
  - Added Channel-based write queue with configurable delay (default: 2.5 seconds)
  - Coalesces rapid plan step updates from parallel Kobolds
  - Expected impact: Significant reduction in filesystem writes during active Kobold execution
  - Similar to Drake optimization that achieved 50x I/O reduction
  - **Implementation Details**:
    - Added `PlanSaveQueue` nested class to `KoboldPlanService` for per-plan debouncing
    - `SavePlanDebouncedAsync()` method for non-blocking plan saves
    - `FlushPlanAsync()` for immediate save when plan completes
    - Configuration via `Planning:PlanSaveDebounceIntervalMs` in appsettings.json
    - `KoboldPlanService` now implements `IDisposable` for cleanup
    - Updated `UpdatePlanStepTool` to use debounced saves
    - Updated `Kobold.cs` to use debounced saves and flush on completion
  - **Files Modified**:
    - `DraCode.KoboldLair/Services/KoboldPlanService.cs` - Core debouncing logic
    - `DraCode.KoboldLair/Agents/Tools/UpdatePlanStepTool.cs` - Use debounced saves
    - `DraCode.KoboldLair/Models/Agents/Kobold.cs` - Use debounced saves + flush
    - `DraCode.KoboldLair/Models/Configuration/KoboldLairConfiguration.cs` - Config property
    - `DraCode.KoboldLair.Server/appsettings.json` - Default config value
    - `DraCode.KoboldLair.Server/Program.cs` - DI registration with config
  - **Effort**: Completed (~4 hours)

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

#### Week 1: Setup & SQLite Connector *(Changed from PostgreSQL — SQLite for dev, PostgreSQL later)*

- [x] **Add Birko.Data project references** ✅ COMPLETED (2026-03-15)
  - Added to `DraCode.KoboldLair` (not Server — library needs the types):
    - `Birko.Helpers`, `Birko.Data.Core`, `Birko.Models`, `Birko.Data.Stores`
    - `Birko.Data.Repositories`, `Birko.Data.Patterns`, `Birko.Data.ViewModel`
    - `Birko.Data.SQL`, `Birko.Data.SQL.View`, `Birko.Data.SQL.ViewModel`
    - `Birko.Data.SQL.SqLite`
  - Added NuGet: `System.Data.SQLite` v1.0.119
  - Updated `DraCode.slnx` with all shared project references

- [x] **SQLite connector (pre-existing)** ✅ ALREADY EXISTS
  - `Birko.Data.SQL.SqLite` at `C:/Source/Birko.Data.SQL.SqLite/`
  - Includes: `SqLiteConnector`, `AsyncSqLiteModelRepository`, bulk operations
  - No new code needed — just referenced the existing shared project

- [x] **Create repository abstraction layer** ✅ COMPLETED (2026-03-15)
  - Created `DraCode.KoboldLair/Data/Repositories/` directory
  - Created interfaces:
    - `IProjectRepository.cs` — full API matching existing `ProjectRepository`
    - `ITaskRepository.cs` — async-first task storage interface
  - Created `RepositoryFactory.cs` with `DataStorageConfig` + `StorageBackend` enum
  - `ProjectRepository` now implements `IProjectRepository` (JSON backend)

#### Week 2: Entity Models & ViewModels for Birko.Data

- [x] **Create database entity models** ✅ COMPLETED (2026-03-15)
  - Created `ProjectEntity` inheriting `AbstractDatabaseLogModel` with `[Table("projects")]`
  - Created `TaskEntity` inheriting `AbstractDatabaseLogModel` with `[Table("tasks")]`
  - **Design decision**: Domain models (Project, TaskRecord) kept unchanged. Separate entity classes
    map flat SQL columns. Complex nested objects (Paths, Agents, Security) stored as JSON text columns.
  - **Location**: `DraCode.KoboldLair/Data/Entities/`

- [x] **Create ViewModel classes** ✅ COMPLETED (2026-03-15)
  - `ProjectViewModel` extends `LogViewModel`, implements `ILoadable<ProjectEntity>`
  - `TaskViewModel` extends `LogViewModel`, implements `ILoadable<TaskEntity>`
  - **Location**: `DraCode.KoboldLair/Data/ViewModels/`

- [x] **Create EntityMapper** ✅ COMPLETED (2026-03-15)
  - Bidirectional mapping: `Project ↔ ProjectEntity`, `TaskRecord ↔ TaskEntity`
  - JSON serialization for nested objects (Paths, Agents, Security, Dependencies, etc.)
  - `UpdateEntity()` methods preserve Guid while updating all fields
  - **Location**: `DraCode.KoboldLair/Data/EntityMapper.cs`

- [x] **Create SQL repository implementations** ✅ COMPLETED (2026-03-15)
  - `SqlProjectRepository` — full IProjectRepository with in-memory cache + SQLite persistence
  - `SqlTaskRepository` — full ITaskRepository with async-first operations
  - Both use `AsyncSqLiteModelRepository<T>` from Birko.Data.SQL.SqLite
  - **Location**: `DraCode.KoboldLair/Data/Repositories/Sql/`

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

### Phase 2 (was Phase 3): Service Integration *(Renumbered — Phase 2 SQL repos were done in Phase 1)*

#### Week 5: DI Wiring & Configuration ✅ COMPLETED (2026-03-15)

- [x] **Wire all services to use IProjectRepository** ✅ COMPLETED (2026-03-15)
  - Changed 8 files from concrete `ProjectRepository` to `IProjectRepository`:
    - Library: ProjectService, SharedPlanningContextService, ProjectImplementationService,
      KoboldPlanService, Drake, DrakeFactory
    - Server: DragonService, ProjectConfigCommandHandler, WebSocketCommandHandler
  - All DI registrations in Program.cs updated to resolve `IProjectRepository`
  - Concrete `ProjectRepository` still registered separately (needed as JSON fallback)

- [x] **Add database configuration to appsettings.json** ✅ COMPLETED (2026-03-15)
  - Added `KoboldLair.Data` section with `DefaultBackend` and `SqLitePath`
  - Default: `JsonFile` (zero behavior change for existing installs)
  - Switch to SQLite: change `"DefaultBackend": "SqLite"`
  ```json
  {
    "KoboldLair": {
      "Data": {
        "DefaultBackend": "JsonFile",
        "SqLitePath": "koboldlair.db"
      }
    }
  }
  ```

- [x] **Update DI registration in Program.cs** ✅ COMPLETED (2026-03-15)
  - `IProjectRepository` registered as singleton with backend selection
  - `DataStorageConfig` bound from `KoboldLair:Data` config section
  - SQLite: creates `SqlProjectRepository` via `RepositoryFactory`
  - JsonFile: delegates to existing `ProjectRepository`

#### Week 6: TaskTracker Migration & Testing

- [x] **Update TaskTracker with dual-write to ITaskRepository** ✅ COMPLETED (2026-03-15)
  - Added `Repository`, `ProjectId`, `AreaName` properties to TaskTracker
  - All mutations (AddTask, UpdateTask, SetError, ClearError) fire-and-forget sync to DB
  - Added `LoadFromRepositoryAsync()` for loading from DB instead of files
  - DrakeFactory passes `ITaskRepository` to TaskTrackers on creation
  - `ITaskRepository` registered in Program.cs (SQLite when configured, null otherwise)

- [x] **Create migration tool** ✅ COMPLETED (2026-03-15)
  - `JsonToSqlMigration` reads projects.json + per-area task JSON files
  - Writes to SQLite via `SqlProjectRepository` + `SqlTaskRepository`
  - Supports `--dry-run` mode (reports without writing)
  - Handles idempotent migration (updates existing, inserts new)
  - Returns `MigrationResult` with counts and error list
  - **Location**: `DraCode.KoboldLair/Data/Migrations/JsonToSqlMigration.cs`

- [ ] **Update SharedPlanningContextService** ⚪ DEFERRED
  - Lower priority — works fine with JSON file persistence
  - Will benefit more from Redis caching (Birko.Caching.Redis) than SQL

- [ ] **Update KoboldPlanService** ⚪ DEFERRED
  - Lower priority — plan files are already debounced and per-project
  - SQL migration resolves debounce race (TODO item) but requires deeper refactor

- [x] **Integration testing** ✅ COMPLETED (2026-03-15)
  - Created `DraCode.KoboldLair.Tests` project (xUnit + FluentAssertions)
  - **SqlProjectRepositoryTests** (8 tests): CRUD, name search, status filter, nested JSON round-trip, agent config, concurrent adds
  - **SqlTaskRepositoryTests** (10 tests): CRUD, project/area/status/priority queries, error set/clear, dependency round-trip, concurrent updates
  - **EntityMapperTests** (3 tests): Project round-trip, TaskRecord round-trip, UpdateEntity preserves Guid
  - **JsonToSqlMigrationTests** (4 tests): project migration, task migration, dry-run, idempotency
  - **Results**: 27/27 passing, 902ms total

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

*Last updated: 2026-03-14*

---

## 🟡 MEDIUM PRIORITY - Birko.Framework Integration (Beyond Data Layer)

Additional Birko.Framework libraries (from `C:\Source\Birko.Framework\`) that can replace custom implementations or add missing capabilities. The data layer integration (`Birko.Data`, `Birko.Data.SQL.PostgreSQL`) is tracked in the Database Persistence section above.

**Source**: `C:\Source\Birko.Framework\` — 50+ modular .NET 10.0 libraries

### Background Jobs — Replace Custom PeriodicBackgroundServices

Current state: 5 custom `PeriodicBackgroundService` classes (WyrmProcessingService, WyvernProcessingService, DrakeExecutionService, DrakeMonitoringService, FailureRecoveryService) each with manual timer loops.

- [x] **Integrate `Birko.BackgroundJobs`** ✅ COMPLETED (2026-03-16)
  - Created `FailureRecoveryJob` implementing `IJob` with full retry logic from `FailureRecoveryService`
  - Registered `InMemoryJobQueue`, `JobExecutor`, `JobDispatcher` in DI
  - `RecurringJobScheduler` schedules `FailureRecoveryJob` at 5-minute interval
  - `BackgroundJobProcessor` processes enqueued jobs with retry/backoff
  - `FailureRecoveryService` marked `[Obsolete]`, hosted service registration removed
  - Remaining periodic services (Wyrm, Wyvern, Drake, Monitoring) kept as-is — inherently periodic scanners

### Event Bus — Inter-Agent Communication

Current state: Agent lifecycle events use direct callbacks (`Action<string, string, string>`). No structured event system.

- [x] **Integrate `Birko.EventBus`** ✅ COMPLETED (2026-03-16)
  - Defined 4 event types: `KoboldLifecycleEvent`, `TaskStatusChangedEvent`, `FeatureBranchReadyEvent`, `ProjectStatusChangedEvent`
  - Created handlers: `TaskStatusChangedHandler`, `KoboldLifecycleHandler` (logging)
  - Drake publishes `TaskStatusChangedEvent` on status transitions and `KoboldLifecycleEvent` on start/complete/fail
  - `IEventBus` injected through `DrakeFactory` → `Drake` (null-safe, non-disruptive)
  - In-process bus via `AddEventBus()` — upgrade to distributed later

### Caching — Shared Planning Context & Project Data

Current state: `SharedPlanningContextService` uses in-memory `ConcurrentDictionary` with LRU (max 50 projects) + JSON file persistence.

- [x] **Integrate `Birko.Caching` (MemoryCache)** ✅ COMPLETED (2026-03-15)
  - Replaced manual ConcurrentDictionary + LRU trimming in `SharedPlanningContextService`
  - Project contexts: `ICache` with 30-min sliding expiration (auto-evict inactive)
  - Agent tracking: `ICache` with 2-hour absolute expiration (auto-clean stale agents)
  - `GetOrSetAsync` with built-in stampede protection for project context loading
  - `UpdateAgentActivityAsync` refreshes cache TTL on agent heartbeat
  - JSON file persistence unchanged (disk = durable store, cache = fast access layer)
  - **Package**: `Birko.Caching` (MemoryCache — no Redis dependency needed)
  - **Upgrade path**: Swap `MemoryCache` for `Birko.Caching.Redis` when multi-instance needed

### Validation — Specification & Feature Input

Current state: No structured validation. Dragon accepts any input, Wyvern may fail on malformed specs.

- [x] **Integrate `Birko.Validation`** ✅ COMPLETED (2026-03-16)
  - Created 4 validators: `SpecificationValidator`, `FeatureValidator`, `ProjectConfigValidator`, `ExternalPathValidator`
  - Fluent API: Required, MaxLength, MinLength, Range, Must, GreaterThanOrEqual rules
  - `SpecificationManagementTool` validates specs before create/update — returns descriptive errors on failure
  - Validators registered as singletons in DI

### Security — WebSocket Authentication

Current state: Simple token-based auth via query string (`?token=your-token`).

- [x] **Integrate `Birko.Security.Jwt`** ✅ COMPLETED (2026-03-19)
  - JWT-based authentication with access tokens, refresh tokens, role-based access
  - Roles: admin (all permissions), user (manage projects + execute), viewer (read-only)
  - Auth endpoints: `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`
  - `JwtAuthenticationConfiguration` with embedded user store, env var expansion for secrets
  - `KoboldLairRoleProvider` and `KoboldLairPermissionChecker` implementing Birko.Security.Authorization
  - `RefreshTokenStore` with in-memory storage, token rotation, auto-cleanup
  - Backward compatible: disabled by default, old token auth preserved
  - **New Files**: `Auth/JwtAuthenticationConfiguration.cs`, `Auth/KoboldLairRoleProvider.cs`, `Auth/KoboldLairPermissionChecker.cs`, `Auth/RefreshTokenStore.cs`, `Auth/AuthEndpoints.cs`
  - **Package**: `Birko.Security` + `Birko.Security.Jwt` + `System.IdentityModel.Tokens.Jwt`

### Event Sourcing — Specification Audit Trail

Current state: `Specification.VersionHistory` tracks versions but loses intermediate state.

- [x] **Integrate `Birko.Data.EventSourcing`** ✅ COMPLETED (2026-03-19)
  - Full audit trail of specification changes stored in SQLite `domain_events` table
  - 7 event types: SpecificationCreated, SpecificationUpdated, SpecificationApproved, FeatureAdded, FeatureModified, FeatureRemoved, FeatureStatusChanged
  - `SqlEventStoreRepository` implements `IAsyncEventStore` with SQLite persistence
  - `SpecificationEventService` wraps event store with specification-specific operations
  - Events recorded in `SpecificationManagementTool` (create/update), `FeatureManagementTool` (add/modify), `DeleteFeatureTool` (remove)
  - `SpecificationHistoryTool` shows rich event audit trail when event store available, falls back to VersionHistory
  - Event recording is non-critical (try/catch, never blocks tool execution)
  - **New Files**: `Events/Specification/SpecificationEvents.cs`, `Data/Entities/DomainEventEntity.cs`, `Data/Repositories/Sql/SqlEventStoreRepository.cs`, `Services/EventSourcing/SpecificationEventService.cs`

### Message Queue — Distributed Agent Messaging

Current state: All agents run in-process. No distributed messaging.

- [x] **Integrate `Birko.MessageQueue`** ✅ COMPLETED (2026-03-19)
  - `ITaskDispatcher` abstraction decouples Drake from Kobold creation mechanism
  - `DirectTaskDispatcher` preserves current in-process behavior (default)
  - `QueueTaskDispatcher` publishes task assignments via `IMessageProducer`
  - `TaskAssignmentMessage`, `TaskCompletionMessage`, `KoboldHeartbeatMessage` DTOs
  - `TaskCompletionHandler` implements `IMessageHandler<TaskCompletionMessage>` with EventBus bridge
  - `MessagingConfiguration` with `Enabled` flag, backend selection (InMemory/MQTT), queue names
  - InMemory queue via `Birko.MessageQueue.InMemory` for dev/testing
  - Disabled by default — zero behavior change for existing installs
  - **New Files**: `Messages/TaskAssignmentMessage.cs`, `Messages/TaskCompletionMessage.cs`, `Messages/KoboldHeartbeatMessage.cs`, `MessageQueue/ITaskDispatcher.cs`, `MessageQueue/DirectTaskDispatcher.cs`, `MessageQueue/QueueTaskDispatcher.cs`, `MessageQueue/TaskCompletionHandler.cs`, `Models/Configuration/MessagingConfiguration.cs`
  - **Package**: `Birko.MessageQueue` + `Birko.MessageQueue.InMemory`

### Integration Priority Order

| Priority | Integration | Status | Replaces | Impact |
|----------|------------|--------|----------|--------|
| 1 | Background Jobs | ✅ DONE | FailureRecoveryService | Reliability, persistence |
| 2 | Event Bus | ✅ DONE | Direct callbacks in Drake | Decoupling, extensibility |
| 3 | Validation | ✅ DONE | No validation | Input safety |
| 4 | Caching (MemoryCache) | ✅ DONE | Manual ConcurrentDict + LRU | Performance |
| 5 | Security (JWT) | ✅ DONE | Token query string | Auth robustness |
| 6 | Event Sourcing | ✅ DONE | Version tracking | Audit trail |
| 7 | Message Queue | ✅ DONE | In-process only | Distributed execution |

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

## 🔴 TODO - Execution Flow Gaps & Architectural Fixes (2026-03-15)

Identified during deep analysis of the KoboldLair Server execution pipeline. Items below require larger refactors.

### Already Fixed (2026-03-15) — 28 issues resolved in commit `402dbc1`

<details>
<summary>Click to expand fixed items</summary>

1. ~~"Verified" status dead end~~ → now transitions to `Completed`
2. ~~Spec modification doesn't stop Drakes~~ → stops all Drakes before `SpecificationModified`
3. ~~Single failed task blocks entire area~~ → independent tasks continue
4. ~~PowerShell hardcoded in verification~~ → cross-platform shell detection
5. ~~No stuck project detection~~ → warns if projects stuck >2h
6. ~~Escalations not cleared on retry~~ → `ClearEscalationsForTaskAsync()`
7. ~~Escalation state lost on crash~~ → immediate save after handling
8. ~~DrakeMonitoring file save unhandled~~ → try-catch with logging
9. ~~Duplicate verification fix tasks~~ → deduplication by CheckType
10. ~~Incomplete tech stack detection~~ → added Go, Rust, Java, C++
11. ~~`_taskToKoboldMap` not thread-safe~~ → `ConcurrentDictionary`
12. ~~WAL recovery fire-and-forget~~ → properly awaited with error handling
13. ~~Non-blocking save on critical transitions~~ → Failed status uses async save
14. ~~SharedPlanningContext not injected into Wyrm~~ → added to DI registration
15. ~~No notification on project completion~~ → `NotifyProjectComplete()` called
16. ~~Unknown errors default to Permanent~~ → changed to Transient
17. ~~Escalation routing fails when no Drake~~ → retry 3x with 30s delay
18. ~~Notifications not persisted on shutdown~~ → `PersistAll()` + shutdown hook
19. ~~No config validation~~ → `Math.Clamp`/`Math.Max` on reflection settings
20. ~~External path injection~~ → `Path.GetFullPath()` normalization
21. ~~XSS in Dragon chat~~ → `escapeHtml()` on all message content
22. ~~No circular dependency detection~~ → DFS cycle detection in Wyvern
23. ~~Spec version drift~~ → content hash comparison + downgrade detection
24. ~~Parallel step index out of bounds~~ → validation with warning
25. ~~Plan resume stale checkpoint~~ → bounds check, discard invalid checkpoints
26. ~~Worktree creation not verified~~ → `Directory.Exists()` after git command
27. ~~Commit author email special chars~~ → regex sanitization
28. ~~Error classification too aggressive~~ → unknown errors default to transient

</details>

### Remaining — Requires Larger Refactors

#### Concurrency & Thread Safety

- [x] **Convert tool Execute methods from sync to async** ✅ COMPLETED (2026-03-16)
  - Added `virtual Task<string> ExecuteAsync(...)` to base `Tool` class (default wraps sync `Execute`)
  - `Agent.cs` now calls `await tool.ExecuteAsync(...)` — all tools run in async context
  - Converted 16 tools to proper async, eliminated all `.GetAwaiter().GetResult()` in tools
  - Replaced `lock` with `SemaphoreSlim` in `UpdatePlanStepTool` and `ReflectionTool`

- [x] **Add project-level mutex for git operations** ✅ COMPLETED (2026-03-16)
  - Added `static ConcurrentDictionary<string, SemaphoreSlim> _repoLocks` in `GitService.cs`
  - `RunGitCommandAsync()` acquires per-directory semaphore before executing, releases in `finally`
  - Key is normalized absolute path (`Path.GetFullPath` + trimmed separators)
  - All git operations automatically serialized per working directory — prevents `.git/index.lock` contention

- [x] **Fix blocking .GetAwaiter().GetResult() calls in DragonService callbacks** ✅ COMPLETED (2026-03-16)
  - Callback signatures changed to async: `Func<..., Task>` / `Func<..., Task<T>>`
  - Updated full chain: DragonService → DragonAgent → WardenAgent/SageAgent → Tools
  - `GetProjectInfoList` → `GetProjectInfoListAsync` with proper async iteration
  - Zero `.GetAwaiter().GetResult()` calls remaining in DragonService

#### Data Loss & Persistence

- [x] **Fix plan save debounce race condition** ✅ COMPLETED (2026-03-16)
  - Created `SqlPlanRepository` with `PlanEntity` — immediate atomic SQLite writes on every `SavePlanAsync()`
  - `KoboldPlanService` takes optional `SqlPlanRepository` — writes to DB immediately, file debounce remains for human-readable output
  - `LoadPlanAsync()` reads from SQLite first (most up-to-date), falls back to file
  - Registered in DI with `SqlPlanRepository` wired to `KoboldPlanService`

- [x] **Fix fire-and-forget history save race condition in Dragon sessions** ✅ COMPLETED (2026-03-16)
  - Created `SqlHistoryRepository` with `DragonHistoryEntity` — serialized writes via `SemaphoreSlim`
  - `DragonService.TrackMessage()` uses SQL write when available, file fallback otherwise
  - History keyed by normalized project folder path, stores full message array as JSON
  - Registered in DI and wired into `DragonService` constructor

- [x] **Fix escalation not persisted before dispatch in ReflectionTool** ✅ COMPLETED (2026-03-16)
  - `ReflectionTool` now calls `SavePlanAsync()` (immediate, not debounced) BEFORE invoking escalation callback
  - Non-escalation reflections continue to use debounced save
  - Ensures plan with escalation data is persisted even if callback fails or server crashes

- [x] **Persist circuit breaker state across server restarts** ✅ COMPLETED (2026-03-16)
  - Created `CircuitBreakerEntity` table in SQLite with provider, state, failures, timestamps
  - `ProviderCircuitBreaker.InitializePersistenceAsync()` loads state from DB on startup
  - `RecordFailure()` and `RecordSuccess()` persist state changes to DB (fire-and-forget, non-blocking)
  - Initialized in `Program.cs` after SQLite migration, before services start

#### Git & Worktree Management

- [x] **Add stale worktree cleanup on server startup** ✅ COMPLETED (2026-03-16)
  - Added `PruneStaleWorktreesAsync()` to `GitService.cs` — runs `git worktree prune`, validates each remaining worktree, removes orphans
  - Called in `Program.cs` startup after project config init — iterates all projects (handles both external SourcePath and KoboldLair folders)
  - Cleans up empty `.worktrees/` directory after pruning

- [x] **Fix commit failure not propagating task failure status** ✅ COMPLETED (2026-03-16)
  - Added `CommitFailed` bool property to `TaskRecord`, `TaskEntity`, `TaskViewModel`, `EntityMapper`
  - Drake now sets `CommitFailed = true` on both exception and `committed == false` cases
  - Task remains Done but warning flag is persisted for visibility

#### Client-Side

- [x] **Fix event listener memory leak on view switch** ✅ COMPLETED (2026-03-16)
  - `attachEventListeners()` now stores bound handler references in `this._handlers`
  - Added `detachEventListeners()` to remove all handlers using stored references
  - `onMount()` calls `detachEventListeners()` before `attachEventListeners()` to prevent accumulation
  - `onUnmount()` now calls `detachEventListeners()` and removes delegated tab handler
  - Tab listeners refactored to use event delegation on `.dragon-tabs` container

- [x] **Add notification deduplication on client reconnect** ✅ COMPLETED (2026-03-16)
  - `NotificationStore` now tracks `_seenIds` Set with composite key: `taskId_type_message`
  - `addEscalation()` checks dedup key before adding — duplicate escalations silently ignored
  - `clearEscalations()` also clears the seen IDs set

#### Architecture

- [x] **Fix worktree file path confusion between parallel feature branches** ✅ COMPLETED (2026-03-16)
  - Added `ModuleApiRegistry` (ConcurrentDictionary) to `ProjectPlanningContext` — stores exported API signatures per file
  - Added `RegisterModuleExportsAsync()` and `GetAllModuleExportsAsync()` to `SharedPlanningContextService`
  - `UpdatePlanStepTool` registers API signatures after each step completion (reads created/modified files, extracts exports)
  - `Kobold.EnsurePlanAsync()` merges cross-branch APIs from `SharedPlanningContextService` into local `ExtractModuleApis()` results
  - Local workspace APIs take precedence; cross-branch APIs fill in missing files
  - Added `Kobold.ExtractApiSignaturesFromContent()` static helper for single-file signature extraction

### Birko.Framework Impact Summary

| Category | Total | Resolved | Fully Resolved by Birko | Partially Helped | Manual Fix Only |
|----------|-------|----------|------------------------|------------------|-----------------|
| Concurrency | 3 | **3** ✅ | 0 | 0 | 0 |
| Data Loss | 4 | **4** ✅ | 0 | 0 | 0 |
| Git/Worktree | 2 | **2** ✅ | 0 | 0 | 0 |
| Client-Side | 2 | **2** ✅ | 0 | 0 | 0 |
| Architecture | 1 | **1** ✅ | 0 | 0 | 0 |
| **Total** | **12** | **12** ✅ | **0** | **0** | **0** |

**Status**: All 12 execution flow gaps resolved (2026-03-16).

---

*Last updated: 2026-03-15*
