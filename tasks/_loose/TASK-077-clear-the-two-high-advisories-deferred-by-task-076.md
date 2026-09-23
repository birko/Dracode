---
id: TASK-077
parent: null
feature: null
# status: todo | in-progress | review (code done, sign-off pending) | blocked | done | cancelled
status: done
priority: P2
assignee: ai
created: 2026-09-23
depends-on: []
blocks: []
related: [TASK-076]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Clear the two High advisories TASK-076 deferred

## Context

[[TASK-076]] fixed the Birko-owned version rows and explicitly deferred two advisories to their own
id: *"`Microsoft.OpenApi` 2.0.0 (High) in `.Server` and `.Tests`, `MessagePack` 2.5.192 (High) in
`.AppHost`… They want their own id."* This is that id.

Both were re-measured 2026-09-23 and both were still live. They also appear in Birko's own
whole-tree sweep (`Birko.Framework` TASK-475), which is where the remedy was probed.

## ⚠ Both were called a "direct declaration". Neither is.

TASK-076 says the OpenAPI one is *"a direct declaration rather than an Aspire transitive"*, and
Birko's TASK-475 repeated it, adding *"the cheapest of the three to fix and the only one nobody has
an excuse for."* **No csproj or props in this repo declares `Microsoft.OpenApi`.** It resolves
through `Microsoft.AspNetCore.OpenApi`, exactly as MessagePack resolves through Aspire:

```
Microsoft.AspNetCore.OpenApi 10.0.0 -> Microsoft.OpenApi 2.0.0
Aspire.Hosting.AppHost 13.1.2 -> Aspire.Hosting -> StreamJsonRpc 2.22.23 -> MessagePack 2.5.192
```

The error was load-bearing in both trees: it ranked this row as the cheap one, which is how a remedy
gets planned without being priced. Corrected in both.

## What changed

Floors found by bisecting throwaway probe projects, not read off version numbers:

| package | floor that clears | at the floor | one below |
|---|---|---|---|
| `Aspire.AppHost.Sdk` | **13.4.5** | MessagePack 2.5.302 | 13.4.4 → 2.5.192, High |
| `Microsoft.AspNetCore.OpenApi` | **10.0.11** | Microsoft.OpenApi 2.12.0 | 10.0.10 → 2.0.0, High |

Two lines:

- `DraCode.AppHost/DraCode.AppHost.csproj:1` — SDK `13.1.2` → `13.4.5`. This repo pins Aspire through
  the **SDK attribute** and carries no `PackageReference` for it at all, unlike Symbio.
- `DraCode.KoboldLair.Server/DraCode.KoboldLair.Server.csproj:25` — `10.0.0` → `10.0.11`.
  `.Tests` inherits it by project reference.

Verified: `DraCode.AppHost`, `DraCode.KoboldLair.Server` and `DraCode.KoboldLair.Tests` all report
**no vulnerable packages**.

## ⚠ Not compile-verified, because `main` does not build — and the cause is upstream

`dotnet build` fails with **82 errors**, and it fails **identically with this change stashed**, so it
is not caused here. The erroring files are unmodified in the working tree, so this is HEAD.

Every error is one of two, 41 of each: `CS0534` + `CS0115` across **41 tool classes** deriving from
`Birko.AI.Tools.Tool`. Birko commit `6b4e374b` (**2026-07-09**) changed the abstract signature to

```csharp
public abstract Task<string> ExecuteAsync(string workingDirectory,
    Dictionary<string, object> input, CancellationToken cancellationToken = default);
```

The default value does not help: an override of an abstract method must match the full signature.
**This repo has not compiled since 2026-07-09.** Filed as its own work — see Out of scope.

## Out of scope

- **The 41 broken tool classes.** Substantial, and it needs a decision upstream first: whether
  `Tool.ExecuteAsync` should have gained the token as a new **virtual overload** rather than by
  changing the abstract signature, which would have kept every consumer compiling. A shared project
  has no package identity, so a breaking change in one reaches consumers with **no version signal at
  all** — nothing to bump, nothing to warn, which is why this sat unnoticed for over two months.
- **`Microsoft.Data.Sqlite` in `DraCode.KoboldLair`**, still unused — TASK-076 left it, and so does this.

## Human test plan

N/A — the advisory clearance is machine-checkable and was checked. A build test is impossible until
the `Tool` breakage above is fixed, and is that task's business.
