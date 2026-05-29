---
id: TASK-018
parent: EPIC-012
status: todo
priority: P2
assignee: ai
created: 2026-05-29
depends-on: []
blocks: []
pr: null
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

- [ ] `Tool` base class (in `Birko.AI.Agents` — verify exact location during planning):
  - [ ] Change `public abstract string Execute(string workingDirectory, Dictionary<string, object> input)` to be removed
  - [ ] Change `public virtual Task<string> ExecuteAsync(...)` to `public abstract Task<string> ExecuteAsync(...)`
- [ ] All 23 sync `override string Execute(...)` methods deleted across `DraCode.KoboldLair/Agents/Tools/`:
  - AgentConfigurationTool, AgentStatusTool, BatchTaskTool, CancelProjectTool, CreateImplementationPlanTool, DeleteProjectTool, NotificationsTool, PauseProjectTool, ProjectApprovalTool, ProjectProgressTool, ResumeProjectTool, RetryAnalysisTool, RetryFailedTaskTool, RetryVerificationTool, SelectAgentTool, SetTaskPriorityTool, SkipVerificationTool, SuspendProjectTool, UserSettingsTool, ViewAnalysisTool, ViewCostReportTool, ViewVerificationReportTool, ViewWorkspaceTool
- [ ] Tools that previously had only sync `Execute()` (4 tools — find via `git diff` of override counts) gain an `ExecuteAsync` implementation:
  - For tools with no async deps, body is `Task.FromResult(...)` around the moved sync logic
  - For tools that previously used `.GetAwaiter().GetResult()` (ViewCostReportTool), the sync wrappers are deleted and the async helpers are called directly with `await`
- [ ] Tools that had both `Execute` and `ExecuteAsync`: keep `ExecuteAsync`, delete `Execute`
- [ ] Grep verification: zero matches for `override string Execute\(` under `DraCode.KoboldLair/Agents/Tools/`
- [ ] Grep verification: zero matches for `\.GetAwaiter\(\)\.GetResult\(\)` under `DraCode.KoboldLair/Agents/Tools/`
- [ ] `dotnet build ./DraCode.slnx` clean
- [ ] Existing tool tests pass (if any) — adapt to call `ExecuteAsync` if they were calling `Execute`
- [ ] `CLAUDE.md` "Known Issues" section updated: "Blocking sync-over-async" entry promoted from *partially resolved* (live path async, dead sync path remains) to *fully resolved* (sync path eliminated)

## Out of scope

- Other `.GetAwaiter().GetResult()` usages outside `Agents/Tools/` (see `Drake.cs`, `Wyvern.cs`, `ProjectService.cs`, `ProjectRepository.cs`, etc. — separate cleanup, not blocking)
- Birko.AI base class changes beyond `Tool` itself
- Any behavioural changes to individual tools

## Risks

- The `Tool` base class lives in Birko.AI (external dependency). If Birko.AI cannot be modified in the same PR, fall back to: keep `Execute` as a non-virtual `[Obsolete(error: true)]` shim that throws `NotSupportedException`, override removed from all subclasses. Decide during planning by checking `Birko.AI` aggregator wiring (`BIRKO_SRC` env var convention from `Directory.Build.props`).
- Some tool callers may still call the sync method via reflection (unlikely — grep for `.Execute(` invocations). Verify before deletion.

## Implementation plan

_Populated by `/tasks plan TASK-018` — leave empty until then._
