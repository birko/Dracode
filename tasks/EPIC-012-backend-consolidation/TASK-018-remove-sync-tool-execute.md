---
id: TASK-018
parent: EPIC-012
status: done
priority: P2
assignee: ai
created: 2026-05-29
closed: 2026-05-29
depends-on: []
blocks: []
pr: "DraCode 1b45c42 · Birko.AI 2e7908f · Birko.AI.Contracts 522ef7f (all local, awaiting push)"
github-issue: null
jira-key: null
---

# Remove sync `Tool.Execute()` overloads — keep only `ExecuteAsync`

## Context

The "Blocking sync-over-async" item in `CLAUDE.md` is marked **resolved** because the live call path now uses `ExecuteAsync` (added as a `virtual` on the `Tool` base class in commit `402dbc1`). However the underlying `abstract string Execute(...)` method is still on the base class, so all 23 `Tool` subclasses in `DraCode.KoboldLair/Agents/Tools/` still override it. `ViewCostReportTool.cs:53-58` shows the anti-pattern surviving in the sync override:

```csharp
return action.ToLowerInvariant() switch
{
    "summary" => ExecuteSummaryAsync().GetAwaiter().GetResult(),
    "daily" => ExecuteDailyAsync().GetAwaiter().GetResult(),
    "project" => ExecuteProjectAsync(input).GetAwaiter().GetResult(),
    "budget" => ExecuteBudgetAsync().GetAwaiter().GetResult(),
    ...
};
```

Any future contributor calling the sync `Execute()` overload reintroduces thread-pool starvation. The dead-code path is a foot-gun: remove it.

## Acceptance criteria

> Corrections from `/tasks plan` (2026-05-29): Tool base class is in **`Birko.AI.Contracts\Tools\Tool.cs`** (not `Birko.AI.Agents`); current `Execute` modifier is **`virtual`** (relaxed from `abstract` in commit `402dbc1`); sync-only count is **22 in DraCode + 9 in Birko.AI**, not 4. Scope expanded during grilling to a consistent fix across 3 repos (Q1) — see Implementation plan §1.

**Base class (`C:\Source\Birko.AI.Contracts\Tools\Tool.cs`):**
- [x] Remove `public virtual string Execute(string workingDirectory, Dictionary<string, object> input)` (currently `virtual`, throws `NotImplementedException`)
- [x] Promote `public virtual Task<string> ExecuteAsync(...)` to `public abstract Task<string> ExecuteAsync(...)` (remove body)
- [x] Update XML doc comment on `ExecuteAsync` — drop the "backwards compatibility with sync Execute" reference

