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
| **P2** | Plan re-evaluation | Kobold | ⏳ Future |
| **P2** | Success self-assessment | Kobold | ⏳ Future |
| **P3** | Uncertainty estimation | Planner | ⏳ Future |
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

### Option 2: Structured Reflection Tool ✅ COMPLETED (2026-02-12)

**Location**: `DraCode.KoboldLair/Agents/Tools/ReflectionTool.cs`

The `reflect` tool forces explicit, structured reasoning output:

```csharp
public class ReflectionTool : Tool
{
    // Input Schema
    // - progress_percent (0-100): Progress toward current step
    // - files_done[]: Files successfully created/modified
    // - blockers[]: Current obstacles
    // - confidence (0-100): Confidence in current approach
    // - decision: "continue" | "pivot" | "escalate"
    // - notes: Optional context

    // Automatic Intervention Detection:
    // - Confidence < 30% → LowConfidence signal
    // - Decision = "escalate" → AgentEscalated signal
    // - 20%+ drop over 3 checkpoints → DecliningConfidence signal
    // - 3+ blockers → MultipleBlockers signal
}
```

**Implementation Details**:
- Static context registration (same pattern as UpdatePlanStepTool)
- Stores reflections in `KoboldImplementationPlan.ReflectionHistory`
- Records to `SharedPlanningContextService` for cross-agent learning
- Generates contextual guidance based on confidence level
- Intervention callback for immediate Drake notification

**Pros**: Structured data, automated intervention triggers, persistence
**Cons**: Adds tool call, requires LLM cooperation

### Option 3: External Reasoning Monitor ✅ COMPLETED (2026-02-12)

**Location**: `DraCode.KoboldLair.Server/Services/ReasoningMonitorService.cs`

Background service that analyzes reflection patterns across all active Kobolds:

```csharp
public class ReasoningMonitorService : BackgroundService
{
    // Pattern Detection (runs every 30 seconds):
    // - ExplicitEscalation: Agent requested help
    // - LowConfidence: Below threshold (default: 30%)
    // - DecliningConfidence: 20%+ drop over 3 checkpoints
    // - MultipleBlockers: 3+ blockers reported
    // - StalledProgress: 0% progress for 3+ checkpoints
    // - RepeatedFileModifications: Same file edited 5+ times (stuck loop)

    // Auto-Intervention:
    // - Critical patterns → Mark Kobold as stuck
    // - Drake recovers on next monitoring cycle
    // - Plan log updated with intervention reason
}
```

**Configuration** (`appsettings.json`):
```json
"ReasoningMonitor": {
  "Enabled": true,
  "MonitoringIntervalSeconds": 30,
  "DecliningConfidenceCheckpoints": 3,
  "RepeatedFileModificationThreshold": 5,
  "StalledProgressCheckpoints": 3,
  "HighBlockerThreshold": 3,
  "LowConfidenceInterventionThreshold": 30,
  "AutoInterventionEnabled": true
}
```

**Pros**: Observes actual behavior, catches patterns LLM might miss, no LLM cooperation required
**Cons**: Reactive (post-hoc), requires reflection data from Option 2

---

## Expected Benefits

| Metric | Current | With Self-Reasoning |
|--------|---------|---------------------|
| Stuck loop detection | After max retries | After 3-5 iterations |
| Error retry success | ~40% (same approach) | ~65% (adapted approach) |
| Plan completion rate | ~70% | ~85% (mid-course corrections) |
| Quality confidence | Binary (done/not done) | Graduated (confidence %) |

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

Self-reasoning addresses DraCode's primary limitation: agents that execute plans rigidly without adapting to discovered realities. The implemented improvements are:

1. ✅ **Iteration checkpoints** - Catch stuck loops early - COMPLETED (Option 1)
2. ✅ **Error root-cause analysis** - Smarter retry strategies - COMPLETED (Option 1)
3. ✅ **Structured reflection tool** - Capture reasoning programmatically - COMPLETED (Option 2)
4. ✅ **Reasoning monitor service** - Detect concerning patterns - COMPLETED (Option 2)
5. ⏳ **Plan feasibility re-evaluation** - Prevent wasted work downstream - FUTURE

**Status**: Options 1 (prompt-based) and 2 (structured tools) are now complete. The system provides:

- **Proactive self-reflection**: Kobolds report progress, confidence, and blockers via `reflect` tool
- **Automatic intervention**: Low confidence or stuck patterns trigger Drake intervention
- **Pattern detection**: ReasoningMonitorService detects declining confidence, stuck loops, stalled progress
- **Learning context**: Reflections persisted in SharedPlanningContextService for cross-agent insights

**Files Added/Modified**:
- `ReflectionSignal.cs` - Data models for reflection checkpoints
- `ReflectionTool.cs` - Structured tool for self-reflection
- `ReasoningMonitorService.cs` - Background pattern detection service
- `KoboldImplementationPlan.cs` - Added ReflectionHistory property
- `SharedPlanningContextService.cs` - Added RecordReflectionAsync method
- `Kobold.cs` - Tool registration, iteration tracking, prompt updates
- `KoboldLairConfiguration.cs` - Added ReasoningMonitorConfiguration
- `Program.cs` - Registered ReasoningMonitorService
- `appsettings.json` - Added ReasoningMonitor configuration section
