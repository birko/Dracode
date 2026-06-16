---
id: FEATURE-068
created: 2026-01-31
owner: human
status: done
---

# Dragon multi-session support

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

A single connection could only hold one conversation, and a dropped connection meant losing the conversation. Users who briefly went offline, or who wanted several conversations going at once, lost their context and had to start over. Changing the underlying AI provider mid-conversation was also not possible.

## Proposed shape

Support multiple concurrent conversations on a single connection, each tracked independently. When a connection drops, conversations survive for about ten minutes so the user can reconnect and pick up exactly where they left off, with recent message history (up to a hundred messages per conversation) replayed automatically. Allow the AI provider to be swapped mid-conversation, and run a background cleanup so stale conversations are tidied up roughly once a minute.

## Out of scope (initial)

- Permanent long-term conversation archival beyond the reconnect window
- Cross-device conversation handoff

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
