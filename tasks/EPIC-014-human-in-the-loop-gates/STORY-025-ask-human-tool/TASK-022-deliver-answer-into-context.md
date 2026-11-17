---
id: TASK-022
parent: STORY-025
feature: FEATURE-028
status: in-progress
blocked: 'merge deferred: stacked on task/TASK-021 (PR #3), itself stacked on task/TASK-020, which waits on its end-to-end check at TASK-024; code complete on task/TASK-022'
priority: P2
assignee: agent
created: 2026-05-29
depends-on: [TASK-020, TASK-021]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Deliver human answer back into agent context on resume

> Blocked 2026-11-17 — merge deferred: stacked on task/TASK-021 (PR #3), itself stacked on task/TASK-020, which
> waits on its end-to-end check at TASK-024; code complete on task/TASK-022. `/tasks unblock TASK-022` and re-close
> once TASK-021 has merged.

## Context

When a parked `ask_human` (TASK-021) or human-decision (TASK-020) is answered, the agent must resume with the answer in-band so its next reasoning step uses it. The Kobold already supports conversation checkpoint save/resume (`Kobold.cs:1200-1238`); this task injects the answer as a synthetic tool-result / user message at resume time. This is the round-trip that makes `ask_human` actually useful and is the same channel STORY-029 reuses for escalation-resolution payloads.

## Acceptance criteria

- [x] On decision resolution, the answer is injected into the resumed agent's conversation as a synthetic user message referencing the decision, clearly attributed to the human. (Reworded 2026-11-16: the `ask_human` call already has its result, and a second one is invalid for the provider APIs.)
- [x] Resume picks up from the saved plan/conversation checkpoint with the answer present before the next LLM call.
- [x] Works for Kobold parked tasks. (Reworded 2026-11-16: FEATURE-028 D4 changed to Kobold only; Wyvern's questions are TASK-170.)
- [x] Choice answers are delivered as the chosen option value; free-text delivered verbatim.
- [x] Test: a Kobold that asked a 2-option question resumes and its next action reflects the chosen option.
- [x] A decision that timed out with nobody answering resumes the task with that fact in the agent's context ("no answer — timed out"), not as if an answer had arrived (from the TASK-019 grill, 2026-11-16).

## Outcome

A person's answer, or the fact that nobody answered in time, reaches the resumed Kobold as a user message tagged
`[Human answer — decision <id>]`, once per conversation: a checkpoint resume gets only the answers its restored
conversation lacks, a question asked on the first step resumes its checkpoint, and a fresh prompt or the basic plan
path lists every answer. A timed-out escalation names what its automatic route did, now stored on the decision. An
unreadable decision store holds the task instead of running it without its answers. 14 new tests; 417/417.

## Out of scope

- The Dragon UI that collects the answer — TASK-024.
- Escalation-resolution feedback content — STORY-029/TASK-026 (reuses this channel).

## Human test plan

- [x] End-to-end: Kobold asks → answer submitted via repository/test hook → Kobold resumes and visibly acts on the answer in its next reflection.
  - **Passed 2026-11-17:** automated — `KoboldRunHooksTests.A_Kobold_that_asked_resumes_its_conversation_with_the_answer_and_acts_on_the_chosen_option`
    (scripted model asks sqlite or postgres, is resumed with "postgres", writes `postgres.txt` and not `sqlite.txt`) and
    `EscalationToHumanTests.A_summoned_Kobold_hears_the_tasks_answers_oldest_first_and_not_an_open_question`
    (answer stored through Drake, read back on summon) green, 417/417 full suite twice; each proved able to fail by
    mutation. The act shown is the next tool call, not a `reflect` call; the real-model round trip is TASK-024's
    end-to-end check.

## Implementation plan

> ⚠ Acceptance criteria question: criterion 1 asks for "a tool-result for `ask_human`", but that `tool_use` already
> got its `tool_result` ("Question sent (decision <id>)…") before the run stopped; a second result for one id is
> invalid for Anthropic and OpenAI-style APIs, and mixing a text block into the tool-result turn throws in
> `LlmProviderBase` (Framework/Birko.AI/Providers/LlmProviderBase.cs ~283). Rewriting the saved result would be
> valid but falsifies history, and there is none to rewrite on the basic path or a fresh start. Recommendation: every
> answer arrives in the user turn that resumes the conversation, naming the decision id, the question, and that a
> person wrote it — `Agent.ContinueWithOutcomeAsync` already appends that turn after a trailing tool-result turn.

### Settled in the grill (2026-11-16)

1. Every answer arrives as a tagged user message (`[Human answer — decision <id>]`) in the turn that resumes the
   conversation, never as a tool result; criterion 1 reworded.
2. No stored "delivered" marker: a resumed run skips an answer whose tag is already in its restored conversation; a
   fresh start lists every answer the task has had.
3. A run that asked on step 0 resumes from its checkpoint when an answer is waiting.
4. A timed-out escalation is told to the agent too ("no answer, timed out … handled automatically: …").
5. When answers are delivered, the resume message reads "Your task was paused for a person's decision. Continue
   from step N…"; otherwise it is unchanged.

### How the resume finds what to deliver

- Drake loads `GetResolvedForTaskAsync(task.Id)` (reversed: oldest first) and hands the list to the Kobold.
- **No stored "delivered" marker.** Each synthetic message opens with the tag `[Human answer — decision <id>]`. On a
  checkpoint resume the Kobold skips any decision whose tag is already in a user text message of the restored
  conversation (match the tag, not the bare id, which the `ask_human` result also contains). With no conversation
  (basic path, no checkpoint) every answer goes in, since a fresh prompt has no memory. "Delivered" thus means
  "present in what was saved"; a crash before the checkpoint re-delivers rather than loses. Rejected: a `DeliveredAt`
  column — needs schema drift handling and a write point, and would wrongly suppress answers on the basic path.

### Steps

1. **Model** `Models/Tasks/HumanAnswer.cs`: `record HumanAnswer(string DecisionId, PendingDecisionSource Source,
   string Question, string? Answer, bool TimedOut, string? AutomaticOutcome)` with `From(PendingDecision)` (question
   via `PendingDecisionNotification.QuestionText`; `AutomaticOutcome` = `Escalation?.Resolution` for a timeout or
   "Handle it automatically") and `ToMessage()`, which owns all wording (step 5).
2. **Drake:** in `SummonKoboldAsync` beside `OnAskHuman` (~800), `kobold.HumanAnswers = await
   LoadHumanAnswersAsync(task.Id)` — defensive like `LatestResolvedAsync`; empty without a store. The parallel path's
   `SummonKoboldAsync` (~2270) gets it too.
3. **Enhanced path** (`RunPlanStepsAsync`, Kobold.cs ~1310–1345):
   - **Step-0 gap:** `LoadResumableCheckpointAsync` returns null when `plan.CurrentStepIndex == 0`, so a question asked
     on the first step loses its conversation. Relax to `CurrentStepIndex == 0 && HumanAnswers.Count == 0`.
   - With a checkpoint: undelivered = answers whose tag is not in the restored conversation; if any, the resume
     message is the answer blocks plus "Your task was paused for a person's decision. Continue from step N of M; do
     not redo completed steps." (replacing "The server was restarted"); otherwise today's message.
   - Without one: the answer blocks are prepended to `initialPrompt`.
4. **Basic path** (`StartWorkingWithPlanAsync` ~913): every answer prepended to `fullTaskPrompt` under "Decisions
   made by a person for this task".
5. **Wording** — always as a person, never as a tool; each block opens with the tag:
   - `ask_human` answered: `A person answered your ask_human question "<q>". Their answer: <answer>` — `Answer` is
     already the option's exact text or verbatim free text (TASK-021's `Accepts`), so nothing is reformatted.
   - `ask_human` timed out: `Nobody answered your ask_human question "<q>" — no answer, timed out. Decide yourself,
     record the assumption with reflect, and continue.`
   - Escalation answered with instructions: `A person reviewed your escalation (<type>: <summary>). Their
     instructions: <answer>` — a bare "Resume with my instructions" becomes "Resume; no further instructions given."
   - Escalation timed out: `Nobody answered your escalation (<type>) — no answer, timed out. It was handled
     automatically: <AutomaticOutcome>.` (its automatic route may have revised the plan; silence would be the "as if
     answered" the last criterion forbids)
   - "Handle it automatically": `A person chose to have it handled automatically: <AutomaticOutcome>.`
6. **Docs:** CLAUDE.md's "ask_human" and "Escalation to a person" bullets — answers return as a tagged user message on
   resume; a step-0 question resumes from its checkpoint.

### Tests (failing first)

- **KoboldRunHooksTests** (ScriptedProvider; a real `KoboldPlanService` on the temp dir so a checkpoint exists):
  run 1 asks with `["sqlite","postgres"]` and stops; run 2, a fresh Kobold with `HumanAnswers = [answered
  "postgres"]`: `Requests[0]` holds the `ask-1` tool_result then a user message with the tag and "postgres", no
  duplicate tool_result; the script touches `postgres.txt` only when the request says "postgres", so it exists and
  `sqlite.txt` does not. Also: a step-0 resume uses the checkpoint; a timeout shows "no answer — timed out" and no
  "Their answer"; delivered once (resuming run 2's checkpoint with the same answers, the tag appears once); the basic
  path's first prompt carries the answer; no answers → today's resume message.
- **EscalationToHumanTests:** answering then `SummonKoboldAsync` → `kobold.HumanAnswers` holds the option's text; free
  text verbatim with its whitespace; a timeout → `TimedOut` with no answer; an escalation's instructions carried; an
  open decision not handed over; several answers oldest first.
- Mutations: break the tag filter, the step-0 relaxation and the TimedOut branch — each must fail a test.

### Tradeoffs

- A user message, not a tool result: one channel for every path and provider; STORY-029 reuses it unchanged.
- No stored marker: no schema change, crash-safe; the basic path and fresh starts re-list all of the task's past
  answers each time — bounded by the question cap, and history the agent should have anyway.

### Risks

- Two user turns in a row (the tool-result turn, then text): Anthropic merges them, OpenAI-style APIs take tool
  messages then a user message, and today's resume already sends this shape; Gemini unverified.
- Parallel path: every batch Kobold gets the answers and they share one checkpoint (TASK-171's area).
- A later context-compaction could drop a tagged message and a restart would re-deliver it; nothing compacts today.
- A checkpoint keeps the last 50 messages, so an answer older than that is delivered again on resume: harmless
  repetition, never a loss.

### Implementation notes (2026-11-16)

- What the automatic route did was not stored: the decision kept the escalation as it was parked. Drake now saves the
  escalation after its automatic route (`SqlPendingDecisionRepository.RecordEscalationOutcomeAsync`), so a timeout and
  "Handle it automatically" can name the outcome.
- Answers are read before the Kobold is created, and an unreadable store summons nothing: the task waits for a later
  cycle instead of running without the person's answer.
- Flaky once seen: `EscalationToHumanTests.A_summoned_Kobold_hears_the_tasks_answers_oldest_first_and_not_an_open_question`
  failed in 2 of the first 7 full-suite runs (the Kobold had no answers although they were readable just before),
  never alone, and not in the 7 runs since. The read never threw under logging. Unexplained; one candidate is Birko
  reading a missing table as empty by design (a file that is not a database does throw, which the held-task test
  uses). → TASK-172.
- A Kobold summoned in the instant between a timed-out escalation's automatic route and its stored outcome hears the
  timeout without the outcome. Left as it is: the gap is one database write, far inside a Drake cycle (close review,
  2026-11-17).
