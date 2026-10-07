---
id: STORY-039
parent: EPIC-018
status: planned
created: 2026-10-07
---

# Adopt Birko.Web upstreams in the web client

## User story

As the KoboldLair web client, I want routing, auth, transport and UI widgets to come from Birko.Web, so that DraCode keeps
only its own screens (Dragon, Kobold runs, projects, escalations, the server selector) and stops carrying copies — and
the bugs those copies hide.

## Source

The 2026-10-07 review of `DraCode.KoboldLair.Client` against Birko.Web. **None of the TS client runs today**: the served
`wwwroot/index.html` is the legacy vanilla-JS app (TASK-072), and all nine views still use mock data. Framework side:
Birko `EPIC-019 / STORY-059` (TASK-523 – TASK-526). Building on the framework from the start also shapes TASK-055 – 058.
