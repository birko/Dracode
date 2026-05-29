---
id: STORY-025
parent: EPIC-014
status: planned
created: 2026-05-29
---

# Async `ask_human` tool for automatic agents

## User story

As an automatic agent (Kobold or Wyvern), I want to ask a human a clarifying question and receive the answer asynchronously, so that I can stop guessing pessimistically when a real ambiguity blocks correct work.

## Behaviour

- A new `ask_human` tool, distinct from the interactive-only `AskUserTool` (`DraCode/Program.cs:508`): it does **not** block a thread or require a console.
- Calling it writes a structured question (prompt text + optional choice options + optional free-text flag) to project state and parks the calling task (reuses the `AwaitingHumanDecision` mechanism from STORY-024).
- The answer is delivered back into the agent's context on resume, as a synthetic tool-result / user message, so the agent continues with the human's input in-band.
- Available to Kobold and Wyvern; explicitly **not** added to Wyrm (pre-analysis should stay fully automatic) unless policy enables it.
- Distinguish "question for the human" (this tool) from "escalation" (a problem signal). A question is a deliberate request for input; an escalation is a failure/uncertainty signal. They share the parking/round-trip plumbing but carry different intent metadata.
- Edge case: an `ask_human` with choice options should validate the human's answer against the options; free-text answers pass through verbatim.
- Risk: agents could over-ask and stall throughput. Mitigate via policy (STORY-026) — e.g. cap questions per task, or disable for non-critical tasks.
