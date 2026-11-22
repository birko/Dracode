---
id: TASK-149
parent: STORY-045
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-151]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Home Dragon on the projects homepage

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

With no project selected, the homepage docks a "home Dragon" — the same dock component as the workspace — for
starting new projects and asking across projects. Dragon history is stored per project; check what the server keeps
for a session with no project. If it keeps nothing, this task adds per-user storage for the home conversation.

Decisions: FEATURE-026 D12.

## Acceptance criteria

- [x] The homepage shows the Dragon dock with its own conversation, not any project's
- [x] A project created from the home Dragon appears on the homepage; opening it switches Dragon to that project's history
- [x] The home conversation is still there after a reload
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The dock component itself (TASK-151)

## Human test plan

- [x] Ask the home Dragon to start a project, finish the spec, open the new project: Dragon now shows the project's conversation; go home: the home conversation is back
  - **Passed 2026-11-22:** see Notes; the run that loaded other projects first could not create one → TASK-186

## Implementation plan

Found: the server keeps nothing for a session with no project — `TrackMessage` persists only when
`CurrentProjectFolder` is set, and `select_project` with no id is a `project_not_found` error. While the home chat
creates a project, Dragon's tools set `CurrentProjectFolder` to it (`CheckForNewSpecifications`, `ResolveProjectFolder`),
so "persist where the folder points" would save the home conversation into the new project. The home scope therefore
has to be a session flag of its own, not "no folder".

1. **Store** — `SqlHistoryRepository.SaveHomeHistoryAsync(ownerSub, messages)` / `LoadHomeHistoryJsonAsync(ownerSub)`:
   the same `dragon_history` table, key `home:<sub>` (not a path, so not `NormalizeFolder`ed). File fallback (no SQL):
   `{ProjectsPath}/.home/<sanitized sub>/dragon-history.json`.
2. **Server: `select_home`** — new message in `DragonService.HandleMessageAsync`. Waits for the running request (as
   `SelectProjectAsync`), sets `session.Home = true`, `CurrentProjectFolder = null`, loads the caller's home history
   into `MessageHistory`, `Dragon.ClearContext()` + `RestoreContext(ConversationFromHistory(...))`,
   `UpdateProjectContext(null)` (the default working directory), answers `home_selected { messageCount }` and replays.
   Empty history → the welcome (`StartSessionAsync`), tracked, so it is saved as the home conversation's first message.
   `select_project` clears `session.Home`.
3. **Server: persistence** — `TrackMessage`: `session.Home` → save to the home key of `session.OwnerSub`, whatever
   `CurrentProjectFolder` points at; otherwise as today.
4. **Server: `specification_created`** — sent after the project is registered (today it goes out first), carrying
   `projectId`, so the homepage can refresh and find the new project.
5. **Client: `DragonConnection.selectHome()`** — state gains `home: boolean`; `select_home` on open / after a lost
   session, `home_selected` → ready (like `project_selected`), resume settles the same way; live `specification_created`
   raises a `projects-changed` signal (a small listener set on the connection).
6. **Client: dock** — `kobold-dragon-dock` without `project-id` follows home: home placeholder ("Ask Dragon to start a
   project…"), no decision cards (Needs you lists them on the homepage).
7. **Client: homepage** — the dock on the right of the cards, stacked below them under 56rem like the workspace;
   reloads its project list on `projects-changed`.
8. **Tests** — .NET: home history saved under the owner and not under the new project's folder, reloaded by
   `select_home`, another owner's home separate, `select_project` after it gets the project's own conversation.
   e2e `home-dragon.spec.ts` (fake `/dragon` as in `dragon-dock.spec.ts`): homepage sends `select_home` and shows the
   replayed home conversation; a `specification_created` reloads the project list and the new card appears; opening it
   sends `select_project`; back home sends `select_home` again; reload → `select_home` again (persistence is the
   server's, covered by .NET). Red before the change.

Decided 2026-11-22 (owner): a project created from the home chat starts with an empty conversation of its own; the
chat that created it stays at home.

Risk, met in the human test: `ResolveProjectFolder` prefers the session's current folder, so once Sage has loaded a
project, a new one cannot be created in that session → deferred to TASK-186.

## Notes

- **Server** (`DragonService`): `select_home` → `SelectHomeAsync` waits for the running request, sets `session.Home`,
  loads the owner's home conversation as the session history and Dragon's context, moves Dragon back to the working
  directory it was created with (`DragonAgent.ResetProjectContext`, also clearing external paths), answers
  `home_selected` and replays; an empty home conversation starts with the welcome (`SendWelcomeAsync`, the
  `client_ready` welcome moved into a method). `select_project` clears `Home`. While `Home`, `TrackMessage` saves to the
  owner's home key whatever `CurrentProjectFolder` says, and the chat-driven project load (`OnProjectLoadedAsync`) and
  `CheckForNewSpecifications` leave the session history alone. `specification_created` now goes out after the project
  is registered and carries `projectId`.
- **Store**: `SqlHistoryRepository.SaveHomeHistoryAsync` / `LoadHomeHistoryJsonAsync`, row `home:<sub>` in
  `dragon_history`; without SQL, `{ProjectsPath}/.home/<sub>/dragon-history.json`.
- **Client**: `DragonConnection.selectHome()` (state `home`; HOME stands for the server's target so a resume settles
  the same way), `onProjectsChanged` fired by a live `specification_created`; `kobold-dragon-dock` with no `project-id`
  follows home (its own placeholder, no pin, no decision cards); the homepage docks it beside the cards (below them
  under 56rem, `data-morph="skip"` so reloading the cards keeps the chat) and reloads its list on `onProjectsChanged`.
- **Tests:** .NET `DragonProjectSocketTests` +4: home replayed per owner and kept apart from another's; an empty home
  starts with the welcome; a message at home saved at home and not in the project left; a project loaded from the home
  chat leaves the home history (red without the guard: "found empty collection"). e2e `home-dragon.spec.ts`, 4 tests
  (all red on the old client): home conversation in the dock; `specification_created` adds the card, opening it sends
  `select_project` with an empty conversation, home again sends `select_home`; a reload asks again; phone width.
  Suites .NET 533/533, e2e 136/136.
- **Human test** (throwaway Testing server on a scratch copy of `projects160`, LAN model): from the home Dragon,
  `todo149` was specified and saved, its card appeared, its workspace showed an empty conversation, and going home and
  reloading brought back the stored home conversation (every user message and final reply). 375px: dock under the
  cards, no horizontal scroll, no console errors. Dark mode not checked (the emulated scheme does not switch the app's
  theme). Found: the home history was overwritten when the home chat loaded another project (fixed here, test above);
  after Sage loads a project, no new one can be created in that session → TASK-186. A new message sent while Dragon
  is still answering cancels that answer (`DragonRequestQueue.EnqueueAsync`), so Dragon replies without it → TASK-187.
