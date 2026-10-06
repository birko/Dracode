---
id: TASK-101
parent: null
feature: null
status: done
priority: P3
assignee: ai
created: 2026-10-06
depends-on: [TASK-098]
blocks: []
findings: [FIELD-013]
pr: null
github-issue: null
jira-key: null
---

# Find the root cause of empty Wyvern replies once the new log captures one

## Context

Split from TASK-098 on 2026-10-06. On 2026-10-04 two Wyvern analyses of `drake-switch-check` (Z.AI `glm-5.3`) produced
0 tasks; TASK-098 made such a reply fail the analysis (project → `Failed`, "Analysis failed: Wyvern analysis contained no
tasks …") and log `Wyvern reply unusable for <spec> (<reason>). Raw reply, <n> chars: <excerpt>`. The third run (2026-10-06)
produced 2 tasks, so the cause — empty text (a reasoning-only or truncated reply) versus a reply in an unexpected shape
(`ExtractJson` takes the first balanced `{…}` anywhere in the text) — could not be identified. Blocked until a log line
with the raw reply exists.

## Acceptance criteria

- [x] From a captured "Wyvern reply unusable" log line, the cause is identified (empty text, preamble object, truncation, other) — identified by other evidence: the warning could not be captured (Wyvern had no logger), so the cause came from the failure ("Agent returned empty response"), the provider code and Z.AI's documented limits: `glm-5.3` capped at 4096 output tokens (truncation). Confirmed by the fix: every Wyvern run since (3) succeeded
- [x] If the cause is in this codebase (prompt, extraction, iteration limit, provider handling), it is fixed with a regression test proven to fail before the fix; if it is the provider's, the finding records that and the retry/notice behaviour chosen — the cause is the provider's (Birko Framework `ZAiProvider`), fixed there with a regression test proven red first (Framework TASK-516, `2150d0c8`); the missing Wyvern logger fixed here in `Program.cs`
- [x] Full suite green

## Out of scope

- Failing loudly and logging the raw reply — done in TASK-098

## Human test plan

N/A — the evidence is a server log line and the fix is covered by its regression test.

## Implementation plan

1. Make the raw-reply warning reach the log: `Program.cs` built `WyvernFactory` without a logger factory, so every Wyvern had a null logger and TASK-098's warning never logged.
2. Fix the cause in the provider (Birko Framework TASK-516): `glm-5.3` was capped at 4096 output tokens and a `length` stop was reported as `end_turn`.
3. Live: re-run Wyvern on a small project with both fixes.

## Progress log

- 2026-10-06 — the empty reply recurred during TASK-102's live check (`branch-check`: "Analysis failed: Agent returned empty response."), but the "Wyvern reply unusable" warning did not appear: `WyvernFactory` was built without a logger factory, so Wyvern's logger was null. Wired `loggerFactory` in `Program.cs`.
- 2026-10-06 — cause identified in the provider: every agent runs on `pi-zai` (type `zai`, model `glm-5.3`); `ZAiProvider.GetMaxTokensForModel` had no `glm-5.3` entry and requested the 4096 default (Z.AI documents 65536 default / 131072 maximum for GLM-5.3); with deep thinking on, reasoning shares that budget and a long reply ends empty, while `ParseResponse` reported `end_turn` regardless of `finish_reason`. Fixed in Birko Framework TASK-516 (`2150d0c8`, local, not pushed): glm-5.3 → 131072, glm-5.3-flash → 65536, `length` → `max_tokens`. DraCode builds the framework from source; full suite 216 passed.
- 2026-10-06 — live with both fixes (`branch-check`): first analysis 3 tasks, re-analysis 2 tasks, no empty reply; Wyvern's own log lines now appear (`DraCode.KoboldLair.Orchestrators.Wyvern`).
