---
id: FEATURE-026
created: 2026-05-31
---

# DraCode.KoboldLair.Client — UI polish reclaimed from EPIC-003 — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Drag-and-drop tab reordering in the project workspace view | approved | Tab order is each person's own, across their projects; not on a phone | 2026-11-24 | owner | TASK-058 |
| D2 | Saveable named workspace layouts (open panels, splitter sizes, active tabs) persisted to the user profile on the server | approved | Per person, from a Layouts menu; a divider sets Dragon's width at rest and focus or the pin still widen it (a mix of prototypes A and B) | 2026-11-24 | owner | TASK-058, TASK-197, TASK-198 |
| D3 | Daemon status indicator in the header (green/red, click for details) | proposed | New affordance exposed by the multi-platform rework | — | — | — |
| D4 | Project switcher in the header that preserves chat scroll position across switches | changed | Now D10: a project picker in the header inside a project, the projects homepage as home; scroll preservation not discussed | 2026-11-12 | owner | TASK-146 |
| D5 | OAuth login button replacing the token-paste field | approved | Sign-in is GitHub through Birko.Framework (D8) | 2026-11-12 | owner | TASK-056 |
| D6 | Service-account key management UI under user settings → API Keys | approved | Part of the v1 settings screen (D20) | 2026-11-12 | owner | TASK-162 |
| D7 | Recent-runs panel to view and stream ad-hoc runs started outside the web UI | approved | Covered by the v1 live run view, the workspace's Runs tab (D20) | 2026-11-12 | owner | TASK-155, TASK-057 |
| D8 | Desktop first; users sign in with GitHub (Birko.Framework) and each sees their own projects; admins see all | approved | KoboldLair runs locally or on one server; specs, boards and diffs need space | 2026-11-12 | owner | TASK-056 |
| D9 | Rebuild the web UI rather than evolve the current views; keep only the plumbing (router, token store, KoboldComponent, build, e2e) | approved | The current views are mocks shaped for the old ribbon layout | 2026-11-12 | owner | TASK-145 |
| D10 | Home is a projects page (all projects, charts, progress, a "Needs you" list); inside a project a picker in the header switches projects, with an "N other projects need you" pill; no left project list | approved | Cross-project overview where it belongs, and the workspace keeps its full width | 2026-11-12 | owner | TASK-145, TASK-146, TASK-147 |
| D11 | Project workspace: the project's tabs in the centre (Board, Spec, Runs, Git, Stats), Dragon docked on the right | approved | The board and runs need width; Dragon, the main way in, stays in view | 2026-11-12 | owner | TASK-150, TASK-151 |
| D12 | Dragon follows the selected project (its per-project history); with no project selected, a home Dragon for new projects and cross-project questions | approved | Dragon history is already stored per project | 2026-11-12 | owner | TASK-149, TASK-151 |
| D13 | Dragon resizes with focus: expands when focus enters it (click or shortcut), shrinks when focus leaves, no timers; ~360px shrunk, ~60% expanded pushing the board aside (board becomes a compact list), ~150 ms transition, a "Keep expanded" pin remembered per user | approved | A thin chat is hard to read; it gets width only while in use | 2026-11-12 | owner | TASK-152 |
| D14 | A new decision does not expand Dragon: its card shows in the shrunk panel with a badge; it expands on the user's click | approved | Does not pull the board away mid-task | 2026-11-12 | owner | TASK-160 |
| D15 | Anything an agent layer cannot resolve bubbles up to Dragon as an answerable decision card | approved | Dragon is the user's one touchpoint (EPIC-014 plans the server side) | 2026-11-12 | owner | TASK-160 |
| D16 | Kanban: fixed status columns (To do / In progress / Blocked·Failed / Done) moved by the agents; the user reprioritises by dragging between priority lanes in To do (existing priority call); Retry on failed cards; further statuses later if missing | approved | No server change; execution order stays priority → dependencies → complexity | 2026-11-12 | owner | TASK-153, TASK-154 |
| D17 | One notification stream shown three ways: a badge on the project, the header bell, and a decision card in Dragon | approved | One source, seen wherever the user is | 2026-11-12 | owner | TASK-159, TASK-160 |
| D18 | Stats: cross-project charts and numbers on the projects homepage; per-project numbers in the workspace's Stats tab | approved | Overview at home, detail in the project | 2026-11-12 | owner | TASK-148, TASK-158 |
| D19 | Read-only spec viewer in the workspace; spec edits go through Dragon | approved | One editing path for specifications | 2026-11-12 | owner | TASK-150 |
| D20 | v1 screens also include: live run view (plan steps, progress), git review and merge (branch diff, merge), settings (providers, API keys, budgets) | approved | The pipeline ends in a merge; settings are needed to run it | 2026-11-12 | owner | TASK-155, TASK-057, TASK-156, TASK-157, TASK-161, TASK-163 |
| D21 | Mobile "check and respond": projects and progress, a read-only board, Dragon chat and decisions, notifications; git, settings, API keys and reprioritising stay desktop | approved | What is done away from the desk, without doubling the work | 2026-11-12 | owner | TASK-164 |
| D22 | Responsive from the start: one breakpoint at ~900px; phones get a bottom tab bar with Dragon as its own tab; tablets the desktop layout with Dragon as an overlay | approved | Retrofitting mobile onto desktop-only views is the expensive way round | 2026-11-12 | owner | TASK-145, TASK-164 |
| D23 | Public preview deployments of projects on subdomains | deferred | Needs hosting, DNS and security decisions of its own; a Preview link is reserved in the workspace. Unblock: its own task | 2026-11-12 | owner | — |
| D24 | Phone push notifications (PWA web push + a server sender) | deferred | In-app badge and bell cover v1. Unblock: its own task | 2026-11-12 | owner | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-023 behaviour (reclaimed EPIC-003 items + new multi-platform affordances).
- 2026-11-12 — layout grill with the owner (mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs, private until shared). D8–D22 approved, D23–D24 deferred to their own tasks; D4 changed into D10; D5, D6, D7 approved as parts of D8 / D20. D1–D3 not discussed, still proposed. Not yet decomposed into tasks.
- 2026-11-16 — decomposed with the owner: the rebuilt UI reads only `/api/v1` and `/dragon` (owner-scoped), and each screen whose server endpoint is missing builds it in the same task. Tasks sit under EPIC-013 in STORY-023 (retitled to the frame and sign-in) and the new STORY-045 to STORY-050. TASK-056 and TASK-057 were rewritten to the new layout; TASK-055, TASK-115 and TASK-116 were cancelled as superseded; TASK-058 is parked with D1/D2. D14–D15 wait on EPIC-014 TASK-024 for the server side.
- 2026-11-16 — D4 (changed) decomposed → TASK-146
- 2026-11-16 — D5 (approved) decomposed → TASK-056
- 2026-11-16 — D6 (approved) decomposed → TASK-162
- 2026-11-16 — D7 (approved) decomposed → TASK-155, TASK-057
- 2026-11-16 — D8 (approved) decomposed → TASK-056
- 2026-11-16 — D9 (approved) decomposed → TASK-145
- 2026-11-16 — D10 (approved) decomposed → TASK-145, TASK-146, TASK-147
- 2026-11-16 — D11 (approved) decomposed → TASK-150, TASK-151
- 2026-11-16 — D12 (approved) decomposed → TASK-149, TASK-151
- 2026-11-16 — D13 (approved) decomposed → TASK-152
- 2026-11-16 — D14 (approved) decomposed → TASK-160
- 2026-11-16 — D15 (approved) decomposed → TASK-160
- 2026-11-16 — D16 (approved) decomposed → TASK-153, TASK-154
- 2026-11-16 — D17 (approved) decomposed → TASK-159, TASK-160
- 2026-11-16 — D18 (approved) decomposed → TASK-148, TASK-158
- 2026-11-16 — D19 (approved) decomposed → TASK-150
- 2026-11-16 — D20 (approved) decomposed → TASK-155, TASK-057, TASK-156, TASK-157, TASK-161, TASK-163
- 2026-11-16 — D21 (approved) decomposed → TASK-164
- 2026-11-16 — D22 (approved) decomposed → TASK-145, TASK-164
- 2026-11-24 — D1, D2 approved with the owner when TASK-058 was picked (prototypes on the layout canvas); built in TASK-058 (PR #60)
- 2026-11-24 — D2 — TASK-197 spawned from TASK-058 (dragging the divider leaves a gap beside Dragon until the drop)
- 2026-11-25 — D2 — TASK-198 split from TASK-197 (the divider stopping at the minimums while dragged; waits on Birko TASK-981)
