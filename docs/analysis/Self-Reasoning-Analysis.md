# Analysis: Self-Reasoning Improvements for DraCode Agents

## Executive Summary

DraCode has excellent structural foundations (plans, validation, error classification, learning context) but operates in a **plan-execution loop** rather than a **reasoning-evaluation-adjustment cycle**. Adding self-reflection could transform it from task automation to genuinely adaptive multi-agent intelligence.

---

## Current State: What Agents Do Well

| Component | Current Capability |
|-----------|-------------------|
| **Error Classification** | Distinguishes transient vs permanent errors |
| **Plan Execution** | Step-by-step with file validation |
| **Learning Context** | Past task metrics provided to planner |
| **Recovery** | Circuit breaker, WAL, exponential backoff |

---

## Key Gaps Where Self-Reasoning Would Help

### 1. No Mid-Execution Strategy Reconsideration

**Current**: Agents commit to plans and don't reconsider if circumstances change.

**Example**: Kobold follows plan blindly even when step 3 becomes impossible due to step 2's decisions.

**With Self-Reasoning**:
```
"After 5 iterations on this step, am I stuck? My initial approach assumed X,
but I've discovered Y. Should I modify my approach before exhausting retries?"
```

### 2. No Error Root-Cause Analysis

**Current**: `Error → classify → retry` (procedural)

**Missing**: Agent explaining *why* the error occurred.

**With Self-Reasoning**:
```
"This syntax error happened because I assumed ES6 imports, but the project uses
CommonJS. I should check module type before generating imports."
```

### 3. No Plan Feasibility Re-Evaluation

**Current**: 8-step plan created upfront, executed regardless of discoveries.

**With Self-Reasoning**:
```
"Step 4 assumes auth module exists, but step 2 revealed it doesn't.
Remaining steps need adjustment before I continue."
```

### 4. No Uncertainty Estimation

**Current**: All tasks treated with equal confidence.

**With Self-Reasoning**:
```
"I'm 80% confident in steps 1-3 (familiar patterns), but 40% confident in
step 5 (novel integration). I should allocate more iterations to step 5."
```

### 5. No Iteration Budget Self-Management

**Current**: Max iterations per step, but agent doesn't pace itself.

**With Self-Reasoning**:
```
"I've used 15 of 20 iterations on step 2 of 8. At this rate I won't finish.
Should I simplify or request more budget?"
```

---

## Proposed Self-Reasoning Mechanisms

### A. Reasoning Checkpoints (Kobold)

Add explicit "pause and reflect" every 3-5 iterations:

```
CHECKPOINT: Am I making progress?
- Current step: Create user authentication service
- Files created so far: auth.service.ts (partial)
- Progress assessment: 40% - basic structure done, need JWT logic
- Blockers: None detected
- Confidence: 70% - familiar pattern but new token library
- Decision: Continue current approach, 3 more iterations estimated
```

### B. Error Explanation Framework

Before retry, agent reasons about failure:

```
ERROR ANALYSIS:
- What happened: Network timeout calling OpenAI API
- Category: Transient (external)
- Root cause: High API load (not my fault)
- Strategy adjustment: Wait longer before retry, no code changes needed

vs.

ERROR ANALYSIS:
- What happened: TypeScript compilation error
- Category: Permanent (code issue)
- Root cause: Used wrong import syntax for ESM project
- Strategy adjustment: Check tsconfig.json module setting, use correct import
```

### C. Plan Feasibility Re-Evaluation (Drake/Kobold)

At each step boundary:

```
PLAN CHECK (Step 3 of 8):
- Assumption: database.ts exports `getConnection()`
- Reality: It exports `createPool()` with different signature
- Impact: Steps 4-6 depend on this assumption
- Options:
  1. Modify steps 4-6 to use createPool()
  2. Create adapter function in step 3
- Decision: Option 1 - simpler, fewer new files
```

### D. Success Criteria Self-Assessment

Before marking step complete:

```
COMPLETION CHECK:
- Step goal: "Create login form with validation"
- Files created: login.tsx ✓
- Structural check: Component exports correctly ✓
- Functional check:
  - Form renders? ✓ (JSX valid)
  - Validation works? ❓ (No tests, medium confidence)
  - Styling applied? ✓ (Tailwind classes present)
- Overall confidence: 75%
- Decision: Mark complete, flag for integration testing
```

---

## Implementation Priority

| Priority | Enhancement | Agent | Status |
|----------|-------------|-------|--------|
| **P1** | Iteration checkpoints | Kobold | ✅ COMPLETED (2026-02-12) |
| **P1** | Error explanation | Kobold | ✅ COMPLETED (2026-02-12) |
| **P2** | Plan re-evaluation | Kobold | ✅ COMPLETED (2026-03-15) via `RevisePlanAsync` |
| **P2** | Success self-assessment | Kobold | ✅ COMPLETED (2026-03-15) via `reflect` tool |
| **P3** | Uncertainty estimation | Planner | ✅ COMPLETED (2026-03-15) via `reflect` tool `confidence_percent` |
| **P3** | Workspace conflict reasoning | Drake | ⏳ Future |

---

## Concrete Implementation Approaches

### Option 1: Prompt-Based Self-Reflection ✅ COMPLETED (2026-02-12)

**Location**: `DraCode.KoboldLair/Models/Agents/Kobold.cs`

Implemented in `BuildFullPromptWithPlanAsync()`:

