---
id: TASK-024
parent: STORY-027
feature: FEATURE-030
status: done
priority: P2
assignee: agent
created: 2026-05-29
depends-on: [TASK-020, TASK-022]
blocks: [TASK-160, TASK-025]
pr: 5
github-issue: null
jira-key: null
---

# Answerable notifications + Dragon actionable prompt round-trip

## Context

Pending decisions (TASK-020) and `ask_human` questions (TASK-021) park tasks, but a human can only resolve them once Dragon can *show and collect* the answer. Extend the notification model with a response schema and render an actionable prompt in the Dragon client, wiring the submitted answer back through the resolution callback (which TASK-022 delivers into agent context).

## Acceptance criteria

- [x] A decision notification carries `requiresResponse` + `responseSchema` (choice options / free-text shape). (Reworded 2026-11-17: built in TASK-020 as notification metadata, `PendingDecisionNotification`, rather than new fields.)
- [x] A `requiresResponse` notification is emitted when a task parks / an agent asks, pushed live via the existing `OnNotification` → WebSocket path to every Dragon session whose user can see the project (FEATURE-030 D2, changed 2026-11-17).
- [x] `/dragon` takes an answer to a decision (`answer_decision`) and replies with what became of it, the channel the decision cards use. (Reworded 2026-11-17, FEATURE-030 D3 changed: rendering the card moved to TASK-160, since today's web Dragon view is a placeholder being rebuilt.)
- [x] Submitting the answer calls the resolution callback → unparks the task (verified end-to-end with TASK-022).
- [x] A tool to list + answer pending decisions explicitly, for missed live prompts, limited to the projects the session can see (a new `pending_decisions` tool for Warden, decided 2026-11-17).
- [x] Idempotent resolution: answering an already-resolved decision is a no-op with a clear message.
- [x] An open decision is replayed on every reconnect until answered or timed out, from the stored decision rather than the notification list (FEATURE-030 D6, changed 2026-11-17).

## Outcome

Open decisions reach a person and come back from them. On `client_ready` the `/dragon` socket sends a card for each
open decision of the user's projects, read from the decision store, so an unanswered one returns on every connect
until answered or timed out; a new card goes to every session that can see its project. `answer_decision` answers it
and `decision_answer_result` says in words what became of it, once; another user's decision reads as unknown. Warden's
`pending_decisions` tool lists and answers decisions from the chat. 16 new tests; 433/433.

## Out of scope

- Policy editing UI — TASK-023 covers the policy model.
- Non-Dragon clients (EPIC-013 territory).

## Human test plan

- [x] Trigger a parked decision while Dragon is connected; confirm the decision reaches the session and answering it unparks and resumes the task. (Until TASK-160 renders cards, through the Dragon chat or a WebSocket client.)
  - **Behaviour passed 2026-11-17, not ticked:** scripted run by the agent against a real, throwaway server (`task/TASK-025` `c3d2c8f`, Kestrel, Testing environment, scratch DB, JWT on), with the socket opened by a token in a header. A real client sends `?token=`, which Kestrel refuses with 401 → TASK-174; re-run this step through the query path once that lands, then tick it. An `ask_human` park in a live LAN-model run reached the already-open session as a `decision_required` card within seconds; answering resolved it, and the task resumed with the answer and finished Done.
  - **Passed 2026-11-19 through `?token=`:** scripted run against a real, throwaway server (`task/TASK-024` merged with `main` incl. TASK-174, Kestrel, Testing environment, scratch DB, JWT on, LAN `finstat` model), every socket opened with the token in the query. A Kobold's `ask_human` card reached the open session live; answered over a second `?token=` socket → `Resolved`; the Kobold "resumed with conversation checkpoint (… 1 new answer(s))" and all three tasks finished Done. The scripted answer "Slovak" was first refused as `InvalidAnswer` because the options were whole phrases (TASK-176, filed).

- [x] Disconnect Dragon, trigger a decision, reconnect; confirm the prompt is replayed.
  - **Behaviour passed 2026-11-17, not ticked:** scripted run by the agent against a real, throwaway server (`task/TASK-025` `c3d2c8f`, Kestrel, Testing environment, scratch DB, JWT on), with the socket opened by a token in a header. A real client sends `?token=`, which Kestrel refuses with 401 → TASK-174; re-run this step through the query path once that lands, then tick it. With no session connected a decision was stored; on reconnect + `client_ready` its card arrived (options in `responseSchema`), and again on the next connect.
  - **Passed 2026-11-19 through `?token=`:** same server, `?token=` sockets only: no card before a decision exists; a decision stored while disconnected arrived on reconnect + `client_ready` with its options, and again on the next connect.

- [x] Answer the same decision twice; confirm the second attempt is a clean no-op.
  - **Behaviour passed 2026-11-17, not ticked:** scripted run by the agent against a real, throwaway server (`task/TASK-025` `c3d2c8f`, Kestrel, Testing environment, scratch DB, JWT on), with the socket opened by a token in a header. A real client sends `?token=`, which Kestrel refuses with 401 → TASK-174; re-run this step through the query path once that lands, then tick it. `MySQL` (not an option) → `InvalidAnswer`, question still open; `Postgres` → `Resolved`; `SQLite` → `AlreadyResolved`, "nothing changed"; stored answer stayed `Postgres`, and the card was not offered again.
  - **Passed 2026-11-19 through `?token=`:** same server: `MySQL` → `InvalidAnswer` (question still open); `Postgres` → `Resolved`; `SQLite` → `AlreadyResolved`, "nothing changed"; stored answer stayed `Postgres`; the card was not offered again.

## Implementation plan

> Settled 2026-11-17 (grill): criterion 3 asked the Dragon client to render the prompt, but the web client's
> Dragon view (`DraCode.KoboldLair.Client/src/views/dragon-view.ts`) is a simulation that never connects to `/dragon`
> (TASK-142, cancelled, was to wire it), and TASK-160 already owns decision cards in the rebuilt Dragon dock
> (TASK-151). This task builds the server side and the answer channel the cards will use; rendering moved
> to TASK-160. Until then a person answers in the Dragon chat, through the tool in step 4. Also settled: replay from
> the store (step 1), push to every session that can see the project (step 3), a new Warden tool (step 4).

### What already exists (TASK-020, TASK-021)

- Criteria 1–2 are mostly built: a park raises a `decision_required` notification whose metadata carries
  `requiresResponse`, `decisionId`, the question, its context and a `responseSchema` (`PendingDecisionNotification`),
  pushed live through `ProjectNotificationService.OnNotification` → `DragonService` as `project_notification`. An
  answer or timeout raises `decision_resolved` and retires the card.
- `DrakeFactory.ResolvePendingDecisionAsync(projectId, decisionId, answer)` returns `Resolved`, `NotFound`,
  `AlreadyResolved` or `InvalidAnswer`, and works with or without a live Drake.

### Gaps

- **Nothing can answer from a client:** `/dragon` has no message that resolves a decision, and no Dragon tool does.
- **Replay is lossy:** `SendPendingNotificationsAsync` marks every notification read once sent, so an unanswered card
  replays on one reconnect only; `view_notifications dismiss` also deletes it while the decision is still open.
- **Live push reaches only sessions viewing that project** (`OnProjectNotificationReceived` matches the session's
  current project folder).

### Steps

1. **Cards from the store.** On `client_ready`, send one `decision_required` card per
   unresolved decision of the projects the session can see (`GetVisibleProjects`, read from
   `SqlPendingDecisionRepository.GetUnresolvedForProjectAsync`), built by `PendingDecisionNotification.ToMetadata`. The
   store is the source of truth, so a card returns on every connect until answered, whatever happened to the
   notification list. Skip `decision_required` in `SendPendingNotificationsAsync` so it is not sent twice.
2. **Answer over `/dragon`:** a new message `{ type: "answer_decision", decisionId, answer }`. The session must see the
   decision's project (else `NotFound`, never a hint that it exists). It calls
   `DrakeFactory.ResolvePendingDecisionAsync` and replies `{ type: "decision_answer_result", decisionId, result,
   message }`, `message` saying what happened in words: answered and the task resumes; already answered or timed out
   (no-op); not one of the options (lists them); unknown decision. `DragonMessage` gains `DecisionId` and `Answer`.
3. **Live push** of `decision_required` / `decision_resolved` to every session whose user can see the project, not
   only the one viewing it.
4. **Tool `pending_decisions`** for Warden (actions `list`, `answer`), scoped to the session's visible projects,
   with the same result wording as step 2. Warden's prompt gets one line: when the user answers a decision in chat,
   answer it with this tool.
5. **Docs:** CLAUDE.md (Decision cards bullet, the Dragon tools list, the WebSocket message types).

### Tests (failing first)

- **DragonService / WebSocket** (`WebApplicationFactory` on SQLite, like `DragonWebSocketAuthTests`): a parked task's
  card arrives on connect, and again on a second connect while unanswered, and not after it is answered;
  `answer_decision` resolves it and unparks the task; a second answer gets `AlreadyResolved` with a clear message and
  changes nothing; an answer outside the options gets `InvalidAnswer` and the question stays open; another user's
  decision gets `NotFound`.
- **End-to-end (signs off TASK-020 / TASK-022):** park through `ask_human`, answer through `/dragon`, summon: the Kobold
  hears the answer (`HumanAnswers`) and resumes from its checkpoint.
- **Tool:** list shows only visible projects' open decisions; answer returns each result's wording.
- Mutations: the store-sourced replay, the visibility check.

### Risks

- A Dragon session needs an LLM provider before `client_ready`; the WebSocket tests may need a stub provider or to
  send the decision messages before the welcome. Measured when the first test runs.
- Without a web client that speaks `/dragon`, the human test plan waits for TASK-151/TASK-160, or runs against a
  WebSocket script.

### Implementation notes (2026-11-17)

- `DecisionDesk` (core) holds the logic both channels share: open decisions of a set of projects, and an answer
  checked against them before `DrakeFactory.ResolvePendingDecisionAsync`. `DragonService` and `PendingDecisionsTool`
  only adapt it, so the rules are tested once without a socket.
- Cards replay on `client_ready` only, not on a project load: a card covers every project the user can see, so a
  load adds nothing.
- No guard on `view_notifications dismiss`: with cards read from the store, dismissing a notification can no longer
  hide an open decision, so the planned guard had nothing left to protect.
- The `/dragon` socket handles one message at a time, so an `answer_decision` sent right after `client_ready` waits
  for Dragon's welcome to be written. With a model that answers, that is seconds; with one that does not, the welcome's
  retries hold the answer for about 40 s (measured in the socket tests, which therefore answer from a client that
  skips `client_ready`).
- Not covered by a test: `SendPendingNotificationsAsync` skipping `decision_required` (so a card is not sent twice).
  It runs only after a welcome that reached a model, which the tests have none of.
- Dragon routes an answer to Warden through one added phrase in its council description; whether a real model routes
  it is the human test plan's to see.

Closed 2026-11-19: all three human-test steps passed through `?token=` after TASK-174; suite 440/440 merged with `main`.
