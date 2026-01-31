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
Kobold (Automatic)       <- Code generation workers
```

## Project Structure

```
DraCode.KoboldLair/
├── Agents/                 # Intelligent agent implementations
│   ├── DragonAgent.cs      # Interactive requirements gathering
│   ├── WyvernAgent.cs      # Specification analysis and task delegation
│   ├── WyrmAgent.cs        # Task delegation and agent selection
│   └── Tools/              # Agent-specific tools
│       ├── FeatureManagementTool.cs
│       ├── ListProjectsTool.cs
│       ├── ProjectApprovalTool.cs
│       ├── SelectAgentTool.cs
│       ├── SpecificationManagementTool.cs
│       └── SpecificationWriterTool.cs
├── Factories/              # Agent and orchestrator creation
│   ├── DrakeFactory.cs     # Creates Drake supervisors
│   ├── KoboldFactory.cs    # Creates Kobold workers
│   ├── KoboldLairAgentFactory.cs
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

**Available Tools:**
- `ListProjectsTool` - Lists all registered projects
- `SpecificationManagementTool` - Create, update, and load specifications
- `FeatureManagementTool` - Manage features within specifications
- `ProjectApprovalTool` - Approve projects for processing
- `AddExistingProjectTool` - Register existing projects

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
