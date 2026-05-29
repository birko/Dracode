---
id: STORY-034
parent: EPIC-016
status: planned
created: 2026-05-29
---

# Codify and enforce the deliverable-vs-working-state boundary

## User story

As a developer extending the pipeline, I want a documented, enforced rule for where new data lives so that working state goes to the DB and only deliverables/regenerable views land on disk — without me having to rediscover the convention each time.

## Behaviour

- CLAUDE.md gains a short section stating the rule: deliverables (`workspace/` code + git) and regenerable views stay as files; all other agent working state goes to the DB.
- The "Data Storage Locations" section of CLAUDE.md is updated to mark each artifact as `DB (source of truth)` or `file (deliverable)` or `file (regenerable view)`.
- A lightweight check (doc note, code-review checklist item, or analyzer) flags new `File.WriteAllText`-style persistence of working state into project folders.
- Edge case: clearly enumerate the *intentional* on-disk exceptions (workspace code, `.worktrees/`, generated `.md` views) so the rule isn't read as "no files ever".
