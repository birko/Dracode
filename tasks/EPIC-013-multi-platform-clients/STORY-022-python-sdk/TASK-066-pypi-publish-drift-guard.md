---
id: TASK-066
parent: STORY-022
feature: FEATURE-025
status: blocked
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-063]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Python SDK PyPI publish + schema-drift guard

## Context

GitHub Actions workflow to publish `koboldlair` to PyPI, versioned to track the server API (`1.x.y` → `/api/v1`). A schema-hash check fails the build fast when the server OpenAPI diverges from the SDK's generated models (the drift risk from STORY-022).

## Acceptance criteria

- [ ] GitHub Actions workflow builds + publishes to PyPI on tagged release
- [ ] Version scheme documented: `koboldlair==1.x.y` targets `/api/v1`
- [ ] CI step compares the live/server OpenAPI schema-hash against the SDK's recorded hash; mismatch fails the build with a clear message
- [ ] Dry-run / TestPyPI path for verification

## Out of scope

- Model generation (TASK-063); client/streaming (TASK-064/065)

## Human test plan

- [ ] Trigger the workflow against TestPyPI → package installs from TestPyPI; bump the server schema without regenerating → CI drift guard fails as expected

## Implementation plan

_Populated by `/tasks plan TASK-066` — leave empty until then._
