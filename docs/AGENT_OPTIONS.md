# Agent Options Implementation

## Overview
DraCode.Agent now supports comprehensive configuration options that can be set through:
- Configuration files (appsettings.json)
- WebSocket message configuration
- Command-line arguments (console client)

## Interactive vs Non-Interactive Mode

### Interactive Mode (Default)
- Agent can prompt for user input when needed
- Suitable for scenarios where human input is available
- Best for development and debugging

### Non-Interactive Mode
- Agent runs autonomously without user prompts
- Prompts are automatically responded to with a default response
- Ideal for CI/CD pipelines, automated testing, and batch processing
- Configure `DefaultPromptResponse` to customize auto-responses

## AgentOptions Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Interactive` | bool | true | Enable/disable interactive mode (user prompts) |
| `MaxIterations` | int | 10 | Maximum number of iterations for agent execution |
| `Verbose` | bool | true | Enable verbose logging output |
| `WorkingDirectory` | string | "./" | Working directory for agent operations |
| `PromptTimeout` | int | 300 | Timeout for interactive prompts in seconds (5 minutes) |
| `DefaultPromptResponse` | string? | null | Default response for prompts in non-interactive mode |
| `ModelDepth` | int | 5 | Model thinking/reasoning depth level (0-10). Higher values encourage deeper reasoning. **0-3**: Quick/shallow reasoning, **4-6**: Balanced reasoning (default), **7-10**: Deep/thorough reasoning |

## Configuration Examples

### appsettings.json (Global Defaults)
```json
{
  "Agent": {
    "WorkingDirectory": "./",
    "Interactive": true,
    "MaxIterations": 10,
    "Verbose": true,
    "PromptTimeout": 300,
    "DefaultPromptResponse": null,
    "ModelDepth": 5,
    "Providers": {
      "openai": {
        "Type": "openai",
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o"
      }
    }
  }
}
```

### appsettings.json (Provider-Specific Options)
```json
{
  "Agent": {
    "WorkingDirectory": "./",
    "Interactive": true,
    "ModelDepth": 5,
    "Providers": {
      "openai": {
        "Type": "openai",
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o",
        "Verbose": true,
        "MaxIterations": 15,
        "ModelDepth": 7
      },
      "claude": {
        "Type": "claude",
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-3-5-sonnet-latest",
        "Interactive": false,
        "DefaultPromptResponse": "Please proceed with your best judgment",
        "MaxIterations": 20,
        "ModelDepth": 3
      }
    }
  }
}
```

### Console Client Command-Line Arguments
```bash
# Interactive mode (default)
DraCode.exe --provider=openai --verbose

# Non-interactive mode
DraCode.exe --provider=claude --no-interactive --default-prompt-response="Proceed automatically"

# Custom max iterations
DraCode.exe --provider=openai --max-iterations=20

# Deep reasoning mode
DraCode.exe --provider=openai --model-depth=8

# Quick reasoning mode for simple tasks
DraCode.exe --provider=claude --model-depth=2

# Custom prompt timeout (in seconds)
DraCode.exe --provider=openai --prompt-timeout=600

# Quiet mode
DraCode.exe --provider=openai --quiet
```

Available console arguments:
- `--provider={name}` - Select AI provider
- `--task={description}` - Set task (supports comma-separated multiple tasks)
- `--verbose` / `--no-verbose` / `--quiet` - Control verbosity
- `--interactive` / `--no-interactive` / `--non-interactive` - Control interactive mode
- `--max-iterations={number}` - Set maximum iterations
- `--prompt-timeout={seconds}` - Set prompt timeout
- `--default-prompt-response={text}` - Set default response for non-interactive mode
- `--model-depth={0-10}` - Set model reasoning depth (0=quick, 5=balanced, 10=deep)

### WebSocket Configuration
When connecting an agent via WebSocket, include options in the config object:

```json
{
  "command": "connect",
  "agentId": "agent1",
  "config": {
    "provider": "openai",
    "workingDirectory": "./project",
    "interactive": "false",
    "maxIterations": "15",
    "verbose": "true",
    "promptTimeout": "600",
    "defaultPromptResponse": "I'll proceed with the default option",
    "modelDepth": "7"
  }
}
```

