---
id: TASK-188
parent: STORY-049
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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
dark theme (DraCode: light / dark); no flash of light for a dark browser. **Shipped in Birko.Web v0.5.2**
(2026-11-22): the theme menu lists Auto first when dark is registered; Auto follows `prefers-color-scheme` live and is
saved as `'auto'`; an app starts on it by overriding `protected get defaultTheme() { return 'auto'; }`; `appliedTheme`
gives what Auto resolved to and `theme-change` carries `{ theme, applied }`; `themeBootScript({ prefix, defaultTheme:
'auto' })` from `birko-web-shell` returns an inline head script against the light flash (same prefix as the shell's
`storagePrefix`). Auto picks the registered dark theme, else light.

## Acceptance criteria

Rescoped with the owner 2026-11-24: there is no Settings → Theme value any more (TASK-161 removed the General tab
that saved `appearance.theme`), so the header's theme menu, kept per browser, is the one control.

- [x] A user who has chosen no theme starts on Auto (`defaultTheme`), and a dark browser gets the dark theme before the
      app's script runs, so there is no light flash
- [x] `auto` follows the browser's `prefers-color-scheme`, including a change while the page is open (e2e with
      `page.emulateMedia({ colorScheme })`)
- [x] A theme chosen in the header's menu sticks whatever the browser says, also after a reload; choosing Auto follows
      the browser again

## Out of scope

- The shell's `auto` support itself (Birko.Web, shipped in v0.5.2)

## Human test plan

- [x] Set the header's theme menu to Auto, switch the OS between light and dark: the UI follows without a reload; set
      Dark: it stays dark whatever the OS says

## Implementation plan

1. Red first, `tests/ui-e2e/theme.spec.ts`:
   - With `app.js` blocked, a dark browser's page still has `data-theme="dark"` (only the boot script can set it).
   - A new user is on Auto and follows the browser both ways while the page is open.
   - Dark from the header menu stays dark under a light browser and after a reload, and choosing Auto follows the
     browser again.
2. `src/theme-settings.ts` holds the storage prefix (`app`, the shell's default, so a saved choice is kept) and the
   starting theme (`auto`). `KoboldLairAppShell` returns them from `storagePrefix` and `defaultTheme`.
3. `build.js` writes `wwwroot/dist/theme-boot.js` from Birko.Web.Shell's own `themeBootScript`, with those two values.
   It bundles the function and the settings with esbuild and calls them at build time, so the shell and the script
   cannot drift. `index.html` runs the script first in `<head>`.

## Notes

- The theme stays per browser (localStorage), as the shell keeps it. That suits Auto, which follows that browser
  anyway. Saving it per user on the server was offered and not chosen.
- Human test by the owner, 2026-11-24, with the real OS setting: Auto showed in the menu, switching Windows between light
  and dark was followed without a reload, and Dark stayed dark under a light OS.
- Screenshots at first load: a dark browser opens dark and a light one opens light, both with Auto ticked in the menu
  (Auto, Light, Dark, Neon, Finstat).
- Full e2e suite: 162 passed. `tsc --noEmit` reports the same 32 errors as before, all in Birko.Web's sources. No .NET
  change.
