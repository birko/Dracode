---
id: EPIC-001
status: planned
created: 2026-05-28
owner: ai
---

# Token storage & auth providers

## Area of concern

Two related security/auth gaps: secure storage for API keys + auth tokens (currently plain text), and OAuth provider integration (Google, GitHub) for user sign-in beyond the current JWT auth.

## Success criteria

- API keys + auth tokens stored encrypted via OS-native secret stores
- Migration path from existing plain-text storage
- OAuth login flows for Google + GitHub working alongside JWT

## Features

| Feature | Covers | Status |
|---------|--------|--------|
| [FEATURE-001](../../docs/features/FEATURE-001-token-storage-auth/idea.md) | TASK-001, TASK-002 | idea |
