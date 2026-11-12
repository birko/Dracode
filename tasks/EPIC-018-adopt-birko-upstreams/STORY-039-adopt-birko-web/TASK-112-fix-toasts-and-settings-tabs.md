---
id: TASK-112
parent: STORY-039
feature: FEATURE-026
status: done
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

- [x] `toast-service.ts` is replaced by the framework `toast` / `ToastManager` (or reduced to a call into it); a success and an error toast show their text
- [x] The settings view uses `b-tabs` through its real API (`setTabs` / `active`) and both tabs render and switch
- [x] A component test (or the UI e2e smoke, once the TS shell is served) asserts a visible toast and both tab labels

## Out of scope

- Mounting the TS shell (TASK-072)

## Human test plan

- [x] With the TS shell served, trigger a toast and switch settings tabs — the text and both tabs are visible

## Implementation plan

1. Delete `toast-service.ts`; the two views call the framework `toast.show(message, variant)`.
2. Settings tabs: `<b-tabs active>` with panels slotted by tab id, labels through `setTabs()`, `tab-change` event.
3. Playwright `settings.spec.ts`.

## Implementation notes

- **Toasts:** `services/toast-service.ts` deleted. `settings-view` and `project-config-view` call `toast.show()` from
  `birko-web-components` (a `b-toast-item` carrying `message`); `dashboard-view` imported the service without using
  it. `index.ts` re-exports the framework `toast` in place of `toastService`. The old service's `title` and
  `clear()` had no callers.
- **Tabs:** `settings-view` renders `<b-tabs id="settingsTabs" active="…">` with `<div slot="providers|general">`
  panels, calls `setTabs([{id,label}…], active)` in `onUpdated`, and records `tab-change` (`detail.tab`) so the next
  render keeps the tab; b-tabs switches the panel itself.
- **Sibling, same view, included:** the General tab's `<b-form schema='…' data='…'>` used attributes b-form does not
  have, and saving called a `getFormData()` that does not exist, so the form rendered empty and Save threw before
  any toast. Now `setSchema()` + `setValues()` once per form element (setSchema re-renders the form, so not on
  every view render), `validate()` on save with the values read from their groups (`appearance.theme`, …), and
  Reset puts the defaults back into the form. The schema's top-level group is named `root`: b-form treats only a
  group of that name as the root, so with `generalSettings` every field path was one level too deep and the selects
  never got their options (Theme / Language read back empty, and Save would always fail validation).
- No other Birko component in `src/` is used by a name or API that does not exist (swept: every `<b-*>` tag is
  defined in Birko.Web.Components; no `b-tab`, `b-toast`, `getFormData`, schema/data attributes left).

## Close notes

- `tests/ui-e2e/settings.spec.ts` (3): both tab labels visible and switching shows/hides the General panel; Save
  with the current settings shows a success `b-toast-item` with "Settings saved successfully"; an empty required
  Theme shows an error toast with "Theme is required". All 3 fail on the code before this change. E2E 26/26 against
  the client host; `tsc` clean in `src/`.
- **Human test plan run** (screenshots at 1280×720): Providers and General tabs both visible and switching; the
  General form shows Theme "Auto (system)", Language "English" and the switches; Save shows the green toast with its
  text bottom-right.

