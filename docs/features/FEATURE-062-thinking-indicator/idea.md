---
id: FEATURE-062
created: 2026-02-01
owner: human
status: done
---

# Dragon thinking indicator

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
While the Dragon assistant was working on a reply, the chat sat silent with no sign that anything was happening. Users couldn't tell whether their message had been received or whether the system had stalled, which made the conversation feel unresponsive during longer waits.

## Proposed shape
Show a live "thinking" indicator in the Dragon chat while a response is being generated. The moment the assistant starts processing, the chat displays a real-time processing cue, and it clears as soon as the reply begins, so the user always knows the system is actively working.

## Out of scope (initial)
- Progress percentages or time-remaining estimates.
- Indicators for agents other than Dragon.

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
