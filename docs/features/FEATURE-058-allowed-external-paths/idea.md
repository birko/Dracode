---
id: FEATURE-058
created: 2026-02-03
owner: human
status: done
---

# Allowed external paths

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Workers were locked to a project's own workspace folder. That kept things safe, but it meant they could not touch a real, existing codebase living elsewhere on disk — a hard blocker for working on imported or external projects. Loosening the lock entirely would have been unsafe.

## Proposed shape

Each project can now keep an allowlist of directories outside its workspace that workers are permitted to read and write. Operators manage that list (view, add, remove) through a dedicated control surface, and every file operation is validated against the workspace plus the approved external paths before it runs — so access stays explicit and auditable rather than wide open.

## Out of scope (initial)

- Per-file or per-user permissions — access is granted at the directory level per project.
- Network or remote-path access — the allowlist covers local directories only.

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
