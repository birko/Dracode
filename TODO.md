# TODO - Planned Enhancements

This file tracks planned enhancements and their implementation status.

---

## Phase 0 - Performance Critical (HIGH PRIORITY)

### Critical Priority (P0) - Server Lag Fixes

- [x] **Async File Operations** - Convert synchronous I/O to async *(Completed 2026-02-05)*
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

## Phase B - State & Reliability

### High Priority

- [x] **Markdown Parser for Task Persistence** - `DrakeFactory.cs:202` *(Completed 2026-02-01)*
  - Added `LoadFromFile()` and `LoadFromMarkdown()` methods to TaskTracker
  - Parses markdown table format (Task | Agent | Status)
  - Handles emoji-prefixed status (🟡 working → Working)
  - Gracefully handles malformed rows and edge cases
  - DrakeFactory now logs loaded task count on startup

- [ ] **Unit Tests for All Tools** - `DraCode.Agent/Tools/`
  - Add tests for all 7 built-in tools
  - Mock file system and command execution
  - Effort: Medium (~1 week)

- [ ] **Rate Limiting** - Agent/Providers
  - Per-provider quota enforcement
  - Configurable limits per provider
  - Effort: Medium (~2-3 days)

---

## Phase C - User Experience (Version 2.5 - Feb 2026)

### Medium Priority

- [ ] **Streaming Response Support**
  - Add `IAsyncEnumerable<string>` to providers
  - Implement SSE endpoint in WebSocket server
  - Update web client for streaming
  - Effort: High (~2 weeks)

- [ ] **Encrypted Token Storage**
  - Windows DPAPI, macOS Keychain, Linux secret managers
  - Migration path from plain text storage
  - Effort: Medium (~2-3 days)

- [ ] **Persistent Conversation History**
  - SQLite storage for conversations
  - Export to JSON/Markdown formats
  - Effort: Medium (~1 week)

---

## Phase D - Extensibility (Version 3.0 - Mar 2026)

### Medium Priority

- [x] **Custom Agent Types** *(Completed 2026-02-06)*
  - DebugAgent - Debugging assistance with error analysis and troubleshooting
  - RefactorAgent - Code refactoring with design patterns and clean code principles
  - TestAgent - Test generation with comprehensive testing strategies
  - Added support for 'debug', 'refactor', 'test' agent types in AgentFactory
  - Effort: Medium (~1 week each)

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

---

## Phase E - Enterprise Features (Version 3.5 - May 2026)

### Lower Priority

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

---

---

## Phase F - Fixes for later

### Lower Priority

- [ ] **UpdatePlanStepTool**
  -  Add a save queue similar to Drake's debouncing mechanism - accumulates saves and flushes after a delay. More complex buthighest performance.

---

## Future / Exploratory (Q3 2026+)

### Under Consideration

- [ ] **VS Code Extension** - Developer experience
- [ ] **Python SDK** - Broader integration options
- [ ] **RAG for Codebase Understanding** - Research area
- [ ] **Fine-tuned Models** - Custom training on project codebases
- [ ] **OAuth Integration** - Google, GitHub auth providers

---

## Technical Debt

| Item | Priority | Effort | Status |
|------|----------|--------|--------|
| Add unit tests for all tools | High | Medium | Pending |
| Implement proper logging system | Medium | Low | Pending |
| Add XML documentation comments | Low | Medium | Pending |
| Refactor configuration loading | Medium | Medium | Pending |
| Add retry logic for API calls | High | Low | **Done** |
| Implement rate limiting | Medium | Medium | Pending |

---

## Completed

- [x] Phase 1: Foundation
- [x] Phase 2: Multi-Provider Support
- [x] Phase 3: GitHub Copilot Integration
- [x] Phase 4: UI Enhancement
- [x] Phase 5: Message Format Fixes
- [x] Phase 6: Multi-Task Execution
- [x] DrakeFactory created
- [x] DrakeMonitoringService created
- [x] Dragon multi-session support
- [x] Wyvern project analyzer
- [x] Git integration (GitService, GitStatusTool, GitMergeTool)
- [x] Kobold Implementation Planner
- [x] Allowed External Paths feature
- [x] Dragon Sub-Agents (Warden, Librarian, Architect)
- [x] LLM Retry Logic with exponential backoff

---

## Recently Completed (v2.4.0)

- [x] **Kobold Implementation Planner** - KoboldPlannerAgent creates structured plans before execution
- [x] **Allowed External Paths** - Per-project access control for directories outside workspace
- [x] **Dragon Sub-Agents** - Specialized agents (Warden, Librarian, Architect)
- [x] **LLM Retry Logic** - Exponential backoff for all 10 providers

*Last updated: 2026-02-03*
