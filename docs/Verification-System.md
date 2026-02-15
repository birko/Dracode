# Project Verification System

## Overview

The KoboldLair verification system automatically validates projects after all implementation tasks complete. It runs tech-stack-appropriate checks (build, test, lint) and creates fix tasks if errors are detected.

## Architecture

### Workflow

```
Tasks Complete → DrakeExecutionService
                      ↓
           Status: AwaitingVerification
                      ↓
         WyvernVerificationService (30s poll)
                      ↓
    ┌─────────────────┴─────────────────┐
    ↓                                    ↓
Verification Passed            Verification Failed
    ↓                                    ↓
Status: Verified              Create Fix Tasks
    ↓                                    ↓
Status: Completed          Status: InProgress
                           (Drake resumes)
```

### Components

1. **WyvernVerificationService** - Background service that:
   - Monitors projects with `AwaitingVerification` status
   - Executes verification checks in project workspace
   - Generates markdown reports
   - Creates fix tasks on failure

2. **Wyrm Pre-Analysis** - Recommends verification steps during initial analysis
3. **Auto-Detection** - Falls back to tech-stack detection if no recommendations
4. **Dragon Tools** - Manual verification management via Warden

## Verification Checks

### Check Types

| Priority | Check Type | Examples |
|----------|------------|----------|
| Critical | Build/Compilation | `dotnet build`, `npm run build` |
| High | Tests | `dotnet test`, `npm test`, `pytest` |
| Medium | Linting | `npm run lint`, `pylint` |
| Low | Documentation | README checks, coverage reports |

### Tech Stack Detection

**Automatic detection** when Wyrm recommendations unavailable:

| Stack | Detection | Checks |
|-------|-----------|--------|
| .NET | `*.csproj`, `*.sln` | `dotnet build`, `dotnet test` |
| Node.js | `package.json` | `npm run build`, `npm test` |
| Python | `requirements.txt`, `setup.py` | `pytest` |
| React | `package.json` + React deps | `npm run build`, `npm run lint` |

## Configuration

### Global Settings

`appsettings.json`:

```json
{
  "KoboldLair": {
    "Verification": {
      "Enabled": true,
      "TimeoutSeconds": 600,
      "AutoCreateFixTasks": true,
      "RequireAllChecksPassing": false,
      "SkipForImportedProjects": true
    }
  }
}
```

**Settings:**
- `Enabled` - Master switch for verification system
- `TimeoutSeconds` - Per-check timeout (default: 600s / 10 minutes)
- `AutoCreateFixTasks` - Auto-create tasks when checks fail
- `RequireAllChecksPassing` - Strict mode (all checks must pass)
- `SkipForImportedProjects` - Skip verification for imported projects

### Per-Project Configuration

Projects inherit global settings. Future enhancement: per-project overrides in `projects.json`.

## Dragon Tools (via Warden)

### retry_verification

**Purpose**: Manually trigger or retry verification

**Actions:**
- `list` - Show projects needing verification
- `run` - Trigger verification for a project
- `status` - Check verification status

**Examples:**
```
User: "Show me projects awaiting verification"
Dragon → Warden: retry_verification with action='list'

User: "Run verification for my-todo-app"
Dragon → Warden: retry_verification with action='run', project='my-todo-app'

User: "What's the verification status of my-todo-app?"
Dragon → Warden: retry_verification with action='status', project='my-todo-app'
```

### view_verification_report

**Purpose**: Display full verification report

**Example:**
```
User: "Show me the verification report for my-todo-app"
Dragon → Warden: view_verification_report with project='my-todo-app'
```

### skip_verification

**Purpose**: Skip verification (for imported/legacy projects)

**Example:**
```
User: "Skip verification for imported-legacy-app"
Dragon → Warden: skip_verification with project='imported-legacy-app'
```

## Verification Report Format

Generated as markdown in `project.VerificationReport`:

```markdown
# Verification Report
**Project**: my-todo-app
**Started**: 2026-02-15 10:00:00 UTC

## Checks Executed

### build - ✅ PASSED
**Command**: `dotnet build`
**Duration**: 12.34s
**Exit Code**: 0

### test - ❌ FAILED
**Command**: `dotnet test`
**Duration**: 5.67s
**Exit Code**: 1
**Output**:
```
Test1.cs(42): error: Null reference exception
...
```

## Summary
**Status**: ❌ FAILED
**Total Checks**: 2
**Passed**: 1
**Failed**: 1
**Critical Failures**: 0
**High Priority Failures**: 1
```

## Fix Task Creation

When verification fails with `AutoCreateFixTasks=true`:

1. **Task File Created**: `{project}/tasks/verification-fixes-tasks.md`
2. **Task Format**:
   ```markdown
   | verify-test-abc12345 | [id:verify-test-abc12345] [priority:High] Fix test failure: dotnet test. Error: Test1.cs(42): Null reference... | unassigned | unassigned |
   ```
3. **Registered**: Task file added to project's `Paths.TaskFiles`
4. **Drake Resumes**: Project transitions back to `InProgress`, Drake picks up fix tasks

## Project Status Flow

```
New → WyrmAssigned → Analyzed → InProgress → AwaitingVerification → Verified → Completed
                                     ↑                  ↓
                                     └──── (if fixes needed) ────┘
```

**Status Meanings:**
- `AwaitingVerification` - All tasks done, verification pending
- `Verified` - Verification passed, ready for completion
- `Completed` - Project fully complete

**VerificationStatus Values:**
- `NotStarted` - No verification run yet
- `InProgress` - Verification currently running
- `Passed` - All checks passed
- `Failed` - Some checks failed
- `Skipped` - Verification skipped by user/config

