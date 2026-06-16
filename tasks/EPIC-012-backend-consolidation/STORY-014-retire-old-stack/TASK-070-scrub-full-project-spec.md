---
id: TASK-070
parent: STORY-014
feature: FEATURE-016
status: done
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-029]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Retire FULL_PROJECT_SPECIFICATION.md (stale regeneration spec)

## Context

Originally scoped as "scrub the old WebSocket/Web stack from the regeneration spec." During execution the scrub revealed the doc is a **predecessor-era regeneration spec**: beyond the old stack, it names the agent library `DraCode.Agent` throughout (the project is now `DraCode.Birko`) and barely reflects the current KoboldLair architecture. A regeneration spec that no longer matches the project actively misleads anyone regenerating from it. **Decision (user, 2026-06-12): retire the doc** rather than partially scrub it.

(History note: commit `706cc81` scrubbed the old stack from the doc; this task then deletes it. The deletion supersedes the scrub — the intermediate state is harmless.)

## Acceptance criteria

- [x] `docs/FULL_PROJECT_SPECIFICATION.md` deleted (`git rm`)
- [x] Links removed from `docs/README.md` (Quick Links + Core Documentation) and `CLAUDE.md` (Documentation Index)
- [x] No non-tracking references to `FULL_PROJECT_SPECIFICATION` remain (grep clean outside `tasks/` + the FEATURE-016 status rollup)
- [x] `dotnet build ./DraCode.slnx` unaffected (doc-only change)

## Out of scope

- Producing a *new* accurate regeneration/architecture spec — if one is wanted later, that's fresh work (CLAUDE.md already documents the current architecture).

## Human test plan

- [x] Confirm the doc is gone and no docs link to it (grep clean) — verified.

## Implementation notes

- `git rm docs/FULL_PROJECT_SPECIFICATION.md`
- Removed the two `docs/README.md` links and the `CLAUDE.md` Documentation Index entry.
- The only remaining mention is the FEATURE-016 `status.md` rollup (tracking), updated to reflect the retirement.
