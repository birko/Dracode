---
id: TASK-188
parent: STORY-049
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-035]
pr: null
github-issue: null
jira-key: null
---

# The theme setting is applied, and "Auto" follows the browser's light/dark preference

## Context

Found 2026-11-22 in TASK-149's human test: with the browser set to dark, the web UI stayed light. The Birko.Web shell
picks the theme at load from localStorage (`{prefix}-theme`, written by the header's theme menu) and otherwise uses
`light` (`Birko.Web.Shell/src/shell/b-core-app-shell.ts` ~341–344). It never reads `prefers-color-scheme`, and the
framework CSS has no media rule for it; dark is only `[data-theme="dark"]` on `<html>`.

The Settings page (`views/settings-view.ts`) saves `appearance.theme` (`light` | `dark` | `auto`, default `auto`) to the
server, but nothing applies it: the page only ever reads localStorage. So "Auto" (and the stored setting as a whole)
does nothing.

The framework side, an `auto` theme in the shell that follows `prefers-color-scheme` and switches live, was asked of the
Birko.Web session on 2026-11-22 and filed there as Birko FEATURE-024 (not scheduled yet). DraCode's answers to its open
questions (owner, 2026-11-22): each app opts in to Auto as its starting theme (DraCode will); the app names its light and
dark theme (DraCode: light / dark); no flash of light for a dark browser. Start this task once that ships.

## Acceptance criteria

- [ ] On sign-in / load, the saved `appearance.theme` is applied through the shell's `setTheme`; changing it in Settings
      applies at once
- [ ] `auto` follows the browser's `prefers-color-scheme`, including a change while the page is open (e2e with
      `page.emulateMedia({ colorScheme })`)
- [ ] The header's theme menu and the Settings value agree (choosing one updates the other)

## Out of scope

- The shell's `auto` support itself (Birko.Web)

## Human test plan

- [ ] Set Settings → Theme to Auto, switch the OS between light and dark: the UI follows without a reload; set Dark: it
      stays dark whatever the OS says

## Implementation plan

_Populated by `/tasks plan TASK-188` — leave empty until then._
