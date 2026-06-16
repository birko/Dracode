---
id: TASK-059
parent: STORY-021
feature: FEATURE-024
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: []
blocks: [TASK-060, TASK-061, TASK-062]
pr: null
github-issue: null
jira-key: null
---

# VSCode extension scaffold + manifest + packaging

## Context

New `DraCode.KoboldLair.VsCode/` npm/TypeScript project. Extension manifest registers the side-bar view container, commands, and settings; set up `.vsix` packaging and Marketplace publish wiring (publisher id chosen during planning).

## Acceptance criteria

- [ ] `DraCode.KoboldLair.VsCode/` scaffolded (TypeScript, npm), excluded from the .NET solution build
- [ ] Manifest registers: "KoboldLair" side-bar container with "Chat" + "Runs" panels; commands `Start Chat`, `Run on Selection`, `Connect to Server`, `Sign In`; settings `koboldlair.server`, `koboldlair.useLocalDaemon`
- [ ] `.vsix` packaging works; Marketplace publish workflow stubbed (publisher id TBD)
- [ ] Extension activates and shows empty panels
- [ ] README with install-from-vsix instructions

## Out of scope

- Panel content (TASK-060), auth (TASK-061), diff/selection (TASK-062)

## Human test plan

- [ ] Build the `.vsix`, install into VSCode → the KoboldLair side-bar container appears with two (empty) panels and the commands show in the palette

## Implementation plan

_Populated by `/tasks plan TASK-059` — leave empty until then._
