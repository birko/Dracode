---
id: FEATURE-063
created: 2026-01-31
owner: human
status: done
---

# New specialized agent types (PHP, Python, Media)

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The system could only field a limited set of specialist workers, so projects involving PHP, Python, or media assets (images, vector graphics, bitmaps) had no dedicated expertise. Work in those areas fell back to general-purpose handling, which produced weaker results for those technologies.

## Proposed shape
Expand the roster of specialist workers from 11 to 17 by adding PHP and Python coding specialists plus a media family (a general media worker with image, vector-graphic, and bitmap specialists beneath it). The planning layer recognizes all 17 specialist types, and the supervisor can assign the right specialist to each piece of work.

## Out of scope (initial)
- Specialists for additional languages beyond PHP and Python in this round.
- Audio or video media handling (only image, vector, and bitmap added).

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