**DraCode (`DraCode.KoboldLair\Agents\Tools\`, 23 files):**
- [x] All 23 sync `override string Execute(...)` methods deleted across:
  - AgentConfigurationTool, AgentStatusTool, BatchTaskTool, CancelProjectTool, CreateImplementationPlanTool, DeleteProjectTool, NotificationsTool, PauseProjectTool, ProjectApprovalTool, ProjectProgressTool, ResumeProjectTool, RetryAnalysisTool, RetryFailedTaskTool, RetryVerificationTool, SelectAgentTool, SetTaskPriorityTool, SkipVerificationTool, SuspendProjectTool, UserSettingsTool, ViewAnalysisTool, ViewCostReportTool, ViewVerificationReportTool, ViewWorkspaceTool
- [x] 22 sync-only tools gain `Task<string> ExecuteAsync(...)` (body wrapped in `Task.FromResult(...)` unless an async helper is obviously available — see Implementation plan §1, Q5=B opportunistic async)
- [x] `ViewCostReportTool`: sync wrapper deleted, existing `async Task<string> ExecuteAsync` retained — clears the 4 `.GetAwaiter().GetResult()` calls at lines 55–58
- [x] `Kobold.cs:1344` — caller migrated: `tool.Execute(...)` → `await tool.ExecuteAsync(...)`
- [x] Stray editor backup `ViewCostReportTool.cs.tmp` deleted

**Birko.AI (`C:\Source\Birko.AI\Tools\`, 9 files):**
- [x] All 9 sync `override string Execute(...)` methods deleted across:
  - AppendToFileTool, AskUserTool, DisplayTextTool, EditFileTool, ListFilesTool, ReadFileTool, RunCommandTool, SearchCodeTool, WriteFileTool
- [x] All 9 tools gain `Task<string> ExecuteAsync(...)` (`Task.FromResult` wrap unless opportunistic async wins)
- [x] `AskUserTool.cs:55,60` — internal `Task.WhenAny(...).GetAwaiter().GetResult()` and `promptTask.GetAwaiter().GetResult()` converted to proper `await`

**Verification:**
- [x] Grep `override\s+string\s+Execute\(` under `DraCode.KoboldLair\Agents\Tools\` → 0 matches
- [x] Grep `override\s+string\s+Execute\(` under `C:\Source\Birko.AI\Tools\` → 0 matches
- [x] Grep `\.GetAwaiter\(\)\.GetResult\(\)` under `DraCode.KoboldLair\Agents\Tools\` → 0 matches
- [x] Grep `\.GetAwaiter\(\)\.GetResult\(\)` under `C:\Source\Birko.AI\Tools\` → 0 matches
- [x] `dotnet build ./DraCode.slnx` clean
- [x] Cross-repo `dotnet build` green for `Symbio`, `BardStudio`, `Birko.Framework` after Contracts commit (audit showed zero `Tool` subclasses in those repos — sanity check only)
- [x] Existing tool tests pass — current audit shows zero `*Tool*Tests` files in DraCode.KoboldLair.Tests, so this criterion is N/A; tick when commit lands

**Docs:**
- [x] `CLAUDE.md` "Known Issues" section: "Blocking sync-over-async" entry updated — promoted from *partially resolved* (live path async, dead sync path remains) to *fully resolved* (sync path eliminated)
- [x] `CLAUDE.md` "Recent Updates" section: add a dated entry for this change, matching repo convention

## Out of scope

- Other `.GetAwaiter().GetResult()` usages outside `Agents/Tools/` (see `Drake.cs`, `Wyvern.cs`, `ProjectService.cs`, `ProjectRepository.cs`, etc. — separate cleanup, not blocking)
- Birko.AI base class changes beyond `Tool` itself
- Any behavioural changes to individual tools

## Risks

- The `Tool` base class lives in Birko.AI (external dependency). If Birko.AI cannot be modified in the same PR, fall back to: keep `Execute` as a non-virtual `[Obsolete(error: true)]` shim that throws `NotSupportedException`, override removed from all subclasses. Decide during planning by checking `Birko.AI` aggregator wiring (`BIRKO_SRC` env var convention from `Directory.Build.props`).
- Some tool callers may still call the sync method via reflection (unlikely — grep for `.Execute(` invocations). Verify before deletion.

## Implementation plan

### Decisions locked during `/tasks plan` grilling (2026-05-29)

| # | Question | Decision |
|---|---|---|
| Q1 | Scope of the fix | **A** — Consistent fix across all 3 repos (DraCode + Birko.AI + Birko.AI.Contracts); do not use the `[Obsolete]`-shim fallback |
| Q2 | AC text corrections | **A** — Edit AC text to fix three stale facts (location, modifier, count) — already applied above |
| Q3 | Sequencing | **C** — Top-down: DraCode → Birko.AI → Contracts last (each repo independently mergeable in this order). **Direct commit to `main`, no PRs.** |
| Q4 | Commit granularity | **A** — One commit per repo (3 commits total). No intermediate bisect-safe sub-commits within a repo. |
| Q5 | Helper async-ification | **B** — Opportunistic: switch helpers to `await` when underlying service has a clean async variant; also clear *all* `.GetAwaiter().GetResult()` in any migrated file (not just AC-scoped ones). |
| Q6a | Cross-repo build check | Yes — `dotnet build` Symbio, BardStudio, Birko.Framework before pushing the Contracts commit |
| Q6b | `Tool.cs` doc comments | Rewrite `ExecuteAsync` XML doc; drop "backwards compatibility" reference |
| Q6c | Tests AC | Leave wording, tick N/A (zero matching test files exist) |
| Q6d | `.tmp` cleanup | Delete `ViewCostReportTool.cs.tmp` in DraCode commit |

### Pre-flight findings (resolved during planning)

- **Birko.AI editability:** `Tool` base class lives at `C:\Source\Birko.AI.Contracts\Tools\Tool.cs`. Wired via shared-project import (`..\..\Birko.AI.Contracts\Birko.AI.Contracts.projitems`) in `DraCode\DraCode.Birko\DraCode.Birko.csproj`. `BIRKO_SRC` env var not set; resolution is purely relative. **In-tree, fully editable — `[Obsolete]`-shim fallback not needed.**
- **Reflection callers of `Execute`:** zero hits across `C:\Source\DraCode` and `C:\Source\Birko.AI*` (Grep for `nameof(Execute)`, dynamic dispatch patterns). Safe to delete.
- **Cross-repo `Tool` subclass audit:** 23 in DraCode, 9 in Birko.AI, **0 in Symbio, 0 in BardStudio, 0 in Birko.Framework**. BardStudio's only `Tool` users (`DJTools.cs`) override `ExecuteAsync` already.
- **`.GetAwaiter().GetResult()` hits (full inventory):** 4 in `DraCode.KoboldLair\Agents\Tools\ViewCostReportTool.cs:55-58`; 2 in `Birko.AI\Tools\AskUserTool.cs:55,60`. No `.Result` shortcuts anywhere in Tools.
- **`Tool.cs` current shape:** `Execute` is `virtual` (not `abstract` as the original TASK said) and throws `NotImplementedException`; `ExecuteAsync` is `virtual` and delegates to `Execute`. Comment claims "backwards compatibility" — to be rewritten.

### §1 Scope (consistent fix, 3 repos)

| Repo | Files touched | What |
|---|---|---|
| `birko/DraCode` | 26 | 22 sync-only tools (boundary swap), `ViewCostReportTool` (delete sync wrapper), `Kobold.cs:1344` (caller fix), `CLAUDE.md` (docs), `ViewCostReportTool.cs.tmp` (delete stray) |
| `birko/Birko.AI` | 9 | All 9 built-in tool subclasses migrated; `AskUserTool` also gets internal `Task.WhenAny` → `await` conversion |
| `birko/Birko.AI.Contracts` | 1 | `Tool.cs` — remove `Execute`, make `ExecuteAsync` abstract, rewrite XML doc |

### §2 Three commits, top-down (each repo independently green at every step)

**Commit 1 — `birko/DraCode` on `main`**

Single commit containing all DraCode-side changes:
1. `DraCode.KoboldLair\Models\Agents\Kobold.cs:1344` — `tool.Execute(Agent.Options.WorkingDirectory, block.Input ?? new Dictionary<string, object>())` → `await tool.ExecuteAsync(...)`. Method is already `async Task RunWithStepDetectionAsync`.
2. **22 sync-only tools** in `DraCode.KoboldLair\Agents\Tools\` (list in AC) — for each:
   - Rename `public override string Execute(...)` → `public override Task<string> ExecuteAsync(...)`
   - Wrap return expression in `Task.FromResult(...)`
   - Per Q5=B: if the helper method called inside has an obvious async sibling (e.g. `await _repo.FindAsync(...)` available instead of `_repo.Find(...)`), switch to `await` and propagate `async` up. Otherwise leave helpers sync.
3. `ViewCostReportTool.cs` — delete lines 49–62 (the sync wrapper). Keep the existing `async Task<string> ExecuteAsync` at line 64. Clears all 4 `.GetAwaiter().GetResult()` hits in DraCode tools.
4. Delete `DraCode.KoboldLair\Agents\Tools\ViewCostReportTool.cs.tmp`.
5. `CLAUDE.md` line 699 — replace "Tool base class added `virtual Task<string> ExecuteAsync(...)`; DragonService callbacks converted to `Func<..., Task>`; zero `.GetAwaiter().GetResult()` calls remain in DragonService." with "`Tool` is `ExecuteAsync`-only; sync `Execute` removed from base class (TASK-018, 2026-05-29). DragonService callbacks are `Func<..., Task>`; zero `.GetAwaiter().GetResult()` in either DragonService or Agents/Tools."
6. `CLAUDE.md` Recent Updates section — add `### Tool Sync Path Eliminated (2026-05-29)` matching repo convention.
7. **Local verify:** `dotnet build ./DraCode.slnx` clean. Grep `override\s+string\s+Execute\(` and `\.GetAwaiter\(\)\.GetResult\(\)` under `DraCode.KoboldLair\Agents\Tools\` both return 0.
8. **Push:** `git push origin main`. Repo is green — base class `Tool` still has `virtual Execute`, no overrides left in DraCode, `ExecuteAsync` wins vtable dispatch.

**Commit 2 — `birko/Birko.AI` on `main` (parallelizable with #1)**

Single commit:
1. **9 tools** in `Birko.AI\Tools\`: AppendToFileTool, AskUserTool, DisplayTextTool, EditFileTool, ListFilesTool, ReadFileTool, RunCommandTool, SearchCodeTool, WriteFileTool — same `string Execute(...)` → `Task<string> ExecuteAsync(...)` + `Task.FromResult` wrap pattern.
2. `AskUserTool.cs:55,60` — under Q5=B, the existing `Task.WhenAny(promptTask, timeoutTask).GetAwaiter().GetResult()` + `promptTask.GetAwaiter().GetResult()` pattern is rewritten as proper `await Task.WhenAny(...)` then `await promptTask`. This is non-trivial because the surrounding method is currently sync (it's inside the old `Execute`); after the boundary swap, the method becomes `async Task<string> ExecuteAsync` and the `await`s are legal. Outcome: clears both Birko.AI `.GetAwaiter().GetResult()` hits.
3. **Local verify:** `dotnet build` of Birko.AI's solution clean. Grep `override\s+string\s+Execute\(` and `\.GetAwaiter\(\)\.GetResult\(\)` under `C:\Source\Birko.AI\Tools\` both return 0.
4. **Push:** `git push origin main`. Repo is green — base class still has `virtual Execute`, Birko.AI just stops overriding it.

**Pre-Commit 3 cross-repo verification**

Before touching `Birko.AI.Contracts`:
- `dotnet build` for Symbio's main solution — green (zero `Tool` subclasses)
- `dotnet build` for BardStudio's main solution — green (DJTools already async)
- `dotnet build` for Birko.Framework's main solution — green (aggregator only)
- Confirm `git status` clean on all three (we haven't edited them; this just confirms nothing's bitrotting against the older Contracts API)

**Commit 3 — `birko/Birko.AI.Contracts` on `main` (the irrevocable step)**

Single commit, `Birko.AI.Contracts\Tools\Tool.cs`:
1. Delete the `virtual string Execute(...)` method (lines 21–28 in current HEAD).
2. Change `public virtual Task<string> ExecuteAsync(...)` to `public abstract Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input);` (no body).
3. Rewrite XML doc:
   ```csharp
   /// <summary>
   /// Tool execution entry point. Performs I/O, database, or network operations as needed.
   /// </summary>
   ```
4. **Local verify:** build each of DraCode, Birko.AI, Symbio, BardStudio, Birko.Framework against the now-updated Contracts shared project. All five must be green.
5. **Push:** `git push origin main`. The API is locked down — sync `Execute` is gone from the contract; no future contributor can reintroduce thread-pool starvation through the `Tool` boundary.

### §3 Verification checkpoint table

| Stage | DraCode `override string Execute` | Birko.AI `override string Execute` | DraCode `.GetAwaiter().GetResult()` | Birko.AI `.GetAwaiter().GetResult()` | All 5 builds green |
|---|---|---|---|---|---|
| Start (HEAD) | 23 | 9 | 4 | 2 | yes |
| After Commit 1 | **0** | 9 | **0** | 2 | yes (DraCode); Birko.AI still has overrides → still uses virtual Execute → fine |
| After Commit 2 | 0 | **0** | 0 | **0** | yes (DraCode + Birko.AI); Symbio/BardStudio/Birko.Framework unchanged, still build against current Contracts |
| After Commit 3 | 0 | 0 | 0 | 0 | yes (all 5) — `Tool.ExecuteAsync` is now `abstract` and every subclass provides it |

### §4 Risk notes (post-grilling)

All risks listed in the TASK body are now resolved by the planning findings (see Pre-flight). Residual risks:

1. **Per-tool opportunistic async-ification (Q5=B) is per-file judgment.** During execution, if a helper has an unclear async path (e.g. a service exposes `FooAsync` but its semantics differ subtly from `Foo`), default to the safer wrap-only behaviour — do NOT chase the async version when there's any doubt. The point of B over A is to clear obvious wins, not to redesign 31 files.
2. **`AskUserTool` async conversion is the trickiest single edit.** The `Task.WhenAny` + `GetAwaiter().GetResult()` pattern was specifically designed to time-out a sync method. The async rewrite is mechanically `await Task.WhenAny(promptTask, timeoutTask); return await promptTask;` — the surrounding control flow needs review to confirm the timeout semantics are preserved.
3. **Three-repo direct-commit means no PR review.** Each repo's local `dotnet build` must be the gate. Push in the exact order DraCode → Birko.AI → Contracts; do not push Contracts until both upstream commits are confirmed in `origin/main`.
4. **Cross-repo bitrot risk is low but non-zero.** If anyone is concurrently editing `Birko.AI.Contracts\Tools\Tool.cs` (unlikely — solo workspace), a merge conflict on Commit 3 would surface there. `git pull` immediately before Commit 3 mitigates.

### §5 Critical files (full paths)

- `C:\Source\Birko.AI.Contracts\Tools\Tool.cs` — base class API change (load-bearing)
- `C:\Source\DraCode\DraCode.KoboldLair\Models\Agents\Kobold.cs:1344` — second sync caller (TASK missed this)
- `C:\Source\DraCode\DraCode.KoboldLair\Agents\Tools\ViewCostReportTool.cs` — dual-override; demonstrates the deletion pattern; clears 4 `GetAwaiter().GetResult()` hits
- `C:\Source\DraCode\DraCode.KoboldLair\Agents\Tools\ViewCostReportTool.cs.tmp` — delete
- `C:\Source\Birko.AI\Tools\AskUserTool.cs` — the only file requiring real async refactoring (not just `Task.FromResult` wrap)
- `C:\Source\Birko.AI\Tools\` × 8 other files — mechanical `Task.FromResult` migration
- `C:\Source\DraCode\DraCode.KoboldLair\Agents\Tools\` × 22 sync-only tools — mechanical `Task.FromResult` migration
- `C:\Source\DraCode\CLAUDE.md` — line 699 + Recent Updates entry

### §6 Execution kickoff

Start with: `/tasks pick TASK-018` (will set status `in-progress` and begin Commit 1 work).

### 0. Birko.AI editability finding (must come first)

- `Tool` base class location: `C:\Source\Birko.AI.Contracts\Tools\Tool.cs` (namespace `Birko.AI.Tools`). The TASK's "in `Birko.AI.Agents`" hint is wrong — it lives in `Birko.AI.Contracts`.
- Wiring: `DraCode\DraCode.Birko\DraCode.Birko.csproj` imports `..\..\Birko.AI.Contracts\Birko.AI.Contracts.projitems` as a shared project (line 16). `C:\Source\Birko.AI.Contracts\` is a sibling directory of `C:\Source\DraCode\`, both directly editable in this workspace.
- `BIRKO_SRC` env var is **not set**; `Directory.Build.props` (root) only sets `PreferSlnx=true`. Resolution is purely via the relative `..\..\` import path. No external NuGet, no submodule.
- **Verdict: Branch A (clean edit) is feasible.** `Tool.cs` is in-tree and edits propagate to any consumer of the shared project on the next build. The `[Obsolete(error: true)]` fallback is not needed for editability reasons.
- Current state of `Tool.cs`: both `Execute` and `ExecuteAsync` are currently `virtual` (the TASK says `Execute` is `abstract`, also stale — it was relaxed in commit `402dbc1` so the new `virtual ExecuteAsync` default could delegate to it). If a subclass overrides neither, `Execute` throws `NotImplementedException`.

### 1. Cross-cutting impact survey

| Location | Files | `override string Execute` | `override Task<string> ExecuteAsync` | Action |
|---|---|---|---|---|
| `DraCode\DraCode.KoboldLair\Agents\Tools\` | 40 (+1 `.tmp` stub, +1 `csproj`) | 23 | 19 | Convert all sync overrides; delete sync wrappers where dual |
| `Birko.AI\Tools\` | 9 (`AskUser`, `DisplayText`, `RunCommand`, `SearchCode`, `AppendToFile`, `EditFile`, `WriteFile`, `ReadFile`, `ListFiles`) | 9 | 0 | Convert ALL nine — same project as base class |
| `Birko.AI.Agents\` | 0 `Tool` subclasses | 0 | 0 | None |
| `BardStudio\src\BardStudio.AI\Tools\DJTools.cs` | 1 file, 3 classes | 0 | 3 | None (already async-only). NOT compiled by `DraCode.slnx`. |

Live tool-invocation sites:
- `Birko.AI\Agents\Agent.cs:317` — already `await tool.ExecuteAsync(...)`. No change.
- `DraCode\DraCode.KoboldLair\Models\Agents\Kobold.cs:1344` — still `tool.Execute(...)` inside `async Task RunWithStepDetectionAsync`. **Must be migrated to `await tool.ExecuteAsync(...)` in this PR.**

Reflection / `nameof` lookups against `Execute`: zero hits across `C:\Source\DraCode` and `C:\Source\Birko.AI*`. Safe to delete.

Tests against `Tool.Execute`: zero. The "adapt tests" acceptance criterion is a no-op for this repo; build still has to be green.

### 2. Exact tool-by-tool migration list

**2a. Sync-only tools (22 files) — body wrapped in `Task.FromResult(...)`**
All in `DraCode.KoboldLair\Agents\Tools\`:
AgentConfigurationTool, AgentStatusTool, BatchTaskTool, CancelProjectTool, CreateImplementationPlanTool, DeleteProjectTool, NotificationsTool, PauseProjectTool, ProjectApprovalTool, ProjectProgressTool, ResumeProjectTool, RetryAnalysisTool, RetryFailedTaskTool, RetryVerificationTool, SelectAgentTool, SetTaskPriorityTool, SkipVerificationTool, SuspendProjectTool, UserSettingsTool, ViewAnalysisTool, ViewVerificationReportTool, ViewWorkspaceTool.

Pattern: rename `public override string Execute(...)` → `public override Task<string> ExecuteAsync(...)`, wrap the existing return expression in `Task.FromResult(...)`. No `async` needed (no awaits). Existing helper method signatures (e.g. `ExecuteOverview`, `ExecuteAll` returning `string`) stay sync — keeps the diff small.

**2b. Dual-override tool (1 file) — delete sync wrapper, keep async**
`ViewCostReportTool.cs`: delete lines 49–62 (the `override string Execute(...)` with four `.GetAwaiter().GetResult()` calls); the existing `override async Task<string> ExecuteAsync(...)` at line 64 already implements the correct behaviour.

**2c. Birko.AI shared-project tools (9 files) — same `Task.FromResult` wrap as 2a**
All in `Birko.AI\Tools\`: AppendToFileTool, AskUserTool, DisplayTextTool, EditFileTool, ListFilesTool, ReadFileTool, RunCommandTool, SearchCodeTool, WriteFileTool.

These are unavoidable: if `Execute` is removed from `Tool`, every `override string Execute` is a compile error regardless of which downstream solution is built. The TASK's "Birko.AI base class changes beyond `Tool` itself" out-of-scope clause must be read as "don't change unrelated Birko.AI APIs" — touching tool subclasses to satisfy the new contract is mechanical and unavoidable.

**2d. Caller migration (1 file)**
`Kobold.cs:1344`: change `tool.Execute(Agent.Options.WorkingDirectory, block.Input ?? new Dictionary<string, object>())` → `await tool.ExecuteAsync(Agent.Options.WorkingDirectory, block.Input ?? new Dictionary<string, object>())`. Surrounding method (`RunWithStepDetectionAsync`) is already async.

**2e. Base class (1 file)**
`Birko.AI.Contracts\Tools\Tool.cs`: delete current `virtual Execute` (lines 21–28) and the `Execute(...)` call inside the default `ExecuteAsync` body, then promote `ExecuteAsync` from `virtual` to `abstract` (removing the body).

**2f. Docs (1 file)**
`DraCode\CLAUDE.md`: line 699 — update the bullet to drop the "Tool base class added `virtual Task<string> ExecuteAsync(...)`" wording and replace with "`Tool` is `ExecuteAsync`-only; sync `Execute` removed". Optionally also note in the dated Recent Updates section.

### 3. Order of operations (bisect-safe — every step compiles)

Each step is a single commit boundary that keeps `dotnet build ./DraCode.slnx` green.

1. **Commit 1 — Migrate caller.** Edit `Kobold.cs:1344` to `await tool.ExecuteAsync(...)`. Build green (no API change yet). Isolates the consumer-side fix from the API-shape change.
2. **Commit 2 — Migrate all 22 DraCode sync-only tools to `ExecuteAsync` via `Task.FromResult`.** Leave the old `override string Execute` body unchanged. Add the new `override Task<string> ExecuteAsync(...)` alongside — both compile because both base methods are still virtual. Build green; `ExecuteAsync` wins now that all sites use it.
3. **Commit 3 — Delete the 23 sync `override string Execute` methods in DraCode** (22 from step 2 + `ViewCostReportTool`'s wrapper). Verify grep `override\s+string\s+Execute\(` under `DraCode.KoboldLair\Agents\Tools` returns zero. Verify grep `\.GetAwaiter\(\)\.GetResult\(\)` under same path returns zero. Build green (base class still has `virtual Execute` so existing Birko.AI tool overrides still compile).
4. **Commit 4 — Migrate the 9 Birko.AI built-in tools** (`Birko.AI\Tools\*.cs`) to `ExecuteAsync` via `Task.FromResult`, then delete their `override string Execute`. Build green.
5. **Commit 5 — Tighten the base class.** Edit `Birko.AI.Contracts\Tools\Tool.cs`: remove `Execute`, change `ExecuteAsync` from `virtual` to `abstract`. Build green — all subclasses now provide `ExecuteAsync`. This is the moment a future foot-gunner can no longer reintroduce the sync path.
6. **Commit 6 — Docs.** Update `CLAUDE.md` line 699 and add a Recent Updates entry. Mark `TASK-018` status `done`, fill in PR link.

Optional housekeeping (out of scope but cheap): delete the stray `DraCode.KoboldLair\Agents\Tools\ViewCostReportTool.cs.tmp` editor backup.

### 4. Verifiable checkpoints

| After commit | `dotnet build ./DraCode.slnx` | Grep `override\s+string\s+Execute\(` in DraCode tools | Grep `\.GetAwaiter\(\)\.GetResult\(\)` in DraCode tools | Notes |
|---|---|---|---|---|
| 1 | clean | 23 | 4 | Kobold-side fix isolated |
| 2 | clean | 23 | 4 | New `ExecuteAsync` added alongside |
| 3 | clean | **0** | **0** | DraCode tools fully migrated |
| 4 | clean | 0 | 0 | Birko.AI tools migrated |
| 5 | clean | 0 | 0 | Base class locked down; `Tool` API is async-only |
| 6 | clean | 0 | 0 | Docs and TASK closed |

After commit 5, also run `dotnet build` for any sibling solution that consumes `Birko.AI.Contracts`. BardStudio's `DJTools.cs` already overrides `ExecuteAsync`, so it should be unaffected.

### 5. Risks and tradeoffs beyond the TASK

1. **Cross-repo blast radius (the real cost).** `Birko.AI.Contracts` is shared with BardStudio (and likely Symbio, which also imports `Birko.AI.Tools`). Pre-merge grep across `C:\Source\*` for `override\s+string\s+Execute\(`: BardStudio's only hits override `ExecuteAsync` already (safe); Symbio has no `Tool` subclasses. Low residual risk; do one final grep before merging.
2. **`Tool` base lives in `Birko.AI.Contracts`, not `Birko.AI.Agents`.** The TASK guessed wrong. Other Birko.* docs / future TASKs that reference the location will be stale. Worthwhile follow-up note.
3. **Stale `Execute` count in the TASK.** The "4 tools previously had only sync `Execute()`" figure is off by ~5x. Architectural shape is unchanged but PR review burden is 22 sync-only files plus 9 Birko.AI files = 31 mechanical conversions, not 4.
4. **Helper-method sync chains.** Many tools have private sync helpers (`ExecuteOverview`, `ExecuteAll`, `FindProject`, `LoadAreaTasks`, etc.). The plan deliberately leaves those sync — `Task.FromResult` wrapping is at the public `ExecuteAsync` boundary only. True async I/O is a separate refactor; doing it here would balloon the PR diff and risk behaviour changes (TASK out-of-scope: "Any behavioural changes to individual tools").
5. **`Kobold.cs` second consumer.** If we removed `Execute` from `Tool` first (e.g. did §3 step 5 before step 1), the build would fail on `Kobold.cs:1344` with a cryptic missing-method error rather than the obvious "tool needs ExecuteAsync" error. The §3 ordering — caller first, then leaf tools, then base class — yields the clearest compile errors at each broken-state boundary.
6. **`async` keyword vs `Task.FromResult`.** For sync-only tools, `Task.FromResult` is cheaper than `async`-marking the method (no state machine). Keep `async` only for the dual-override case (ViewCostReportTool already uses `async`). Style-consistent with `Tool.cs`'s existing default body.
7. **Stray `.tmp` file.** `ViewCostReportTool.cs.tmp` exists in the Tools directory but isn't in csproj globs. Harmless; could be cleaned up.

### Critical files for implementation

- `C:\Source\Birko.AI.Contracts\Tools\Tool.cs` — base class API change (the load-bearing edit)
- `C:\Source\DraCode\DraCode.KoboldLair\Models\Agents\Kobold.cs` — second sync caller at line 1344 (TASK missed this; PR will not build without fixing it)
- `C:\Source\DraCode\DraCode.KoboldLair\Agents\Tools\ViewCostReportTool.cs` — only dual-override tool; demonstrates the deletion pattern and clears the 4 `.GetAwaiter().GetResult()` hits
- `C:\Source\Birko.AI\Tools\` (directory of 9 files) — the hidden scope expansion; cannot ship the base-class change without these
- `C:\Source\DraCode\CLAUDE.md` — line 699 "Blocking sync-over-async" entry promotion (acceptance criterion)