1. **SELF-REFLECTION PROTOCOL** (lines 1070-1082)
   - CHECKPOINT block format with Progress, Files done, Blockers, Confidence, Decision

2. **ERROR HANDLING PROTOCOL** (lines 1084-1093)
   - ERROR ANALYSIS block for root-cause reasoning

3. **Checkpoint Injection** (lines 810-835)
   - Runtime injection every N iterations (configurable via `CheckpointInterval`)

**Pros**: Simple, no code changes beyond prompts
**Cons**: LLM may ignore, inconsistent formatting

### Option 2: Structured Reflection Tool ✅ COMPLETED (2026-03-15)

**Location**: `DraCode.KoboldLair/Agents/Tools/ReflectionTool.cs`

The `reflect` tool forces Kobolds to produce structured self-assessments that drive automated intervention:

- **Captures**: `progress_percent`, `blockers`, `confidence_percent`, `decision` (continue/pivot/escalate), `escalation_type`
- **Stall detection**: Auto-escalates if the last N reflections show no progress change
- **Low confidence**: Auto-escalates if `confidence_percent < 30%` (threshold configurable)
- **Data storage**: Reflections appended to `KoboldImplementationPlan.Reflections` list; escalation alerts stored in `KoboldImplementationPlan.Escalations` list
- **Decision routing**: `pivot` triggers `KoboldPlannerAgent.RevisePlanAsync`; `escalate` creates an `EscalationAlert` routed through Drake

**Pros**: Structured data, triggers automated responses, feeds into monitoring
**Cons**: Adds tool call overhead per checkpoint

### Option 3: External Reasoning Monitor ✅ COMPLETED (2026-03-15)

**Location**: `DraCode.KoboldLair.Server/Services/ReasoningMonitorService.cs`

`ReasoningMonitorService` extends `PeriodicBackgroundService` (configurable interval, default 45s) and observes Kobold behavior without requiring LLM cooperation:

- **Stuck loop detection**: Identifies repeated writes to the same files across iterations
- **Stalled progress detection**: Flags Kobolds with no step completions over multiple cycles
- **Repeated error detection**: Catches identical blocker strings across consecutive reflections
- **Budget exhaustion detection**: Alerts when iteration budget is nearly spent with low progress
- **Escalation**: Creates `EscalationAlert` with `Source = ReasoningMonitor` and routes through Drake

**Pros**: Doesn't require LLM cooperation, catches issues the agent itself misses
**Cons**: Reactive rather than preventive (but complements Option 2's proactive checks)

### Escalation Routing (2026-03-15)

When the `reflect` tool or `ReasoningMonitorService` creates an `EscalationAlert`, Drake's `HandleEscalationAsync` routes it by type:

| Escalation Type | Route | Action |
|----------------|-------|--------|
| `WrongApproach` | `KoboldPlannerAgent.RevisePlanAsync` | Revises plan in-place, preserving completed steps |
| `TaskInfeasible` / `NeedsSplit` / `MissingDependency` | `Wyvern.RefineTaskAsync` | LLM-driven task refinement and re-breakdown |
| `WrongAgentType` | Task reset | Resets task to `Unassigned` for reassignment with correct agent |

**Real-time notifications** are pushed to the Dragon client via `ProjectNotificationService.OnNotification`:
- Chat displays: "WARNING: ESCALATION" inline message
- 10-second warning toast notification
- Red badge on project navigation
- Dashboard banner for active escalations

---

## Expected Benefits

| Metric | Before Self-Reasoning | With Self-Reasoning (Live) |
|--------|----------------------|----------------------------|
| Stuck loop detection | After max retries | After 3-5 iterations (ReasoningMonitorService, 45s cycle) |
| Error retry success | ~40% (same approach) | ~65% (adapted approach via reflect + plan revision) |
| Plan completion rate | ~70% | ~85% (mid-course corrections via RevisePlanAsync) |
| Quality confidence | Binary (done/not done) | Graduated (confidence_percent from reflect tool) |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Reasoning overhead slows execution | Make checkpoints lightweight, skip if progress is steady |
| LLM produces verbose/useless reflections | Enforce structured format, discard unstructured |
| Reflection becomes infinite loop | Cap reflection iterations, timeout |
| Overconfidence in self-assessment | Cross-validate with file checks, tests |

---

## Conclusion

Self-reasoning has been fully implemented across three complementary layers, addressing DraCode's primary limitation of rigid plan execution:

1. ✅ **Iteration checkpoints** (Option 1) - Prompt-based reflection protocol - COMPLETED 2026-02-12
2. ✅ **Error root-cause analysis** (Option 1) - Smarter retry strategies - COMPLETED 2026-02-12
3. ✅ **Structured reflection** (Option 2) - `ReflectionTool` with confidence tracking, stall detection, and escalation - COMPLETED 2026-03-15
4. ✅ **External monitoring** (Option 3) - `ReasoningMonitorService` detecting stuck loops, stalled progress, and budget exhaustion - COMPLETED 2026-03-15
5. ✅ **Plan re-evaluation** - `KoboldPlannerAgent.RevisePlanAsync` preserving completed steps - COMPLETED 2026-03-15
6. ⏳ **Workspace conflict reasoning** - Drake-level multi-agent file conflict detection - FUTURE

**Status**: All three implementation options are live. The remaining gap is workspace conflict reasoning at the Drake level, which would complete cross-agent self-awareness.
