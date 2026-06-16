---
id: FEATURE-028
created: 2026-05-31
owner: human
status: idea
---

# Async ask-human tool for automatic agents

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Background agents have no way to ask a person a question. The only "ask the user" mechanism exists for the interactive Dragon chat and blocks on a console prompt — useless for agents running unattended. So when a real ambiguity blocks correct work, an agent has to guess (usually pessimistically) instead of getting a quick answer from a human.

## Proposed shape

Give automatic agents an `ask_human` capability that posts a structured question — prompt text, optional multiple-choice options, and an optional free-text flag — and then parks the task to wait for the answer (reusing the parking mechanism). When a person answers, the response is delivered straight back into the agent's working context as if it had always been there, so the agent simply continues with the human's input. This is available to the worker and the analyzer agents, but deliberately not to the pre-analysis agent, which should stay fully automatic. A "question for the human" is treated as a deliberate request for input, distinct from an "escalation" (a failure signal), even though they share the same waiting-and-resume plumbing.

## Out of scope (initial)

- The policy that caps how often agents may ask or disables asking for low-risk work (only the hooks are read here).
- The Dragon chat experience for collecting the answer.
- Adding the tool to the pre-analysis agent.

## Prototype

Pending — backlog item; prototype decision deferred to /feature decide.
