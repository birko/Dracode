---
id: TASK-078
parent: null
feature: null
# status: todo | in-progress | review (code done, sign-off pending) | blocked | done | cancelled
status: done
priority: P1
assignee: ai
created: 2026-09-23
depends-on: []
blocks: []
related: [TASK-077]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Migrate the 41 tool overrides — this repo has not compiled since 2026-07-09

## Context

Birko commit `6b4e374b` (2026-07-09) added a `CancellationToken` to the **abstract**
`Birko.AI.Tools.Tool.ExecuteAsync`. An override must match the full signature, so the default value
helps nobody and every implementer had to change. **This repo's 41 tool classes did not**, and it has
not built since — `41 × CS0534` + `41 × CS0115`, 82 errors, for 2½ months.

Found while clearing an unrelated advisory ([[TASK-077]]). The framework has since recorded the
decision to **keep** the abstract signature — Birko `TASK-483`, and the reasoning matters here: a
virtual overload forwarding to the old two-argument form would have compiled everywhere and
**discarded the token**, so tools would accept a cancellation they could never honour. A compile
error naming all 41 files is the cheaper failure.

The migration is written up in Birko's `CHANGELOG.md` under `2026-07-09`, and **BardStudio had
already done it** without difficulty.

## What changed

One mechanical edit, 41 overrides across 40 files (one file holds two), covering all five spellings
the repo used (`input`, `arguments`, `parameters`, sync and `async`):

```csharp
- public override Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
+ public override Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input,
+     CancellationToken cancellationToken = default)
```

No `using` was needed — `ImplicitUsings` is enabled and covers `System.Threading`.

## Verified

- `DraCode.KoboldLair`: **82 errors → 0**
- `DraCode.slnx` whole solution: **build succeeded, 0 errors, 2 warnings**
- `DraCode.KoboldLair.Tests`: **172 passed, 0 failed** — the first green run since July

## ⚠ The token is accepted everywhere and used almost nowhere

This migration makes the code **compile**; it does not make cancellation **work**. All 41 tools now
take a `CancellationToken` and ignore it. Measured: **24 of the 41 files contain `await`**, and
exactly **1** passes the token to anything it calls.

That is a legitimate first step — Birko's changelog says so explicitly — but it is not the end state,
and it is worth being blunt about: a tool that accepts a token and discards it is the same shape the
framework rejected when it refused the virtual-overload option. The difference is only that here the
omission is **visible in each file**, where someone can fix it, rather than hidden in a base class.

**Scheduled as [[TASK-079]]**, not left as a paragraph.

## Out of scope

- **Threading the token into awaited calls** — [[TASK-079]].
- **The two build warnings.** Pre-existing, unrelated to this change.

## Human test plan

N/A — build and test results above are the verification.
