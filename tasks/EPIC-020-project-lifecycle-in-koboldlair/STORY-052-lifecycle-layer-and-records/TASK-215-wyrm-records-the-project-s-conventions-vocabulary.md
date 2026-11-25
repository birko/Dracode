---
id: TASK-215
parent: STORY-052
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-211]
blocks: [TASK-219]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Wyrm records the project's conventions, vocabulary and hard-to-reverse choices

## Context

FEATURE-078 D7. Wyrm's pre-analysis already finds constraints and exclusions for one run. It records the project's
conventions, vocabulary and hard-to-reverse choices as lasting project documents, so later checks have rules.

Decisions: FEATURE-078 D7.

## Acceptance criteria

- [ ] Wyrm writes the conventions, vocabulary and hard choices as lasting project documents
- [ ] A later analysis reads and updates them rather than starting over

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] After Wyrm runs on a project, its conventions document lists the rules it found

## Implementation plan

_Populated by `/tasks plan TASK-215` — leave empty until then._
