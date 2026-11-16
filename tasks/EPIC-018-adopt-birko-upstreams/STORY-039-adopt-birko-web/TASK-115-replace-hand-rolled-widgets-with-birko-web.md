---
id: TASK-115
parent: STORY-039
feature: FEATURE-026
status: cancelled
priority: P3
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Replace hand-rolled widgets and helpers with Birko.Web ones

## Context

The review found copies of framework pieces across the views: the same 4-line `escapeHtml` in all nine views and the server
selector (37 copies; Birko.Web.Core `dom/html.ts` has it); views re-binding listeners by calling `onMount()` from `onUpdated()`
instead of `BaseComponent.listen()`; Dragon's custom message list (`b-chat` exists); custom `.stat-card` tiles and range
buttons in dashboard / metrics / impact (`b-stat`, `b-segmented`); hand-rolled CRUD for providers and project config (Shell
`base-crud-page` / `base-form-modal` / `base-list-page`); the 1,920-line legacy `wwwroot/styles.css` (framework `tokens.css` +
themes).

## Acceptance criteria

- [ ] `escapeHtml` and `listen()` come from Birko.Web.Core; no local copies remain
- [ ] Dragon's chat uses `b-chat`; dashboard / metrics / impact tiles and ranges use `b-stat` / `b-segmented`
- [ ] Providers and project config use the Shell CRUD / form-modal / list pages
- [ ] Styling comes from the framework tokens and themes; `styles.css` is retired with the legacy page
- [ ] The UI e2e smoke stays green; each switched view keeps its behaviour

## Out of scope

- Domain screens' content (projects, hierarchy, compare, escalations) — they stay DraCode's

## Human test plan

- [ ] Walk each switched screen in light and dark theme — layout and wording read right

## Implementation plan

## Cancelled

Cancelled 2026-11-16, superseded. The views it would convert are being rebuilt (FEATURE-026 D9). Each rebuilt screen's task requires the Birko pieces from the start: `b-chat` for Dragon (TASK-151), `b-stat` tiles (TASK-148, TASK-158), Shell CRUD pages for providers (TASK-161), Core `escapeHtml` / `listen()`, the framework tokens and themes. The legacy `styles.css` goes with the old settings view (TASK-161).
