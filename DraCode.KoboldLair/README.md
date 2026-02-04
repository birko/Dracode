# DraCode.KoboldLair

A multi-agent orchestration library for intelligent project requirements gathering, specification analysis, task decomposition, and automated code generation.

## Overview

KoboldLair implements a hierarchical agent system where different agent types handle specific stages of the software development workflow:

```
Dragon (Interactive)     <- User's touchpoint for requirements gathering
    |
    v Creates specification
Wyvern (Automatic)       <- Analyzes specs, creates task breakdown
    |
    v Creates task files
Drake (Automatic)        <- Supervises task execution
    |
    v Assigns and monitors
Kobold Planner (Automatic) <- Creates implementation plans
    |
    v Plans ready for execution
Kobold (Automatic)       <- Executes plans step-by-step
```

## Project Structure

```
DraCode.KoboldLair/
├── Agents/                 # Intelligent agent implementations
│   ├── DragonAgent.cs      # Interactive requirements gathering
│   ├── WyvernAgent.cs      # Specification analysis and task delegation
│   ├── WyrmAgent.cs        # Task delegation and agent selection
│   ├── KoboldPlannerAgent.cs  # Implementation planning before execution
│   ├── SubAgents/          # Dragon Council members
│   │   ├── SageAgent.cs       # Specifications, features, approval
│   │   ├── SeekerAgent.cs     # Scan/import existing codebases
│   │   ├── SentinelAgent.cs   # Git operations, branches, merging
│   │   └── WardenAgent.cs     # Agent config, limits, external paths
│   └── Tools/              # Agent-specific tools
│       ├── AddExistingProjectTool.cs        # Import existing projects
│       ├── AgentConfigurationTool.cs        # Configure agent providers
│       ├── CreateImplementationPlanTool.cs  # Kobold Planner tool
│       ├── DelegateToCouncilTool.cs         # Dragon council delegation
│       ├── ExternalPathTool.cs              # Manage allowed external paths
│       ├── FeatureManagementTool.cs
│       ├── GitMergeTool.cs         # Merge feature branches to main
│       ├── GitStatusTool.cs        # View branch status and merge readiness
│       ├── ListProjectsTool.cs
│       ├── ProjectApprovalTool.cs
│       ├── SelectAgentTool.cs
│       └── SpecificationManagementTool.cs
├── Factories/              # Agent and orchestrator creation
│   ├── DrakeFactory.cs     # Creates Drake supervisors
│   ├── KoboldFactory.cs    # Creates Kobold workers
│   ├── WyrmFactory.cs      # Creates Wyrm analyzers
│   └── WyvernFactory.cs    # Creates Wyvern orchestrators
├── Models/                 # Data models
│   ├── Agents/             # Agent-related models (Kobold, DragonMessage, etc.)
│   ├── Configuration/      # Configuration models
│   ├── Projects/           # Project and specification models
│   └── Tasks/              # Task and feature models
├── Orchestrators/          # Orchestration logic
│   ├── Drake.cs            # Task execution supervisor
│   ├── Wyvern.cs           # Project analyzer
│   └── WyrmRunner.cs       # Task delegation runner
└── Services/               # Business logic layer
    ├── GitService.cs       # Git operations (branch, merge, commit)
    ├── ProjectService.cs   # Project lifecycle management
    ├── ProjectRepository.cs # Data persistence
    ├── ProjectConfigurationService.cs
    └── ProviderConfigurationService.cs
```

## Agents

### Dragon

The user-facing agent for interactive requirements gathering.

**Responsibilities:**
- Conducts conversations to understand project requirements
- Creates and manages project specifications
- Manages features within specifications
- Supports multi-session with persistent conversation history

**Available Tools (Dragon):**
- `ListProjectsTool` - Lists all registered projects
- `DelegateToCouncilTool` - Routes tasks to council members

**Dragon Council (Sub-Agents):**

| Agent | Tools | Responsibility |
|-------|-------|----------------|
| **SageAgent** | `SpecificationManagementTool`, `FeatureManagementTool`, `ProjectApprovalTool` | Specifications, features, approval |
| **SeekerAgent** | `AddExistingProjectTool` | Scan and import existing codebases |
| **SentinelAgent** | `GitStatusTool`, `GitMergeTool` | Git operations, branches, merging |
| **WardenAgent** | `ExternalPathTool`, `AgentConfigurationTool`, `SelectAgentTool` | Config, limits, external paths |

### Wyvern

Analyzes project specifications and organizes work into tasks.

**Responsibilities:**
- Analyzes specifications created by Dragon
- Breaks down work into areas and task lists
- Delegates to appropriate specialized agents
- Generates analysis reports

**Specialized Agents Available:**
- **Coding:** `coding`, `csharp`, `cpp`, `assembler`
- **Web:** `javascript`, `typescript`, `css`, `html`, `react`, `angular`, `php`, `python`
- **Media:** `media`, `image`, `svg`, `bitmap`
- **Other:** `diagramming`, `wyrm`

### Wyrm

Handles task delegation and agent selection.

**Responsibilities:**
- Analyzes task descriptions
- Recommends appropriate agent types
- Provides reasoning for agent selection

### Kobold Planner

Creates implementation plans before Kobold task execution.

**Responsibilities:**
- Analyzes tasks and breaks them into atomic steps
- Identifies files to create and modify for each step
- Orders steps by dependencies
- Enables resumability if execution is interrupted

**Available Tools:**
- `CreateImplementationPlanTool` - Creates structured implementation plans

**Benefits:**
- **Visibility**: See what steps Kobold will perform before execution
- **Resumability**: Restart from last completed step after interruption
- **Quality**: Better structured approach to complex tasks
- **Debugging**: Easier to identify where issues occur

