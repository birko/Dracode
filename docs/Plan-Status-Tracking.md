# Plan Status Tracking

This document explains how implementation plan statuses and step statuses are managed in the KoboldLair system, including when they change and what triggers those changes.

## Overview

The planning system has two levels of status tracking:
- **Plan Status** (`PlanStatus`) - Overall status of the entire implementation plan
- **Step Status** (`StepStatus`) - Individual status of each step within the plan

## Plan Status States

### PlanStatus Enum

Located in: `DraCode.KoboldLair\Models\Agents\KoboldImplementationPlan.cs`

```csharp
public enum PlanStatus
{
    Planning,    // Plan is being generated
    Ready,       // Plan is ready for execution
    InProgress,  // Plan is being executed
    Completed,   // Plan has been completed successfully
    Failed       // Plan execution failed
}
```

### Plan Status Transitions

#### 1. **Planning → Ready**
- **When**: After `KoboldPlannerAgent` successfully creates the plan
- **Where**: `KoboldPlannerAgent.cs` - After all steps are generated
- **Code**: Plan starts with `Planning` status (default), transitions to `Ready` when planner finishes

#### 2. **Ready → InProgress**
- **When**: When Kobold starts executing the first step
- **Where**: `Kobold.StartWorkingAsync()` - When `LoadOrCreatePlanAsync()` loads an existing plan or creates a new one
- **Code**: Automatically transitioned when Kobold begins work with a plan

#### 3. **InProgress → Completed**
- **When**: After Kobold finishes work AND all steps are in a terminal state
- **Where**: `Kobold.UpdatePlanStatusAsync()` (line 433-467)
- **Condition**: Plan is marked completed ONLY if:
  ```csharp
  var allStepsFinished = ImplementationPlan.Steps.All(s =>
      s.Status == StepStatus.Completed ||
      s.Status == StepStatus.Skipped ||
      s.Status == StepStatus.Failed);
  ```
- **Important**: If the Kobold finishes its work session but some steps are still `Pending` or `InProgress`, the plan stays in `InProgress` state for later resumption

#### 4. **InProgress → Failed**
- **When**: Kobold encounters an unrecoverable error
- **Where**: `Kobold.UpdatePlanStatusAsync()` with `success = false`
- **Triggers**: Exception in `StartWorkingAsync()`, timeout (marked as stuck), or explicit failure

## Step Status States

### StepStatus Enum

Located in: `DraCode.KoboldLair\Models\Agents\KoboldImplementationPlan.cs`

```csharp
public enum StepStatus
{
    Pending,     // Step has not started
    InProgress,  // Step is currently being executed
    Completed,   // Step completed successfully
    Skipped,     // Step was skipped
    Failed       // Step failed
}
```

### Step Status Transitions

All step status changes happen through the `UpdatePlanStepTool` or direct method calls on the `ImplementationStep` object.

#### 1. **Pending → InProgress**
- **When**: Kobold starts working on a step
- **How**: Call `step.Start()` method
- **Code**:
  ```csharp
  public void Start()
  {
      Status = StepStatus.InProgress;
      StartedAt = DateTime.UtcNow;
  }
  ```
- **Note**: Currently NOT automatically called - Kobold must explicitly call this or use the tool

#### 2. **InProgress → Completed**
- **When**: Kobold successfully completes a step
- **How**: Use `update_plan_step` tool with `status: "completed"`
- **Code**:
  ```csharp
  step.Complete(output);
  _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) completed");
  _currentPlan.AdvanceToNextStep(); // Moves CurrentStepIndex forward
  ```
- **Side Effects**:
  - Sets `CompletedAt` timestamp
  - Advances `CurrentStepIndex` to next step
  - Saves plan to disk automatically

#### 3. **InProgress → Failed**
- **When**: Kobold encounters an error while executing a step
- **How**: Use `update_plan_step` tool with `status: "failed"`
- **Code**:
  ```csharp
  step.Fail(output);
  _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) failed: {output}");
  ```
- **Side Effects**:
  - Sets `CompletedAt` timestamp
  - Does NOT advance `CurrentStepIndex`
  - Step can be retried by resuming the plan

