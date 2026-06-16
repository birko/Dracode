---
id: TASK-050
parent: STORY-019
feature: FEATURE-022
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: []
blocks: [TASK-051, TASK-052, TASK-053, TASK-054]
pr: null
github-issue: null
jira-key: null
---

# Rename DraCode → DraCode.KoboldLair.Cli + single-file publish

## Context

The existing `DraCode/` project is the Spectre.Console CLI agent (confirmed: `Program.cs` + Spectre.Console dependency). Rename it to `DraCode.KoboldLair.Cli/`, update `DraCode.slnx`, and add single-file publish profiles. The CLI statically includes `KoboldLair.Server` to enable self-spawn (binary ~80–100 MB after trimming — accepted).

## Acceptance criteria

- [ ] `DraCode/` → `DraCode.KoboldLair.Cli/`; `.csproj` renamed; `DraCode.slnx` updated; build green
- [ ] References from any project/docs updated to the new name
- [ ] Single-file publish profiles for `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`
- [ ] `koboldlair --version` / `--help` meta verbs work from the published binary
- [ ] Project references `KoboldLair.Server` so later tasks can self-spawn it

## Out of scope

- The actual verbs (TASK-052/053/054) and daemon wiring (TASK-051)
- chocolatey / homebrew / scoop packaging (follow-up, out of story scope)

## Human test plan

- [ ] `dotnet publish` for each RID → run the resulting single-file binary on at least win-x64 and confirm `--help` lists verbs

## Implementation plan

_Populated by `/tasks plan TASK-050` — leave empty until then._
