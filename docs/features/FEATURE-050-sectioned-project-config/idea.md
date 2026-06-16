---
id: FEATURE-050
created: 2026-02-04
owner: human
status: done
---

# Sectioned project configuration

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Project configuration lived in a flat layout that mixed identity, agent settings, and security concerns together. It was hard to read at a glance, hard to extend, and gave little fine-grained control over how each type of agent behaved or how much access a project had.

## Proposed shape
Reorganize each project's configuration into clear sections: project identity, agent settings, security, and metadata. Each agent type gets its own settings (whether it is enabled, which provider and model it uses, how many can run at once, and a timeout), with a separate block for the Kobold Planner. Security gains a sandbox mode (workspace, relaxed, or strict) controlling how much access a project has outside its own folder, and every project records a creation timestamp for auditing. The result is a configuration file that is easier to read and far more controllable.

## Out of scope (initial)
- A user-facing editor UI for the new configuration sections
- Migrating historical projects beyond the new format

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
