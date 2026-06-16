---
id: FEATURE-037
created: 2026-05-31
owner: human
status: idea
---

# Codify and enforce the deliverable-vs-working-state boundary

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The project draws a line between two kinds of data: the actual product the user asked for (the generated code and its git history) and the agents' internal working state (plans, decisions, reasoning, analysis, notifications). Today that line is implicit, so developers keep rediscovering it and new data sometimes lands in the wrong place — working state ends up as loose files in project folders when it should be in the database. Without a written, enforced rule, the split drifts over time.

## Proposed shape

Write the rule down where developers will see it: deliverables and regenerable views stay as files, everything else (agent working state) goes to the database. Update the storage map so each existing artifact is clearly labelled as either the database source of truth, a deliverable file, or a regenerable view. Add a lightweight guard — a documentation note, a code-review checklist item, or a simple analyzer — that flags when new code tries to persist working state as files in a project folder. Spell out the intentional file-based exceptions (the generated code, git worktrees, generated markdown views) so the rule isn't misread as "never write files".

## Out of scope (initial)

- Actually moving the remaining artifacts to the database — that is its own feature; this one documents and enforces the rule.
- Changing where the deliverable code or git history lives — those intentionally stay on disk.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