#### 4. **Pending/InProgress → Skipped**
- **When**: Kobold determines a step is not needed
- **How**: Use `update_plan_step` tool with `status: "skipped"`
- **Code**:
  ```csharp
  step.Skip(reason);
  _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) skipped: {output}");
  _currentPlan.AdvanceToNextStep(); // Moves CurrentStepIndex forward
  ```
- **Side Effects**:
  - Sets `CompletedAt` timestamp
  - Advances `CurrentStepIndex` to next step
  - Saves plan to disk automatically

## Current Issue: Plans Marked Completed with Pending Steps

### Problem Description

Plans can be marked as `Completed` even when some steps are still `Pending`. This happens when:

1. Kobold starts working on a plan
2. Kobold completes some steps but doesn't finish all of them
3. Kobold reaches max iterations or decides it's done
4. `UpdatePlanStatusAsync()` is called with `success = true`
5. **Bug**: The code checks if all steps are "finished" but doesn't distinguish between:
   - Kobold successfully completing its work session (but more work remains)
   - Kobold actually finishing all the work

### Root Cause

In `Kobold.UpdatePlanStatusAsync()` (lines 440-456):

```csharp
if (success)
{
    // Only mark the plan as completed if all steps are actually done
    var allStepsFinished = ImplementationPlan.Steps.All(s =>
        s.Status == StepStatus.Completed ||
        s.Status == StepStatus.Skipped ||
        s.Status == StepStatus.Failed);

    if (allStepsFinished)
    {
        ImplementationPlan.MarkCompleted();
    }
    else
    {
        // Agent finished but not all steps are done - keep plan in progress for resumption
        ImplementationPlan.AddLogEntry($"Kobold {Id.ToString()[..8]} finished work session, {ImplementationPlan.CompletedStepsCount}/{ImplementationPlan.Steps.Count} steps completed");
    }
}
```

**The logic is correct** - it should NOT mark the plan as completed if steps are still pending. However, there might be scenarios where:

1. **Steps not being updated**: Kobold completes work but doesn't call `update_plan_step` for each step
2. **Race conditions**: Multiple Kobolds working on the same plan (not currently supported but could cause issues)
3. **Manual status changes**: Something else is calling `MarkCompleted()` directly

### Where `MarkCompleted()` Can Be Called

Search results show only one place where `MarkCompleted()` is called:
- `Kobold.UpdatePlanStatusAsync()` line 450 - with the proper check

### Possible Causes

1. **Kobold not using the tool**: If the Kobold doesn't call `update_plan_step` for each step, the steps remain `Pending` even though the work is done, but the plan should still remain `InProgress` (not `Completed`)

2. **Different plan instance**: If the plan is loaded multiple times and different instances are used, updates might not be synchronized

3. **Plan loaded after completion**: If a completed plan is loaded and inspected later, it might still show old step statuses if the plan was marked complete through a different code path

4. **Direct step manipulation**: If code directly modifies `step.Status` without using the `Start()`, `Complete()`, `Fail()`, or `Skip()` methods, the status might be changed incorrectly

5. **Premature completion**: The agent reaches max iterations and exits normally (line 284 in Kobold.cs calls `UpdatePlanStatusAsync(success: true)`), but the check should prevent marking it complete if steps are pending

### Verification Steps

To debug this issue:

1. **Check plan save times**: Look at the markdown plan file - does the `Updated` timestamp match when it was marked completed?

2. **Check execution log**: The log should show each step completion via `update_plan_step`. Look for:
   - `Step {N} ({Title}) completed/failed/skipped` entries
   - If these are missing, the Kobold didn't call `update_plan_step`

3. **Check step statuses in the markdown**: In the "Steps Overview" table, look at the Status column. Count how many are Pending/InProgress vs Completed/Skipped/Failed

4. **Search for direct status changes**:
   ```bash
   grep -r "Status = PlanStatus.Completed" --include="*.cs"
   grep -r "\.Status = StepStatus\." --include="*.cs"
   ```

5. **Check if plan is being reused**: Look for warnings about plan file existence before creation

6. **Verify the check logic**: Add logging to `UpdatePlanStatusAsync` to see exactly which steps are in what status when the completion check runs

## Recommended Fix

To add better visibility into why plans are being marked completed, add debug logging:

