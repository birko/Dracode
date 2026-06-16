---
id: TASK-001
parent: EPIC-001
status: todo
priority: P1
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
feature: FEATURE-001
pr: null
github-issue: null
jira-key: null
---

# Encrypted token storage

## Context

API keys + auth tokens are currently stored in plain text. Need OS-native encrypted storage: Windows DPAPI, macOS Keychain, Linux secret managers (libsecret / GNOME Keyring / KWallet).

## Acceptance criteria

- [ ] `ITokenStorage` abstraction with `GetAsync(key)`, `SetAsync(key, value)`, `DeleteAsync(key)`
- [ ] `DpapiTokenStorage` for Windows (System.Security.Cryptography.ProtectedData)
- [ ] `KeychainTokenStorage` for macOS
- [ ] `LinuxSecretStorage` for Linux
- [ ] Auto-selection based on `RuntimeInformation.IsOSPlatform()`
- [ ] Migration path: detect existing plain-text storage, encrypt + delete on first read
- [ ] Unit tests for each platform impl (mocked where the OS API is involved)

## Out of scope

- Sharing tokens across machines (separate sync concern)
- Hardware security keys (Yubikey etc. — future)

## Implementation plan

_Populated by `/tasks plan TASK-001` — leave empty until then._
