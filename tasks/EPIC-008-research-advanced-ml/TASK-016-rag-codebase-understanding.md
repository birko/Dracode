---
id: TASK-016
parent: EPIC-008
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
feature: FEATURE-006
---

# RAG for codebase understanding

## Context

Research task. Retrieval-Augmented Generation against the project's codebase — Kobolds get relevant code snippets injected as context rather than searching by name. Could measurably improve completion quality on larger codebases.

## Acceptance criteria

- [ ] Spike: pick an embedding model (OpenAI ada-002, Voyage Code, local model — evaluate)
- [ ] Spike: pick a vector DB (Qdrant, Weaviate, Postgres pgvector, in-process FAISS)
- [ ] Indexer that walks the workspace, chunks code, embeds, stores
- [ ] Retrieval API: `IRagService.SearchAsync(query, topK)` returns relevant chunks
- [ ] Integrate retrieval into Kobold prompt (additional context block)
- [ ] Benchmark: before/after task completion rate on a sample project
- [ ] **Decision documented**: ship / pivot / drop based on benchmark

## Out of scope

- Production hardening (this is a spike)
- Continuous re-indexing on file change (poll-based initially)

## Implementation plan

_Populated by `/tasks plan TASK-016` — leave empty until then._
