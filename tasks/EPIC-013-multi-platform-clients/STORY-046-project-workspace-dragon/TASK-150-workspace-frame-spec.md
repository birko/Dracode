---
id: TASK-150
parent: STORY-046
feature: FEATURE-026
status: done
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: [TASK-146, TASK-151, TASK-153, TASK-155, TASK-156, TASK-158]
findings: []
pr: 14
github-issue: null
jira-key: null
---

# Project workspace frame with the read-only spec tab

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

A project opens at `#/projects/{id}/{tab}`: the project's tabs in the centre — Board, Spec, Runs, Git, Stats (D11).
A tab is shown once its screen exists, so this task ships the frame with the Spec tab: the specification
(`GET /api/v1/projects/{id}/specification`, Markdown) and its features (`GET .../features`), read only. Spec edits go
through Dragon (D19), and the tab says so. The homepage's project cards become links to the workspace.

Decisions: FEATURE-026 D11, FEATURE-026 D19.

## Acceptance criteria

- [x] A project card opens the workspace; the URL carries the project and the tab
- [x] The Spec tab renders the specification's Markdown (sanitised) and lists the features with their status; there are no edit controls
- [x] An unknown or not-owned project shows the not-found view
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Dragon docked beside the tabs (TASK-151)
- The other tabs (STORY-047)

## Human test plan

- [x] Open a local project with a long spec: headings, lists and code blocks render; the features show with their status

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Routes:** `#/projects/:id/:tab` opens `kobold-project-workspace`; `#/projects/:id` redirects to the first tab. Birko's
  Router does not hand params to a route's component, so the factory reads `router.current.params` (set before the
  component is built). `WORKSPACE_TABS` in `project-workspace.ts` lists the tabs whose screens exist (Spec only); an
  unknown tab, or a project the API answers 404 for (unknown or another owner's), shows the not-found view.
- **Spec tab** (`spec-tab.ts`): `GET …/specification` and `GET …/features` in parallel; the Markdown beside the
  features (status and priority badges, description); stacks in one column below 56rem. No edit controls; a note links
  to Dragon. Code blocks and tables scroll inside themselves, so a long line never widens the page.
- **Markdown** (`services/markdown.ts`): Birko's `BMarkdownEditor.renderMarkdown` escapes raw HTML but copies link and
  image targets into the attribute unchecked (`javascript:` URLs, a `"` that opens a new attribute), and Birko.Web has
  no sanitiser. Its output goes through a DOM allowlist (only the elements and attributes the renderer emits; href/src
  only http(s), mailto, fragment or relative; external links open in a new tab with `noopener`). Two renderer quirks
  are tidied after it: a `<br>` between the items of a list that directly follows a paragraph line, and the newline
  after an opening code fence. The sanitiser is the candidate to move into Birko.Web.
- **Not fixed (Birko renderer):** a list item continued on an indented next line ends the list; the continuation
  renders as its own paragraph. Seen on `waver`'s spec. Belongs in Birko's renderer, not patched here.
- **Homepage:** a project card is now a link to `#/projects/{id}/spec`.
- **e2e:** `workspace.spec.ts`, 10 tests: click-through and URL, Markdown and features, read-only + Dragon note, hostile
  markup (script, `onerror`, `javascript:` link, `"`-injected handlers) leaves no script, handler or `javascript:` URL,
  tight list and code-fence quirks, empty spec/features, 404 project, unknown tab, `#/projects/{id}` redirect, 375px with
  a long code line and a wide table. 7 were red on the old build (the 404 and unknown-tab tests passed there only
  because no route matched); the hostile-markup test fails with the sanitiser switched off, the tight-list test without
  the tidy step. Suite 45/45.
- **Human test** (throwaway Testing server on a scratch copy, JWT on): the long `docs/Drake-Monitoring-System.md` put
  into `waver2` through `PUT /specification` rendered with 16 h2, 34 h3, 40 code blocks, 3 tables and no stray fences;
  `waver`'s feature showed with its status and priority; another user got the not-found view. Light and dark at 1280px
  and 375px: no horizontal scroll, no console errors.
