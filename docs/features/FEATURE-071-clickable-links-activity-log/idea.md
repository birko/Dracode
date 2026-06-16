---
id: FEATURE-071
created: 2026-01-13
owner: human
status: done
---

# Clickable links in activity log

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
URLs that appeared in the activity log were plain text. To open one, a user had to select it and copy-paste it into a browser, which is slow and error-prone.

## Proposed shape
Automatically detect web addresses in activity log entries and turn them into clickable links. The links open in a new browser tab safely, and get the usual hover and visited styling so they look and behave like links users expect.

## Out of scope (initial)
- Detecting non-URL references (file paths, ticket IDs, etc.)
- Inline previews or expansions of linked content
- Editing or annotating log entries

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
