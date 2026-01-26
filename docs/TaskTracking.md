# Task Status Tracking System

## Overview

The wyvern agent now includes a comprehensive task tracking system that monitors task lifecycle and generates markdown status reports.

## Components

### 1. TaskStatus Enum
```csharp
public enum TaskStatus
{
    Unassigned,      // ⚪ Task received, no agent assigned
    NotInitialized,  // 🔵 Agent assigned but not started
    Working,         // 🟡 Agent actively working
    Done            // 🟢 Task completed
}
```

### 2. TaskRecord Class
Stores information about each task:
- Task description
- Assigned agent type
- Current status
- Creation and update timestamps
- Error message (if any)

### 3. TaskTracker Class
Manages multiple tasks and generates reports:
- `AddTask(string task)` - Add new task to track
- `UpdateTask(TaskRecord, status, agent)` - Update task status
- `SetError(TaskRecord, message)` - Mark task as errored
- `GenerateMarkdown(title)` - Generate markdown report
- `SaveToFile(path, title)` - Save report to file

## Usage Patterns

### Single Task with Report

```csharp
var (agentType, _, conversation, tracker) = await WyvernRunner.RunAsync(
    provider: "openai",
    task: "Create React login component",
    outputMarkdownPath: "./status.md"
);

// status.md updated at each transition:
// 1. Initially: unassigned
// 2. After analysis: notinitialized (agent selected)
// 3. During execution: working
// 4. After completion: done
```

### Multiple Tasks

```csharp
var tasks = new[] { "task1", "task2", "task3" };

var (results, tracker) = await WyvernRunner.RunMultipleAsync(
    provider: "openai",
    tasks: tasks,
    outputMarkdownPath: "./all-tasks.md"
);

// all-tasks.md contains status table for all tasks
```

### Custom Tracking

```csharp
var tracker = new TaskTracker();

// Add tasks manually
var task1 = tracker.AddTask("Implement feature X");
var task2 = tracker.AddTask("Fix bug Y");

// Update statuses
tracker.UpdateTask(task1, TaskStatus.Working, "csharp");
tracker.UpdateTask(task2, TaskStatus.Done, "react");

// Generate report
tracker.SaveToFile("./my-report.md", "Sprint Tasks");
```

## Markdown Output Format

### Table Section
```markdown
| Task | Assigned Agent | Status |
|------|----------------|--------|
| Create component | react | 🟢 done |
| Fix auth bug | csharp | 🟡 working |
```

### Summary Section
```markdown
## Summary

- **Total Tasks**: 5
- **Unassigned**: 0
- **Not Initialized**: 1
- **Working**: 2
- **Done**: 2
```

### Error Section (if errors exist)
```markdown
## Errors

### Task: Create React component...
**Error**: Failed to connect to API
```

## Real-time Updates

When `outputMarkdownPath` is specified, the markdown file is updated at each status transition:

```
Task received → File created (unassigned)
↓
Agent selected → File updated (notinitialized)
↓
Execution starts → File updated (working)
↓
Task completes → File updated (done)
```

This allows you to monitor progress in real-time by watching the markdown file.

## Best Practices

1. **Use descriptive task descriptions** - They appear in the table
2. **Provide output path** - Enable automatic report generation
3. **Keep tasks focused** - One clear objective per task
4. **Check error section** - Review any tasks with errors
5. **Share reports** - Markdown format is git-friendly and readable

## Integration Examples

### With CI/CD
```csharp
// Run tasks as part of build pipeline
var (_, tracker) = await WyvernRunner.RunMultipleAsync(
    provider: "openai",
    tasks: GetTasksFromIssues(),
    outputMarkdownPath: "./build-artifacts/task-report.md"
);

// Fail build if any errors
if (tracker.GetAllTasks().Any(t => !string.IsNullOrEmpty(t.ErrorMessage)))
{
    throw new Exception("Some tasks failed");
}
```

### With Web Dashboard
```csharp
// Generate JSON for dashboard
var tasks = tracker.GetAllTasks();
var json = JsonSerializer.Serialize(tasks);

// Or generate HTML table
var markdown = tracker.GenerateMarkdown("Dashboard");
var html = MarkdownToHtml(markdown);
```

## API Reference

### TaskTracker Methods

| Method | Description | Returns |
|--------|-------------|---------|
| `AddTask(string)` | Add new task | TaskRecord |
| `UpdateTask(TaskRecord, TaskStatus, string?)` | Update task status | void |
| `SetError(TaskRecord, string)` | Mark as error | void |
| `GetAllTasks()` | Get all tracked tasks | List<TaskRecord> |
| `GenerateMarkdown(string?)` | Generate report | string |
| `SaveToFile(string, string?)` | Save to file | void |
| `Clear()` | Remove all tasks | void |

### WyvernRunner Methods

| Method | Description |
|--------|-------------|
| `RunAsync(...)` | Run single task with tracking |
| `RunMultipleAsync(...)` | Run multiple tasks with shared tracker |
| `GetRecommendationAsync(...)` | Get agent recommendation only |

## Example Reports

See `docs/example-task-report.md` for a sample output file.
