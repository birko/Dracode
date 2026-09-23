---
id: TASK-079
parent: null
feature: null
# status: todo | in-progress | review (code done, sign-off pending) | blocked | done | cancelled
status: todo
priority: P2
assignee: ai
created: 2026-09-23
depends-on: [TASK-078]
blocks: []
related: [TASK-078]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Thread the cancellation token through the tool bodies

## Context

[[TASK-078]] migrated all 41 `Tool.ExecuteAsync` overrides to the signature that takes a
`CancellationToken`, which got this repo compiling again. It **added the parameter and used it in
almost nothing**: 24 of the 41 files contain `await`, and exactly 1 passes the token onward.

So the agent run loop passes a token believing tool execution can be cancelled, and for practically
every tool it cannot. These tools run git commands, touch the database, read and write files and
call out to services — a long one cannot currently be interrupted.

⚠ This is the same failure the framework declined to build into the base class. Birko `TASK-483`
rejected a virtual overload precisely because it would let a tool *"accept a `CancellationToken` and
silently discard it"*. TASK-078 has left 40 tools doing exactly that — the difference being that the
omission is visible per file and fixable, rather than hidden in an abstract. That difference is only
worth something if someone actually fixes it.

## Acceptance criteria

- [ ] Every awaited call inside a tool body that accepts a `CancellationToken` is passed the one the
      method received
- [ ] Long-running loops inside tool bodies check `cancellationToken.ThrowIfCancellationRequested()`
      at a sensible interval
- [ ] A tool that genuinely has nothing cancellable says so in a one-line comment, so "unused" is a
      recorded decision rather than an oversight — the distinction TASK-078 could not make in bulk
- [ ] **Proven, not assumed:** at least one long-running tool is shown to actually stop when its
      token is cancelled. A signature that accepts a token proves nothing
- [ ] `DraCode.slnx` builds and `DraCode.KoboldLair.Tests` stays green

## Out of scope

- Changing any tool's behaviour beyond cancellation.
- The framework's `Tool` contract — settled in Birko `TASK-483`.

## Human test plan

- [ ] Start a long-running tool through the agent loop, cancel it, and confirm it stops rather than
      running to completion. Expected before this task: it runs to completion.