## Data Storage

Per-project data in `Project` class:

```csharp
public VerificationStatus VerificationStatus { get; set; }
public DateTime? VerificationStartedAt { get; set; }
public DateTime? VerificationCompletedAt { get; set; }
public string? VerificationReport { get; set; }
public List<VerificationCheck> VerificationChecks { get; set; }
```

Persisted in `projects.json`.

## Troubleshooting

### Verification Stuck in "InProgress"

**Symptom**: Project shows `VerificationStatus=InProgress` indefinitely

**Causes:**
- Service crashed during verification
- Check timed out without cleanup
- Network issue during command execution

**Resolution:**
1. Use Dragon: "Retry verification for {project}"
2. Warden calls `retry_verification` to reset state
3. Service will re-run within 30 seconds

### Verification Skipped When Expected

**Symptom**: Verification status shows `Skipped` unexpectedly

**Causes:**
- `SkipForImportedProjects=true` and project was imported
- User manually skipped via Dragon tool
- No verification steps configured (empty Wyrm recommendations + no auto-detection)

**Resolution:**
- Check `appsettings.json` → `Verification:SkipForImportedProjects`
- Review project's `wyrm-recommendation.json` for verification steps
- Manually trigger: "Run verification for {project}"

### Verification Fails with Timeout

**Symptom**: Checks fail with `[TIMEOUT: Process killed after {N}s]`

**Causes:**
- Default timeout (600s) too short for large projects
- Hung process (infinite loop, waiting for input)

**Resolution:**
1. Increase timeout in `appsettings.json`:
   ```json
   "Verification": {
     "TimeoutSeconds": 1200
   }
   ```
2. Check project for interactive prompts (add `-y` flags, etc.)
3. Retry verification after adjustments

### False Positives (Checks Fail Incorrectly)

**Symptom**: Verification reports failure but code works

**Causes:**
- Missing dependencies not installed
- Incorrect success criteria
- Environment-specific issues

**Resolution:**
1. Check verification report for actual error
2. Inspect Wyrm recommendations in `wyrm-recommendation.json`
3. If incorrect, skip verification and fix manually:
   ```
   User: "Skip verification for {project}"
   ```
4. Submit bug report with project details

## Best Practices

### For Developers

1. **Review Wyrm Recommendations** - Check `wyrm-recommendation.json` before approval
2. **Test Locally First** - Run verification commands manually before submitting
3. **Add Missing Tests** - Verification will catch projects without tests
4. **Fix Warnings** - Don't skip verification to hide warnings

### For System Administrators

1. **Tune Timeouts** - Adjust `TimeoutSeconds` based on project sizes
2. **Monitor Failures** - Track verification failure rates
3. **Review Auto-Detection** - Verify tech-stack detection accuracy
4. **Adjust Strictness** - Use `RequireAllChecksPassing` for production projects

## Implementation Details

### Background Service Timing

- **Check Interval**: 30 seconds
- **Concurrent Projects**: Max 3 at a time
- **Per-Check Timeout**: Configurable (default: 600s)

### Command Execution

- **Shell**: PowerShell on Windows
- **Working Directory**: Project workspace (`{project}/workspace/`)
- **Output Capture**: Stdout + Stderr combined
- **Retry Logic**: None (single execution per check)

### Error Classification

- **Exit Code 0**: Success (unless success criteria specifies otherwise)
- **Non-Zero Exit**: Failure
- **Timeout**: Failure with special marker
- **Exception**: Failure with error message

## Future Enhancements

### Planned (Not Yet Implemented)

1. **WebSocket Notifications** - Real-time verification progress updates
2. **Per-Project Config** - Override verification settings per project
3. **Custom Verification Steps** - User-defined check commands
4. **Verification Templates** - Reusable verification profiles
5. **Partial Re-runs** - Re-run only failed checks

### Under Consideration

- Verification metrics dashboard
- Integration with CI/CD pipelines
- Parallel check execution
- Verification history tracking
- Smart retry (exponential backoff)

## API Reference

### WyvernVerificationService

```csharp
public class WyvernVerificationService : BackgroundService
{
    // Check interval: 30 seconds
    // Max concurrent: 3 projects
    // Per-check timeout: Configurable (default: 600s)
}
```

### VerificationCheck Model

```csharp
public class VerificationCheck
{
    public string CheckType { get; set; }          // "build", "test", "lint"
    public string Command { get; set; }            // Command executed
    public int ExitCode { get; set; }              // Exit code
    public string Output { get; set; }             // Stdout + Stderr
    public double DurationSeconds { get; set; }    // Execution time
    public bool Passed { get; set; }               // Success/failure
    public VerificationCheckPriority Priority { get; set; } // Critical/High/Medium/Low
    public DateTime ExecutedAt { get; set; }       // Timestamp
}
```

### VerificationStepDefinition Model

```csharp
public class VerificationStepDefinition
{
    public string CheckType { get; set; }          // Check type
    public string Command { get; set; }            // Command to execute
    public string SuccessCriteria { get; set; }    // "exit_code_0", "contains:text"
    public string Priority { get; set; }           // "Critical", "High", "Medium", "Low"
    public int TimeoutSeconds { get; set; }        // Timeout (0 = use default)
    public string? WorkingDirectory { get; set; }  // Relative path
    public string Description { get; set; }        // Human-readable description
}
```

## Version History

- **v1.0** (2026-02-15) - Initial implementation
  - Background verification service
  - Wyrm recommendations
  - Auto-detection fallback
  - Dragon tools integration
  - Fix task creation
  - Configuration system
