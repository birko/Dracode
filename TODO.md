# TODO - Planned Enhancements

This file tracks planned enhancements and their implementation status.

---

## Phase A - Quick Wins & Foundation (Current)

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

- [ ] **Proper Logging System** - Solution-wide
  - Integrate Serilog or similar
  - Add structured logging to key components
  - Effort: Low (~1 day)

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

- [ ] **Custom Agent Types**
  - DebugAgent - Debugging assistance
  - RefactorAgent - Code refactoring
  - TestAgent - Test generation
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

---

*Last updated: 2026-02-03*
