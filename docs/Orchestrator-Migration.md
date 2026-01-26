# Wyvern Migration to KoboldTown

## Overview
This document describes the migration of Wyvern components from `DraCode.Agent` to `DraCode.KoboldTown`, along with the creation of a specialized agent factory for KoboldTown.

## Changes Made

### 1. Moved Files from DraCode.Agent to DraCode.KoboldTown

The following files were removed from `DraCode.Agent\Agents` and are now exclusively in `DraCode.KoboldTown`:

- **WyvernAgent.cs** → `DraCode.KoboldTown\Agents\WyvernAgent.cs`
  - Namespace: `DraCode.KoboldTown.Agents`
  - Uses alias `using AgentBase = DraCode.Agent.Agents.Agent` to avoid namespace conflicts
  
- **WyvernRunner.cs** → `DraCode.KoboldTown\Agents\WyvernRunner.cs`
  - Namespace: `DraCode.KoboldTown.Agents`
  - Uses alias `using TaskStatus = DraCode.KoboldTown.Wyvern.TaskStatus` to avoid conflicts with `System.Threading.Tasks.TaskStatus`

- **TaskRecord.cs** → `DraCode.KoboldTown\Wyvern\TaskRecord.cs` (already existed)
- **TaskTracker.cs** → `DraCode.KoboldTown\Wyvern\TaskTracker.cs` (already existed)

### 2. Created KoboldTownAgentFactory

**Location:** `DraCode.KoboldTown\Agents\KoboldTownAgentFactory.cs`

**Purpose:** 
- Handles local creation of the `Wyvern` agent
- Delegates all other agent types to `DraCode.Agent.Agents.AgentFactory`

**Implementation:**
```csharp
public static class KoboldTownAgentFactory
{
    public static Agent Create(
        string provider,
        AgentOptions? options = null,
        Dictionary<string, string>? config = null,
        string agentType = "coding")
    {
        if (agentType.Equals("Wyvern", StringComparison.OrdinalIgnoreCase))
        {
            var llmProvider = CreateLlmProvider(provider, config);
            return new WyvernAgent(llmProvider, options, provider, config);
        }
        
        // Delegate to DraCode.Agent.AgentFactory for all other types
        return AgentFactory.Create(provider, options, config, agentType);
    }
}
```

### 3. Updated AgentFactory in DraCode.Agent

**File:** `DraCode.Agent\Agents\AgentFactory.cs`

**Changes:**
- Removed `"Wyvern"` case from the agent type switch statement
- Updated supported agent types list to exclude Wyvern
- Now supports: `coding`, `csharp`, `cpp`, `assembler`, `javascript`, `typescript`, `css`, `html`, `react`, `angular`, `diagramming`

### 4. Updated WyvernService

**File:** `DraCode.KoboldTown\Services\WyvernService.cs`

**Changes:**
- Changed using statements from `DraCode.Agent.Agents` to `DraCode.KoboldTown.Agents`
- Updated reference: `WyvernRunner.GetRecommendationAsync` → `Agents.WyvernRunner.GetRecommendationAsync`

## Architecture

### Agent Creation Flow

1. **KoboldTown requests an agent:**
   ```
   KoboldTownAgentFactory.Create("openai", options, config, "Wyvern")
   ```

2. **Factory decides:**
   - If type = "Wyvern" → Create locally using `WyvernAgent`
   - Otherwise → Delegate to `DraCode.Agent.AgentFactory.Create()`

3. **Result:**
   - wyvern agents are KoboldTown-specific
   - All specialized coding agents come from DraCode.Agent

### Namespace Organization

```
DraCode.KoboldTown
├── Agents
│   ├── WyvernAgent.cs          (KoboldTown.Agents namespace)
│   ├── WyvernRunner.cs         (KoboldTown.Agents namespace)
│   └── KoboldTownAgentFactory.cs     (KoboldTown.Agents namespace)
├── Wyvern
│   ├── TaskRecord.cs                 (KoboldTown.Wyvern namespace)
│   └── TaskTracker.cs                (KoboldTown.Wyvern namespace)
└── Services
    └── WyvernService.cs        (KoboldTown.Services namespace)

DraCode.Agent
└── Agents
    ├── Agent.cs                       (Base class)
    ├── CodingAgent.cs                 (Base for coding agents)
    ├── CSharpCodingAgent.cs
    ├── CppCodingAgent.cs
    ├── AssemblerCodingAgent.cs
    ├── JavaScriptTypeScriptCodingAgent.cs
    ├── CssCodingAgent.cs
    ├── HtmlCodingAgent.cs
    ├── ReactCodingAgent.cs
    ├── AngularCodingAgent.cs
    ├── DiagrammingAgent.cs
    └── AgentFactory.cs                (Creates specialized agents)
```

## Namespace Conflict Resolution

### Issue 1: Agent Type Conflict
**Problem:** `Agent` is both a namespace (`DraCode.Agent`) and a class name, causing ambiguity in `DraCode.KoboldTown.Agents` namespace.

**Solution:**
```csharp
using AgentBase = DraCode.Agent.Agents.Agent;

public class WyvernAgent : AgentBase
```

### Issue 2: TaskStatus Conflict
**Problem:** `TaskStatus` exists in both `System.Threading.Tasks` and `DraCode.KoboldTown.Wyvern`.

**Solution:**
```csharp
using TaskStatus = DraCode.KoboldTown.Wyvern.TaskStatus;
```

## Benefits

1. **Clear Separation of Concerns:**
   - Wyvern is KoboldTown-specific functionality
   - Specialized coding agents remain reusable across projects

2. **Flexible Agent Creation:**
   - KoboldTown can create wyvern agents locally
   - Falls back to DraCode.Agent for specialized agents
   - Easy to extend with more KoboldTown-specific agents

3. **Maintainability:**
   - Wyvern code lives with the service that uses it
   - DraCode.Agent focuses on core agent framework and specialized agents
   - Clear dependencies: KoboldTown depends on DraCode.Agent, not vice versa

## Testing

All projects build successfully:
```bash
dotnet build DraCode.Agent\DraCode.Agent.csproj         # ✓ Success
dotnet build DraCode.KoboldTown\DraCode.KoboldTown.csproj  # ✓ Success
dotnet build DraCode.slnx                                  # ✓ Success
```

## Future Enhancements

Potential additions to KoboldTownAgentFactory:
- Custom agents specific to KoboldTown workflows
- Agent wrappers with additional logging/monitoring
- Agent pools for resource management
- Custom tool sets for KoboldTown-specific operations

## Related Documentation

- [WyvernAgent.md](./WyvernAgent.md) - Wyvern usage guide
- [TaskTracking.md](./TaskTracking.md) - Task tracking system
- [KoboldTown-Summary.md](./KoboldTown-Summary.md) - Complete KoboldTown overview