```csharp
// In Kobold.UpdatePlanStatusAsync(), around line 440
if (success)
{
    // Log current step statuses
    var statusCounts = ImplementationPlan.Steps
        .GroupBy(s => s.Status)
        .ToDictionary(g => g.Key, g => g.Count());
    
    var statusSummary = string.Join(", ", statusCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
    _logger?.LogInformation("Plan step status summary: {Summary}", statusSummary);

    // Only mark the plan as completed if all steps are actually done
    var allStepsFinished = ImplementationPlan.Steps.All(s =>
        s.Status == StepStatus.Completed ||
        s.Status == StepStatus.Skipped ||
        s.Status == StepStatus.Failed);

    if (allStepsFinished)
    {
        _logger?.LogInformation("All steps finished - marking plan as completed");
        ImplementationPlan.MarkCompleted();
    }
    else
    {
        var pendingSteps = ImplementationPlan.Steps
            .Where(s => s.Status == StepStatus.Pending || s.Status == StepStatus.InProgress)
            .Select(s => $"Step {s.Index}: {s.Title}")
            .ToList();
        
        _logger?.LogInformation(
            "Plan still has {Count} unfinished steps, keeping in InProgress: {Steps}",
            pendingSteps.Count,
            string.Join("; ", pendingSteps)
        );
        
        ImplementationPlan.AddLogEntry(
            $"Kobold {Id.ToString()[..8]} finished work session, " +
            $"{ImplementationPlan.CompletedStepsCount}/{ImplementationPlan.Steps.Count} steps completed. " +
            $"Unfinished: {string.Join(", ", pendingSteps.Select(p => p.Split(':')[0]))}"
        );
    }
}
```

This will help identify:
- Whether steps are actually being marked as completed
- Which specific steps are still pending when the plan ends
- If the logic is working correctly

## Best Practices

### For Kobold Agent Developers

1. **Always call `update_plan_step`**: After completing each step's work, the Kobold MUST call the tool
   ```
   update_plan_step(step_index=1, status="completed", output="Created User.cs class")
   ```

2. **Update in real-time**: Don't wait until all work is done - update steps as you go

3. **Use appropriate statuses**:
   - `completed`: Step was successfully done
   - `failed`: Step encountered an error (can be retried)
   - `skipped`: Step is not needed (e.g., file already exists)

### For Plan Management Code

1. **Always check all steps**: Before marking a plan complete, verify all steps are in terminal states

2. **Save frequently**: Plans should be saved after each step update for resumability

3. **Use proper locking**: The `UpdatePlanStepTool` uses a lock to prevent race conditions

## Debugging Commands

### Check plan status from the command line

```bash
# View plan JSON
cat C:\Source\DraCode-Projects\{project-name}\workspace\kobold-plans\{plan-filename}-plan.json

# View plan markdown (human-readable)
cat C:\Source\DraCode-Projects\{project-name}\workspace\kobold-plans\{plan-filename}-plan.md

# Count steps by status
grep -E "\"status\":" {plan-file}.json
```

### Check logs for plan updates

Look for these log messages:
- `Loaded plan for task` - Plan was loaded from disk
- `Created new implementation plan` - New plan was created
- `Saved plan` - Plan was persisted
- `Step {N} ({Title}) completed/failed/skipped` - Step status change
- `finished work session` - Kobold finished but plan incomplete
- `Plan completed successfully` - Plan marked as complete

## Configuration

Plan behavior is controlled by `appsettings.json`:

```json
{
  "KoboldLair": {
    "Planning": {
      "Enabled": true,              // Enable/disable planning system
      "PlannerProvider": null,      // Override provider for planner
      "PlannerModel": null,         // Override model for planner
      "MaxPlanningIterations": 5,   // Max iterations for plan creation
      "SavePlanProgress": true,     // Save after each step update
      "ResumeFromPlan": true        // Resume from saved plans
    }
  }
}
```

## Related Files

- `DraCode.KoboldLair\Models\Agents\KoboldImplementationPlan.cs` - Plan and step models
- `DraCode.KoboldLair\Services\KoboldPlanService.cs` - Plan persistence
- `DraCode.KoboldLair\Models\Agents\Kobold.cs` - Kobold execution logic
- `DraCode.KoboldLair\Agents\Tools\UpdatePlanStepTool.cs` - Tool for updating steps
- `DraCode.KoboldLair\Agents\KoboldPlannerAgent.cs` - Creates plans
