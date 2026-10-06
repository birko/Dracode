---
id: TASK-101
parent: null
feature: null
status: todo
blocked: 'waiting for the next "Wyvern reply unusable" log line — the failure has not recurred since logging was added'
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

- [ ] From a captured "Wyvern reply unusable" log line, the cause is identified (empty text, preamble object, truncation, other)
- [ ] If the cause is in this codebase (prompt, extraction, iteration limit, provider handling), it is fixed with a regression test proven to fail before the fix; if it is the provider's, the finding records that and the retry/notice behaviour chosen
- [ ] Full suite green

## Out of scope

- Failing loudly and logging the raw reply — done in TASK-098

## Human test plan

N/A — the evidence is a server log line and the fix is covered by its regression test.

## Implementation plan

_Populated by `/tasks plan TASK-101` — leave empty until then._
