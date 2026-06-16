---
id: TASK-063
parent: STORY-022
feature: FEATURE-025
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-042]
blocks: [TASK-064, TASK-066]
pr: null
github-issue: null
jira-key: null
---

# Python SDK scaffold + Pydantic models from OpenAPI

## Context

New `koboldlair-python/` package (separate from the .NET solution), PyPI name `koboldlair`. Pydantic models generated from STORY-016's OpenAPI spec (TASK-042) via a pinned generator (`datamodel-code-generator`). Versioning targets `/api/v1`.

## Acceptance criteria

- [ ] `koboldlair-python/` package scaffold (pyproject, src layout, lint/test config)
- [ ] Pydantic models generated from `/api/v1/openapi.json` via pinned `datamodel-code-generator`
- [ ] A `make`/script target regenerates models; schema-hash recorded for the drift guard (TASK-066)
- [ ] Models import cleanly; basic round-trip serialization test

## Out of scope

- Client classes (TASK-064), streaming (TASK-065), publish (TASK-066)

## Human test plan

- [ ] N/A — fully covered by automated tests

## Implementation plan

_Populated by `/tasks plan TASK-063` — leave empty until then._
