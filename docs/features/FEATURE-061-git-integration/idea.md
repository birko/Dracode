---
id: FEATURE-061
created: 2026-02-01
owner: human
status: done
---

# Git integration

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Generated project code lived in plain folders with no version history, no branching, and no safe way to review or combine the work produced for different features. There was no way to see what changed, no isolation between parallel pieces of work, and no controlled path from "code was generated" to "code is accepted into the main line."

## Proposed shape
Add first-class Git handling so every project becomes a real repository. Each feature gets its own branch, completed work is committed automatically as it lands, and the user gets clear visibility (branch status, which feature branches are unmerged, whether a branch is ready to merge) plus a controlled merge step that detects conflicts before combining work back into the main line. Works the same way across operating systems.

## Out of scope (initial)
- Hosted remote integration (pushing to GitHub/GitLab) — local repositories only.
- Pull-request style review workflows beyond branch diff and merge.
- Manual interactive rebase or history rewriting.

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
