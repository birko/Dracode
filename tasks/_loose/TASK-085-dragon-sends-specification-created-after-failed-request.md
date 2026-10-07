---
id: TASK-085
parent: null
feature: null
status: done
priority: P3
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-004]
pr: null
github-issue: null
jira-key: null
---

# Dragon sends specification_created after a request that failed

## Context

Found 2026-10-03: when the LLM call failed (Z.AI insufficient balance), Dragon still emitted a `specification_created` frame for the
project named in the message, right after the error `dragon_message`. Nothing was created — the client would show a success notice
for a failure.

## Acceptance criteria

- [x] Find what emits `specification_created` after the response and why it fires on a failed request — `DragonService.CheckForNewSpecifications`, run after every response, announced the newest `specification.md` if `DateTime.UtcNow - LastWriteTime < 5 s`. `LastWriteTime` is local time, so on a UTC+2 machine the difference is about −7200 s and *every* existing spec passed — on every response, failed or not (it also re-ran project auto-registration each time). A 30-second cached file list could also miss a spec written during the request
- [x] A failed Dragon request emits no `specification_created` (test covers it) — only a spec written (UTC) since the request was queued is announced; `SpecificationCreatedDetectionTests` (the before-the-request case fails under the old comparison on this UTC+2 machine)

## Out of scope

- The provider failure itself (TASK-086)

## Human test plan

N/A — covered by a DragonService test once the trigger is found.

## Implementation plan

Done 2026-10-07.

1. `DragonService.FindSpecificationWrittenSince(projectsPath, sinceUtc)`: fresh listing, UTC write times, newest written at or
   after `sinceUtc`.
2. `CheckForNewSpecifications` takes the request's `QueuedAt` and uses it; the 30-second spec-file cache (its only reader) is removed.
3. Tests: `SpecificationCreatedDetectionTests`.

## Close notes

- Closed 2026-10-07. Suite 244/244. A Sage *edit* of an existing spec during the request is still announced as
  `specification_created` (as before); only stale specs stopped being announced.