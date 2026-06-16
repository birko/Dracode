---
id: FEATURE-030
created: 2026-05-31
owner: human
status: idea
---

# Answerable decisions surfaced in Dragon

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Today's notifications are read-only: they tell a human something happened, but you have to poll for them and you can't act on them in place. Once an agent parks a task waiting on a person, there's no way to actually answer in the chat — so the pipeline can end up silently waiting while the human keeps refreshing a notification list.

## Proposed shape

Turn pending decisions into actionable prompts inside the Dragon chat. A notification gains an optional "needs a response" flag plus the shape of the expected answer (the choice options or free-text). When a task parks or an agent asks a question, that prompt is pushed to the chat in real time and shown as choice buttons or an input box — not just a readable line. Submitting the answer feeds it back through the round-trip plumbing to unpark and resume the task. A dedicated tool also lets the human list and answer pending decisions explicitly, in case the live prompt was missed. Answering is idempotent (a second answer to the same decision is a harmless no-op), and if the human is offline when a decision is raised, it persists and is replayed on reconnect.

## Out of scope (initial)

- The policy model that decides which decisions need a human (separate feature).
- Non-Dragon clients.

## Prototype

Pending — backlog item; prototype decision deferred to /feature decide.
