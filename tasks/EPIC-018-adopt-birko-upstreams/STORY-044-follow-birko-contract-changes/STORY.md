---
id: STORY-044
parent: EPIC-018
status: planned
created: 2026-10-08
---

# Follow Birko framework contract changes

## User story

As DraCode, I want to finish the follow-up a Birko contract change leaves on DraCode's own code — not just the call sites that broke the build — so that the framework's intent (cancellable tools, Guid naming) holds here too.

## Source

Loose follow-ups to framework changes: Birko TASK-483 (token-taking `Tool.ExecuteAsync`, consumed by TASK-078) →
TASK-079; Birko TASK-506 (Guid naming) → TASK-080. Unlike STORY-038 – STORY-040 these do not delete a local copy —
they finish adapting DraCode's own code to a contract the framework already changed.