## Configuration Priority (Highest to Lowest)
1. **Runtime WebSocket message config** - Overrides everything
2. **Command-line arguments** - Overrides config files
3. **Provider-specific configuration** - Overrides global settings
4. **Global appsettings.json** - Base defaults
5. **Built-in defaults** - Final fallback

## Use Cases

### CI/CD Pipeline (Quick execution)
```json
{
  "Agent": {
    "Interactive": false,
    "Verbose": false,
    "MaxIterations": 20,
    "ModelDepth": 3,
    "DefaultPromptResponse": "Use best practices and continue",
    "Providers": {
      "openai": {
        "Type": "openai",
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o"
      }
    }
  }
}
```

### Development/Debugging (Deep reasoning)
```json
{
  "Agent": {
    "Interactive": true,
    "Verbose": true,
    "MaxIterations": 10,
    "ModelDepth": 8,
    "PromptTimeout": 600,
    "Providers": {
      "claude": {
        "Type": "claude",
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-3-5-sonnet-latest"
      }
    }
  }
}
```

### Automated Testing (Balanced)
```bash
DraCode.exe --no-interactive --max-iterations=5 --model-depth=5 --default-prompt-response="Skip" --quiet
```

## Behavior Notes

### Model Depth Levels
The `ModelDepth` setting influences the agent's reasoning approach:

**Quick (0-3):**
- Makes direct, straightforward decisions
- Prioritizes speed over exhaustive analysis
- Uses common patterns and best practices
- Best for: Simple tasks, quick fixes, routine operations

**Balanced (4-6) - DEFAULT:**
- Think step-by-step approach
- Considers important edge cases
- Balances thoroughness with efficiency
- Best for: General development tasks, moderate complexity

**Deep (7-10):**
- Thinks carefully through multiple approaches before acting
- Considers edge cases and potential issues
- Analyzes trade-offs and documents reasoning
- Extra careful with changes that could have side effects
- Best for: Complex problems, critical systems, refactoring

### Non-Interactive Mode
- When a prompt is requested in non-interactive mode:
  - If `DefaultPromptResponse` is set: Agent receives the default response
  - If `DefaultPromptResponse` is NOT set: Agent receives an error message
- Logs indicate when auto-responses are used: `[Non-Interactive Mode] Auto-responding to prompt`

### Interactive Mode
- Agent waits for user input when prompts are requested
- Timeout is enforced based on `PromptTimeout` setting
- After timeout, agent receives an error and continues

### Max Iterations
- Controls how many agent reasoning cycles are allowed
- Prevents infinite loops
- Can be increased for complex tasks
- Agent receives a warning when limit is reached

## Web Client Integration
The web client TypeScript interface supports all options:

```typescript
interface AgentConfig {
    provider: string;
    workingDirectory?: string;
    verbose?: string;
    interactive?: string;
    maxIterations?: string;
    promptTimeout?: string;
    defaultPromptResponse?: string;
    modelDepth?: string;
}
```

## Migration Guide

### Existing Code Using Legacy Constructor
Old code:
```csharp
var agent = AgentFactory.Create("openai", "./workspace", verbose: true, config);
```

Still works but is marked obsolete. New code:
```csharp
var options = new AgentOptions
{
    WorkingDirectory = "./workspace",
    Interactive = true,
    Verbose = true,
    MaxIterations = 10,
    ModelDepth = 5
};
var agent = AgentFactory.Create("openai", options, config);
```

## Implementation Details

### Classes
- `AgentOptions` - Main options class in DraCode.Agent
- `AgentConfiguration` - WebSocket server configuration with global defaults
- `ProviderConfig` - Per-provider configuration including options

### Methods
- `AgentOptions.FromDictionary()` - Create options from config dictionary
- `AgentOptions.ToDictionary()` - Convert options to dictionary
- `AgentOptions.Clone()` - Create a copy of options
- `AgentOptions.Merge()` - Merge another options object

### Tools Integration
- `Tool.Options` property provides access to agent options
- `AskUser` tool checks `Options.Interactive` to determine behavior
- Timeout handling in interactive prompts
- Auto-response mechanism for non-interactive mode
