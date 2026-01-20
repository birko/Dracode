# DraCode CLI Options

## Command-Line Arguments

### Provider Selection
```bash
# Specify provider explicitly
dotnet run -- --provider=openai --task="Your task"
dotnet run -- --provider=claude --task="Your task"
dotnet run -- --provider=gemini --task="Your task"
dotnet run -- --provider=githubcopilot --task="Your task"

# If omitted and multiple providers configured, interactive menu will appear
dotnet run -- --task="Your task"
```

### Verbose Output
```bash
# Enable verbose mode (shows detailed execution info)
dotnet run -- --verbose --task="Your task"

# Disable verbose mode (shows only results)
dotnet run -- --no-verbose --task="Your task"
dotnet run -- --quiet --task="Your task"

# Explicit boolean syntax
dotnet run -- --verbose=true --task="Your task"
dotnet run -- --verbose=false --task="Your task"

# If omitted, interactive prompt will appear
dotnet run -- --task="Your task"
```

### Task Prompt
```bash
# Inline task
dotnet run -- --task="Create a hello world program"

# Read task from file
dotnet run -- --task="task.txt"

# If omitted, will prompt user for input
dotnet run
```

### Combining Arguments
```bash
# All options together
dotnet run -- --provider=claude --verbose --task="Write tests"

# Mix of args and interactive
dotnet run -- --provider=openai  # Will ask for verbose option and task
dotnet run -- --verbose          # Will ask for provider and task
```

## Configuration File Options

### appsettings.json / appsettings.local.json

```json
{
  "Agent": {
    "Provider": "openai",
    "Verbose": true,
    "WorkingDirectory": "./",
    "TaskPrompt": "",
    "Providers": {
      "openai": {
        "type": "openai",
        "apiKey": "${OPENAI_API_KEY}",
        "model": "gpt-4"
      },
      "claude": {
        "type": "claude",
        "apiKey": "${ANTHROPIC_API_KEY}",
        "model": "claude-3-7-sonnet-20250219"
      }
    }
  }
}
```

### Configuration Hierarchy
1. **Command-line arguments** (highest priority)
2. **Environment variables**
3. **appsettings.local.json**
4. **appsettings.json** (lowest priority)

## Interactive UI

### Provider Selection Menu
When no `--provider` argument is given and multiple providers are configured:

```
Select an AI Provider:

> 🤖 openai (default)
  ☁️ azureopenai
  🧠 claude
  ✨ gemini
  🦙 ollama
  🐙 githubcopilot
```

- Use arrow keys ↑↓ to navigate
- Press Enter to select
- Default provider is marked with "(default)"

### Verbose Output Prompt
When no `--verbose`, `--no-verbose`, or `--quiet` argument is given:

```
Enable verbose output?

> Yes - Show detailed execution info
  No - Show only results
```

- Use arrow keys ↑↓ to navigate
- Press Enter to select

### Task Input
When no `--task` argument is provided:

```
Enter task prompt: _
```

- Type your task description
- Press Enter to submit
- Can also provide a file path to read task from file

## Verbose Mode Differences

### Verbose Enabled (--verbose)
```
── ITERATION 1 ─────────────────────────────
Stop reason: tool_use
╭─ Tool Call ────────────────────────────────╮
│ 🛠️ write_file                             │
│                                            │
│ Creating new file...                       │
╰────────────────────────────────────────────╯

Tool Result: File created successfully
```

Shows:
- Iteration numbers
- Stop reasons
- Tool call details with formatted panels
- Tool execution results
- Full conversation flow

### Verbose Disabled (--no-verbose / --quiet)
```
File created successfully
```

Shows:
- Final results only
- Error messages
- No iteration details
- No tool call panels
- Cleaner, minimal output

## Examples

### Interactive Mode (Beginner-Friendly)
```bash
# Just run without arguments - UI will guide you
dotnet run
```

### Quick Task (Power User)
```bash
# Everything specified, no prompts
dotnet run -- --provider=claude --quiet --task="Add unit tests to Calculator.cs"
```

### Debugging Mode
```bash
# See detailed execution for troubleshooting
dotnet run -- --verbose --task="Fix the bug in Auth.cs"
```

### File-Based Task
```bash
# Read complex task from file
dotnet run -- --provider=githubcopilot --task="refactor-plan.md"
```

## Tips

1. **First-time users**: Run without arguments to see interactive UI
2. **Power users**: Use `--provider=X --quiet --task="Y"` for fast execution
3. **Debugging**: Always use `--verbose` to see detailed execution
4. **CI/CD**: Use explicit arguments to avoid interactive prompts
5. **Default provider**: Set in config file to skip provider selection
6. **Multiple providers**: Leave provider unset to see selection menu
