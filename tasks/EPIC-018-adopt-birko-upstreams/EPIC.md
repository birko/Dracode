---
id: EPIC-018
status: planned
created: 2026-10-07
owner: ai
affects: [DraCode.KoboldLair, DraCode.KoboldLair.Server, DraCode.KoboldLair.Client, DraCode.ServiceDefaults]
---

# Adopt Birko framework upstreams

## Area of concern

DraCode wrote generic capability itself where Birko.Framework did not provide it yet. A review on 2026-10-07 sorted it into
"DraCode policy — stays" and "generic — belongs in Birko"; the generic part is filed in the framework as
`Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`. This epic is the consumer side: once a framework
task ships, the matching task here deletes DraCode's local copy and switches to the framework version — the same split
Reps used (framework EPIC-016 ⇄ Reps EPIC-002 / STORY-010).

Each task here is **blocked on its Birko task** until that one is done; the `blocked:` field names it.

## Stories

- [[STORY-038]] Adopt the Birko security / OAuth upstreams (backend auth)
- [[STORY-039]] Adopt Birko.Web upstreams (web client)
- [[STORY-040]] Adopt the Birko KoboldLair upstreams (agent runtime, data, server plumbing) — from the 2026-10-08 review, framework STORY-060 / STORY-061