**Configuration:**
```json
{
  "KoboldLair": {
    "Planning": {
      "Enabled": true,
      "PlannerProvider": null,
      "PlannerModel": null,
      "MaxPlanningIterations": 5,
      "SavePlanProgress": true,
      "ResumeFromPlan": true
    }
  }
}
```

## Orchestrators

### Drake

Supervises Kobold workers and manages task execution.

**Key Features:**
- Summons Kobolds for task execution
- Enforces per-project resource limits
- Monitors task status and completion
- Syncs task status from Kobold state

### Wyvern

Analyzes specifications and creates organized task lists.

**Key Features:**
- Loads and analyzes specifications
- Creates work areas with dependencies
- Supports incremental area reprocessing
- Tracks feature status based on task completion

## Models

### Project Lifecycle

```
Prototype -> New -> WyvernAssigned -> Analyzed -> InProgress -> Completed
                                          |
                                          v
                                 SpecificationModified (triggers reprocessing)
```

### Feature Lifecycle

```
New -> AssignedToWyvern -> InProgress -> Completed
```

### Kobold Lifecycle

```
Unassigned -> Assigned -> Working -> Done
```

### Task Lifecycle

```
Unassigned -> NotInitialized -> Working -> Done
```

## Services

### ProjectService

Core service for project lifecycle management.

```csharp
// Create a new project folder
var folder = projectService.CreateProjectFolder("my-project");

// Register a project
await projectService.RegisterProject(specification, projectFolder);

// Assign a Wyvern for analysis
await projectService.AssignWyvernAsync(projectId);

// Analyze the project
var analysis = await projectService.AnalyzeProjectAsync(projectId);

// Approve a project (Prototype -> New)
await projectService.ApproveProject(projectId);
```

### ProjectRepository

JSON-based persistence for project data.

```csharp
// Get all projects
var projects = projectRepository.GetAll();

// Get by status
var newProjects = projectRepository.GetByStatus(ProjectStatus.New);

// Get multiple statuses
var activeProjects = projectRepository.GetByStatuses(
    ProjectStatus.New,
    ProjectStatus.InProgress
);
```

## Factories

### KoboldFactory

Creates and manages Kobold workers with resource limits.

```csharp
// Check if we can create more Kobolds
if (koboldFactory.CanCreateKoboldForProject(projectId, maxParallel))
{
    var kobold = koboldFactory.CreateKobold("csharp", provider, projectId);
}

// Get statistics
var stats = koboldFactory.GetStatistics();
```

### DrakeFactory

Creates Drake supervisors for task monitoring.

```csharp
var drake = drakeFactory.CreateDrake(
    taskFilePath: "./projects/my-app/backend-tasks.md",
    specificationPath: "./projects/my-app/specification.md",
    projectId: "my-app",
    provider: provider
);
```

### WyvernFactory

Creates Wyvern orchestrators for project analysis.

```csharp
var wyvern = wyvernFactory.CreateWyvern(
    projectId: "my-app",
    specificationPath: "./projects/my-app/specification.md"
);
```

## Configuration

### KoboldLairConfiguration

```json
{
  "KoboldLair": {
    "ProjectsPath": "./projects",
    "DefaultProvider": "openai",
    "Providers": [
      {
        "Name": "openai",
        "Type": "OpenAI",
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4"
      }
    ],
    "Limits": {
      "MaxParallelKobolds": 5,
      "MaxParallelDrakes": 3,
      "MaxParallelWyrms": 2,
      "MaxParallelWyverns": 2
    }
  }
}
```

## Data Storage

Projects are stored in a consolidated folder structure:

```
{ProjectsPath}/                      # Default: ./projects
├── projects.json                    # Project registry
└── {project-name}/                  # Per-project folder
    ├── specification.md             # Project specification
    ├── specification.features.json  # Feature list
    ├── {area}-tasks.md              # Task files (e.g., backend-tasks.md)
    ├── analysis.md                  # Wyvern analysis report
    └── workspace/                   # Generated code output
```

## Allowed External Paths

Projects can access directories outside their workspace through the allowed external paths feature.

### Managing External Paths

```csharp
// Add an allowed path
projectConfigService.AddAllowedExternalPath(projectId, "/shared/libraries");

// Remove an allowed path
projectConfigService.RemoveAllowedExternalPath(projectId, "/shared/libraries");

// Get allowed paths for a project
var paths = projectConfigService.GetAllowedExternalPaths(projectId);
```

### Dragon Tool Usage

In Dragon chat:
- "Show allowed paths for project" - Lists current allowed paths
- "Add external path /my/shared/code" - Grants access to a directory
- "Remove external path /my/shared/code" - Revokes access

### Security

- Paths are validated by PathHelper.IsPathSafe()
- Both workspace and allowed external paths are checked
- Kobolds inherit project's allowed paths during execution

## Dependencies

- `DraCode.Agent` - Base agent class and LLM providers
- `Microsoft.Extensions.Logging.Abstractions` - Logging
- `Microsoft.Extensions.Options` - Configuration/DI

## Thread Safety

The library is designed for concurrent use:

- `TaskTracker` uses locks for concurrent task updates
- `KoboldFactory` uses `ConcurrentDictionary` for thread-safe Kobold tracking
- `ProjectRepository` uses locks for file operations
- All factories use locks for registry access

## Integration

KoboldLair integrates with:

- **DraCode.KoboldLair.Server** - WebSocket hosting for real-time communication
- **DraCode.KoboldLair.Client** - Web UI for interacting with agents
- **DraCode.Agent** - Core agent infrastructure and LLM providers

## License

Part of the DraCode project.
