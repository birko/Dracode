---
id: TASK-112
parent: STORY-039
feature: FEATURE-026
status: todo
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Toasts show nothing and the settings tabs never render — both use Birko.Web components wrongly

## Context

Found in the 2026-10-07 review. `DraCode.KoboldLair.Client/src/services/toast-service.ts` creates a `<b-toast>` element, but Birko.Web.Components
defines only `b-toast-item` (`Birko.Web.Components/src/feedback/b-toast.ts`, which also exports `toast` / `ToastManager`), and
that reads a `message` attribute, not text — every toast is empty. `DraCode.KoboldLair.Client/src/views/settings-view.ts` builds `<b-tabs active-tab=…>`
with `<b-tab>` children; `BTabs` has only `active` + `setTabs()` and no `b-tab` element exists, so the Providers / General tabs
never appear (TASK-056's API-keys tab would hit the same).

## Acceptance criteria

- [ ] `toast-service.ts` is replaced by the framework `toast` / `ToastManager` (or reduced to a call into it); a success and an error toast show their text
- [ ] The settings view uses `b-tabs` through its real API (`setTabs` / `active`) and both tabs render and switch
- [ ] A component test (or the UI e2e smoke, once the TS shell is served) asserts a visible toast and both tab labels

## Out of scope

- Mounting the TS shell (TASK-072)

## Human test plan

- [ ] With the TS shell served, trigger a toast and switch settings tabs — the text and both tabs are visible

## Implementation plan
