---
id: TASK-015
parent: STORY-004
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Python SDK

## Context

Python SDK for scripting + data-science workflows. Lets users drive DraCode from Jupyter notebooks, scripts, etc.

## Acceptance criteria

- [ ] `dracode` Python package, published to PyPI
- [ ] `DraCodeClient` class with both async + sync surfaces
- [ ] JWT auth helper (login, refresh, logout)
- [ ] Methods: `chat()`, `analyze()`, `run_task()`, `list_projects()`, `wait_for_completion()`
- [ ] Type hints throughout, mypy clean
- [ ] Notebook-friendly: rich repr for results, progress bars for long-running calls
- [ ] README + example notebook

## Out of scope

- Jupyter widgets (separate `dracode-jupyter` follow-up if demand appears)
- Pandas DataFrame helpers

## Implementation plan

_Populated by `/tasks plan TASK-015` — leave empty until then._
