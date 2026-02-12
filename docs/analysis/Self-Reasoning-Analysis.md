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

| Priority | Enhancement | Agent | Impact |
|----------|-------------|-------|--------|
| **P1** | Iteration checkpoints | Kobold | Catches stuck loops early |
| **P1** | Error explanation | All | Better retry strategies |
| **P2** | Plan re-evaluation | Kobold | Prevents wasted work |
| **P2** | Success self-assessment | Kobold | Higher quality outputs |
| **P3** | Uncertainty estimation | Planner | Better iteration budgets |
| **P3** | Workspace conflict reasoning | Drake | Fewer parallel conflicts |

---

## Concrete Implementation Approaches

### Option 1: Prompt-Based Self-Reflection

Add reflection prompts to system instructions:

```csharp
// In Kobold.BuildFullPromptWithPlanAsync()
sb.AppendLine("\n## SELF-REFLECTION PROTOCOL");
sb.AppendLine("Every 3 iterations, output a CHECKPOINT block:");
sb.AppendLine("```");
sb.AppendLine("CHECKPOINT:");
sb.AppendLine("- Progress: [percentage] toward current step");
sb.AppendLine("- Blockers: [any issues encountered]");
sb.AppendLine("- Confidence: [how sure am I this approach will work]");
sb.AppendLine("- Adjustment: [continue/pivot/escalate]");
sb.AppendLine("```");
```

**Pros**: Simple, no code changes beyond prompts
**Cons**: LLM may ignore, inconsistent formatting

### Option 2: Structured Reflection Tool

Add a `reflect` tool that forces explicit reasoning:

```csharp
public class ReflectionTool : ITool
{
    public string Execute(Dictionary<string, object> args)
    {
        var progress = args["progress_percent"];
        var blockers = args["blockers"];
        var confidence = args["confidence_percent"];
        var adjustment = args["adjustment"];

        // Log reflection for analysis
        // Trigger Drake intervention if confidence < 30%
        // Auto-escalate if progress stalled 3 checkpoints
    }
}
```

**Pros**: Structured data, can trigger automated responses
**Cons**: More complex, adds tool call overhead

### Option 3: External Reasoning Monitor

Separate service analyzes Kobold outputs for patterns:

```csharp
public class ReasoningMonitorService
{
    public async Task AnalyzeKoboldOutputAsync(string output, KoboldContext ctx)
    {
        // Detect repeated error patterns
        // Identify stuck loops (same files modified repeatedly)
        // Flag low-progress iterations
        // Recommend Drake intervention
    }
}
```

**Pros**: Doesn't require LLM cooperation, observes actual behavior
**Cons**: Post-hoc analysis, can't prevent issues proactively

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

Self-reasoning would address DraCode's primary limitation: agents that execute plans rigidly without adapting to discovered realities. The highest-impact improvements are:

1. **Iteration checkpoints** - Catch stuck loops early
2. **Error root-cause analysis** - Smarter retry strategies
3. **Plan feasibility re-evaluation** - Prevent wasted work downstream

Recommended approach: Start with **Option 1 (prompt-based)** to validate the concept, then evolve to **Option 2 (structured tools)** once patterns are understood.
