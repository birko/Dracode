---
id: FEATURE-001
created: 2026-05-31
owner: human
status: idea
---

# Token storage & auth providers

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Today, API keys and login tokens are kept in plain text on disk. Anyone who can read the files can read the secrets — a real security gap. On top of that, the only way to sign in is a built-in username/password style flow; there's no way to log in with a Google or GitHub account that people already have.

## Proposed shape

Move all secrets into the operating system's own secure vault — the Windows credential store on Windows, the Keychain on Mac, and the standard secret manager on Linux — so the right vault is picked automatically per machine. Existing plain-text secrets are quietly upgraded into the vault the first time they're used, so nobody has to re-enter anything. Separately, add "Sign in with Google" and "Sign in with GitHub" alongside the existing login, with options to control who is allowed to create an account and what role they get.

## Out of scope (initial)

- Sharing or syncing secrets across multiple machines
- Hardware security keys (e.g. Yubikey)
- Enterprise single sign-on (SAML / SSO)
- Linking more than one external account to a single user

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
