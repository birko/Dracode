---
id: TASK-021
parent: STORY-025
feature: FEATURE-028
status: done
blocked: 'merge deferred: stacked on task/TASK-020, which waits on its end-to-end check at TASK-024; code complete on task/TASK-021'
priority: P1
assignee: agent
created: 2026-05-29
depends-on: [TASK-019, TASK-020]
blocks: [TASK-022, TASK-170, TASK-171]
pr: null
github-issue: null
jira-key: null
---

# Implement async `ask_human` tool

> Blocked 2026-11-16 — merge deferred: stacked on task/TASK-020, which waits on its end-to-end check at TASK-024; code
> complete on task/TASK-021. `/tasks unblock TASK-021` and re-close once TASK-020 has merged.

## Context

The interactive `AskUserTool` is wired only for the Dragon CLI (`DraCode/Program.cs:508`) and blocks on a console prompt — unusable in background agents. Build a non-blocking `ask_human` tool for automatic agents (Kobold, Wyvern) that posts a structured question and parks the task using the TASK-019 plumbing. The tool follows the project's tool conventions (async `ExecuteAsync` only — `Tool` base is async-only as of TASK-018; docs live on the `Tool` class per the ACI principle).

## Acceptance criteria

- [x] New `AskHumanTool` in `DraCode.KoboldLair/Agents/Tools/`, `ExecuteAsync`-only, with self-describing tool docs.
- [x] Parameters: `question` (string), `options` (string[]?, optional choices), `allow_free_text` (bool), `context` (string?, why it's asking).
- [x] Execution writes a `PendingDecision` (intent = question, not escalation) and parks the calling task (reuses TASK-019 mechanism).
- [x] Registered in the Kobold tool catalogue; **not** for Wyvern (deferred to TASK-170, FEATURE-028 D4 changed 2026-11-16) or Wyrm.
- [x] Gated by policy hooks (`allowAskHuman`, `maxQuestionsPerTask`) — read defensively so it still works before TASK-023 lands (default allow, no cap).
- [x] Answer with `options` set is validated against the options; free-text passes through verbatim.
- [x] Tool call + pending decision captured in the plan execution log.

## Out of scope

- Delivering the answer back into agent context on resume — TASK-022.
- Dragon-side rendering/answering — TASK-024.
- Policy enforcement implementation — TASK-023 (only the hooks are read here).
- Deferred to TASK-171 — a question in parallel mode, or in a turn that also finishes the plan or uses the last
  iteration.
- The basic plan path saves no conversation, so TASK-022 delivers the answer there without the asking turn: its own
  plan's fallback.

## Human test plan

- [x] Have a Kobold call `ask_human` with two options; confirm the task parks and the question is persisted with its options.
  - **Passed 2026-11-16:** automated — `KoboldRunHooksTests.Asking_ends_the_run_after_the_turn_with_the_question_and_its_result_saved` (a Kobold run calls the tool) and `EscalationToHumanTests.Asking_parks_the_task_on_a_question_without_cancelling_the_run` (the task parks; the stored decision and the card carry the options) green; the run stop proved red with its hook check removed
- [x] Confirm Wyrm has no access to the tool.
  - **Passed 2026-11-16:** automated — `EscalationToHumanTests.The_analyzer_and_pre_analysis_agents_have_no_ask_human_tool` green (Wyvern and Wyrm)

## Implementation plan

> Reading: there is no separate "Kobold tool catalogue" in the code; it is the per-run tool injection in `Kobold.cs`
> where `reflect` and `update_plan_step` are added. The plan treats that as the catalogue.

### Settled in the grill (owner, 2026-11-16)

- **`ask_human` stops the run at the next turn boundary**, not at once; escalations still stop it at once. The
  tool's description says to make it the turn's last call. Birko's `OnToolCallStartingAsync` can only observe or
  throw, so a later call in the same turn cannot be skipped; deferring the stop is what keeps the question and its
  result in the saved conversation for TASK-022.
- **A refused question** (policy disallows, or the per-task cap is reached) answers "Not asked — decide yourself,
  record the assumption with reflect, continue", and the run goes on.
- **No timeout on a question** until TASK-023 supplies one; it waits until answered.
- **`ResolvePendingDecisionAsync` returns a result** — `Resolved`, `NotFound`, `AlreadyResolved`, `InvalidAnswer` —
  instead of a bool, changed now while TASK-020 is unmerged, so TASK-024 can tell the person an answer is not one of
  the options. `DrakeFactory.ResolvePendingDecisionAsync` returns the same.
- **The cap counts every question the task ever asked**, answered ones included.

### Established from the code

- **How the tool reaches Drake:** like `ReflectionTool`, through a callback the Kobold carries — but `OnEscalation`
  is fire-and-forget, and `ask_human` must know whether the task was parked, so the callback returns:
  `Func<AskHumanQuestion, CancellationToken, Task<AskHumanOutcome>>`. Drake wires it in `SummonKoboldAsync` beside
  `OnEscalation` (Drake.cs:781), so the parallel path's Kobolds get it too.
- **Why the run must not be cancelled inside the tool:** `ParkTaskAsync` cancels the run at once, and Birko's
  `HandleToolUse` (Framework/Birko.AI/Agents/Agent.cs:425–427) checks cancellation before every tool call of a batch.
  If `ask_human` is not the turn's last call, the next one throws before the turn's tool results reach the
  conversation; `ConsistentConversation()` then cuts before the assistant turn, and the `ask_human` call and its
  result are lost — the pair TASK-022 needs to answer.
- **So cancel at the next iteration boundary:** `ParkTaskAsync(task, decision, bool cancelRun = true)` with the
  cancel moved into `CancelRun(taskId)`. The `ask_human` handler parks with `cancelRun: false`; the tool returns its
  result and sets `kobold.StopAfterToolTurn`. `RunHooks.OnIterationStartingAsync` — right after
  `_consistentLength = context.Conversation.Count`, before the `_detectSteps` early return — calls the Drake-wired
  `kobold.CancelRun()` when the flag is set. Birko throws on cancellation right after the hook (Agent.cs:244); the
  existing cancellation handler saves the plan and a checkpoint whose conversation ends with the `ask_human` result,
  the Kobold stays `Working`, the plan step stays `InProgress`; `ExecuteTaskAsync`'s `finally` unsummons, the
  worktree is kept (the task is parked) and `SyncTaskFromKoboldAsync` leaves the task alone. Not `context.Stop()`:
  a stopped run continues into `UpdatePlanStatusAsync(success: true)`.
- **Limits:** only the enhanced path saves the conversation; the basic path saves the plan alone
  (`conversation: null`), so TASK-022 falls back to a user message there. A question on the last allowed iteration
  ends the run with the task parked.
- **Plan path only, like `reflect`:** added only where `ImplementationPlan != null` — `StartWorkingWithPlanAsync`
  (867) and `StartWorkingWithPlanEnhancedAsync` (1100) — and removed in both `finally` blocks. The non-plan path gets
  no token and no `ask_human`, so TASK-020's out-of-scope note holds.

### Steps

1. **Payload** `Models/Tasks/AskHumanQuestion.cs`: record `Question`, `Options` (string[]), `AllowFreeText`,
   `Context`, stored as camelCase JSON in `PendingDecision.Question` (already a JSON blob; no schema change);
   `TryParse(PendingDecision)`; `Accepts(answer, out stored)` — with options, an answer equal to an option after trim
   (ordinal) is stored as the option's text; anything else is refused unless `AllowFreeText`, then stored verbatim; no
   options forces free text; an empty answer is refused.
2. **Card** (`PendingDecisionNotification`): `ResponseSchemaFor` for `AskHuman` → `choice` with the stored options
   and `allowFreeText`, or `text` with none. The card's `question` metadata gets the plain question, plus a new
   `context` key. `QuestionText(decision)`, used at `Program.cs:491` — which today passes `decision.Question`, so the
   raw JSON would reach the card.
3. **Policy hooks** `Models/Tasks/AskHumanPolicy.cs`: `IAskHumanPolicy { AskHumanLimits For(string projectId); }`,
   `record AskHumanLimits(bool Allow = true, int? MaxQuestionsPerTask = null)`, default `UnrestrictedAskHumanPolicy`;
   `DrakeFactory.AskHumanPolicy` → optional `Drake` argument, like `DispositionPolicy`. Read defensively: a throwing
   policy is logged and read as allow, no cap. TASK-023 implements it from `projects.json`.
4. **Repository:** `CountForTaskAsync(taskId, PendingDecisionSource)` (answered and open). `maxQuestionsPerTask`
   counts every `AskHuman` decision ever stored for the task.
5. **`Drake.AskHumanAsync(TaskRecord, AskHumanQuestion) → AskHumanOutcome(Parked, DecisionId?, Refusal?)`:** refuses
   when the policy disallows, the count is at or over the cap, there is no store, or the task is already parked;
   otherwise `PendingDecision { Source = AskHuman, Disposition = HumanDecisionRequired, Question = json,
   Timeout = null }` and `ParkTaskAsync(..., cancelRun: false)`. The timeout stays null until TASK-023 supplies one.
6. **`AskHumanTool`** (`Agents/Tools/AskHumanTool.cs`, modelled on `ReflectionTool`): constructor takes plan, plan
   service, logger, project/task ids, Kobold id, the callback and a stop action (`kobold.StopAfterToolTurn = true`).
   The description documents the four parameters and says the task pauses until a person answers, the answer arrives
   when work resumes, and the tool should be the turn's last call. `InputSchema`: `question` required; `options`
   string array; `allow_free_text` boolean (default true without options); `context`. `JsonElement`-tolerant parsing.
   Invalid input (empty question, `allow_free_text: false` with no options, duplicate options) → `Error:` without the
   callback. Plan log: `ask_human: "<question>" [a | b] — <context>` before the callback, then
   `→ decision <id>, task parked` or `→ not asked: <refusal>`, saved with `SavePlanAsync`. A refusal returns "Not
   asked: … Decide yourself, record the assumption with reflect, continue." and does not stop the run.
7. **Kobold wiring:** `OnAskHuman` and `CancelRun` properties set by Drake; the tool added and removed beside
   `reflect` in both plan paths, only when `OnAskHuman != null`; `StopAfterToolTurn` reset at each run start.
8. **Answer validation (D5) and the result type:** `enum DecisionAnswerResult { Resolved, NotFound,
   AlreadyResolved, InvalidAnswer }` returned by `Drake.ResolvePendingDecisionAsync` and
   `DrakeFactory.ResolvePendingDecisionAsync` (TASK-020's tests move to it). After loading, before `ResolveAsync`: an
   `AskHuman` decision must pass `Accepts`, else `InvalidAnswer` with the decision left open; resolve with the stored
   form. The same check in the factory's no-Drake path. Escalation answers unchanged.
9. **CLAUDE.md:** an "ask_human" bullet beside "Escalation to a person": Kobold plan path only, the payload, the
   policy seam, the cancel at the next iteration boundary.

### Tests (failing first)

- **`Agents/AskHumanToolTests`** (fake callback): arguments reach the callback; both plan-log entries; a refusal
  returns "Not asked" without the stop action; invalid input never calls the callback; the `allow_free_text` default.
- **`EscalationToHumanTests`** (its Drake harness): `AskHumanAsync` parks with `Source = AskHuman`, the card's schema
  lists the options, the run is not cancelled; cap 1 → a second question refused; disallowed → nothing stored; a
  throwing policy still allows; no store → refused; an invalid choice → false and still open, then a valid choice
  accepted; free text stored verbatim with its whitespace; the same refusal through the factory's no-Drake path.
- **`KoboldRunHooksTests`** (ScriptedProvider): a turn of `ask_human` then `touch` → the run ends before the next
  model call, the saved checkpoint ends with the `ask_human` result, the plan step stays `InProgress`; a run without
  a plan has no `ask_human`.
- **Wyvern / Wyrm:** build both, assert no tool named `ask_human`.

### Tradeoffs

- The deferred cancel bends TASK-020's "every park stops the run at once": tool calls after `ask_human` in the same
  turn still run. In return TASK-022 always finds the question and its result in the checkpoint. Escalations still
  cancel at once.
- Policy read at call time, not at injection: changes apply without restarting the Kobold; the model may try and get
  "not asked".

### Risks

- On the parallel path all batch Kobolds share one task token and one checkpoint: parking cancels siblings
  mid-call, and the last checkpoint written wins.
- A model looping on refused questions spends iterations; the refusal text tells it to stop.
- A cap counted from stored decisions only works on the SQL backend — the only one where asking works at all.

## Outcome

Code complete 2026-11-16; both human-test steps passed by automated tests. Merge deferred: stacked on task/TASK-020
(parked at verify until the end-to-end check at TASK-024). Suite 403/403 (373 before; 30 new across
`AskHumanToolTests`, `EscalationToHumanTests` and `KoboldRunHooksTests`), red first, with mutations proving the
turn-boundary stop and answer validation.

Review gate (2026-11-16) — one verdict per pass:

- **Standards:** CLAUDE.md's Kobold tool list now names `ask_human` (fixed).
- **Intent:** every criterion met; beyond them only what the grill settled (the turn-boundary stop, the answer
  result type) and the card's plain question text.
- **Correctness:** no blockers. Fixed, red first: the turn-boundary stop went through Drake's per-task map, so a run
  that asked could cancel a newer run of the same task (or nothing, if Drake had not wired it) — each plan run now
  links its own stop and the hook stops only that run; the basic plan path now stops too (only the enhanced path
  saves the conversation, as CLAUDE.md now says); a free-text "Handle it automatically" to a question is recorded as
  an answer, not mistaken for an escalation's choice. Filed: TASK-171 (parallel mode; a turn that also finishes the
  plan or uses the last iteration). Kept: the cap's count-then-park race is harmless (a second park is refused under
  the lock).
- **Security:** fixed: the question, options and context are limited in size, and the plan log entry is one line.
  Tenancy holds; no answer endpoint yet.
- **Comments:** a misplaced XML doc in `Kobold.cs`, stale "False when…" docs, the `ParkTaskAsync` summary and a
  duplicated comment fixed; the rest hold content that lives nowhere else.

Closed 2026-11-19: merged with `main` (TASK-020, TASK-174); suite 410/410.
