---
id: FEATURE-019
created: 2026-05-31
owner: human
status: idea
---

# OAuth/OIDC authentication and per-caller identity

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Right now there is no real notion of who is making a request. Anyone with the shared token has the same access. The moment KoboldLair is exposed to more than one client — a laptop CLI, a Discord bot, a CI runner, a teammate — there is no way to tell callers apart, audit who did what, or revoke one caller without disrupting the rest. At the same time, the common single-user local install should keep working with no setup at all.

## Proposed shape

Add real per-caller identity backed by a recognised sign-in provider (GitHub first, with room for others later). Human users sign in through their browser; the command line caches the login. Non-human callers like bots and CI runners get long-lived service credentials with limited scopes. Each caller maps to a stored user record, and projects gain an owner so people see their own work by default.

Crucially, the common local case stays friction-free: when the server runs as a trusted local daemon bound to the machine itself, it trusts the operating-system user and skips sign-in entirely. Only remote, network-exposed servers require it.

## Out of scope (initial)

- Adding identity providers beyond the first (GitHub); others come later
- Fine-grained team roles and sharing beyond owner-scoped visibility
- Permanently removing the old shared-token system in this step (it stays one release as a migration fallback)

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
