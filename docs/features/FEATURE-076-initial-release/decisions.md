---
id: FEATURE-076
created: 2025-12-01
---

# Initial release — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Ship a command-line assistant with support for six AI providers (OpenAI, Claude, Gemini, Azure OpenAI, Ollama, GitHub Copilot) and interactive provider selection | approved | shipped in 1.0 | 2025-12-01 | team | — |
| D2 | Give the assistant a seven-action toolset (list files, read file, write file, search code, run command, ask user, display text) | approved | shipped in 1.0 | 2025-12-01 | team | — |
| D3 | Deliver a polished interactive terminal UI with a conversational loop and iteration limits | approved | shipped in 1.0 | 2025-12-01 | team | — |
| D4 | Provide safe configuration: GitHub Copilot sign-in, file-access sandboxing, environment-variable and command-line configuration | approved | shipped in 1.0 | 2025-12-01 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2025-12-01 — feature created; decisions seeded from the shipped 1.0 changelog entry.
