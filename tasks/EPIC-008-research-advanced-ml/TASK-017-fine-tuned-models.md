---
id: TASK-017
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

# Fine-tuned models on project codebases

## Context

Research task. Custom fine-tuning of an open-weight model on a specific project's codebase + style. Potential gain: lower latency + better adherence to project conventions vs. generic models.

## Acceptance criteria

- [ ] Spike: pick a base model (Qwen-Coder, DeepSeek-Coder, CodeLlama — evaluate)
- [ ] Spike: pick a fine-tuning approach (LoRA, QLoRA, full fine-tune)
- [ ] Sample dataset extraction from a project: code + commit messages
- [ ] Train a small LoRA on a sample project
- [ ] Wire the fine-tuned model into DraCode as a custom provider
- [ ] Benchmark: before/after on the project (task completion, convention adherence)
- [ ] **Decision documented**: ship / pivot / drop

## Out of scope

- Hosting + serving infrastructure (assume local via Ollama / vLLM in v1)
- Multi-project / shared fine-tunes

## Implementation plan

_Populated by `/tasks plan TASK-017` — leave empty until then._
