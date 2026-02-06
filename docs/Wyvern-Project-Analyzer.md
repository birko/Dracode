# 🐲 Wyvern - Project Analyzer & Task Organizer

## Overview

Wyvern is a specialized project analyzer that reads Dragon specifications and transforms them into organized, dependency-aware task lists. **One Wyvern per project** - it categorizes work into areas (backend, frontend, etc.), identifies dependencies, and creates executable tasks for Drakes to monitor.

**Wyverns work automatically** - they're created and assigned by WyvernProcessingService (background service running every 60 seconds) when new specifications are detected.

## Architecture

```
Dragon creates specification ({ProjectsPath}/{project}/specification.md)
    ↓
ProjectService.RegisterProject() (Status: Prototype)
    ↓
User reviews spec with Dragon
    ↓
Dragon calls approve_specification tool
    ↓
Status changes to "New"
    ↓
WyvernProcessingService checks every 60 seconds
    ↓
Detects project with "New" status → Creates Wyvern instance
    ↓
WyvernAgent analyzes specification
    ↓
Categorizes into work areas:
  • Backend
  • Frontend
  • Database
  • Infrastructure
  • Testing
  • Documentation
    ↓
Ensures required tasks:
  • README.md with run/usage instructions (always included)
  • Proper folder structure (js/, css/, docs/, etc.)
    ↓
Identifies dependencies
    ↓
Orders tasks by dependency level
    ↓
Creates task files ({ProjectsPath}/{project}/{area}-tasks.md)
    ↓
DrakeMonitoringService detects tasks
    ↓
Drakes monitor & assign to Kobolds
```

### Project Status Requirements

Wyvern only processes projects with **"New"** status:

| Status | Description | Wyvern Action |
|--------|-------------|---------------|
| `Prototype` | Dragon created spec, awaiting approval | Skip |
| `New` | User approved spec | **Process** |
| `Analyzing` | Wyvern currently processing | Skip |
| `Active` | Tasks created, Kobolds working | Skip |
| `Completed` | All tasks finished | Skip |

## Components

### 1. Wyvern (`Orchestrators/Wyvern.cs`)

Main class that orchestrates the entire analysis and task creation process.

**One Wyvern per Project** - manages the complete lifecycle from specification to executable tasks.

**Key Methods:**
- `AnalyzeProjectAsync()` - Analyzes specification, returns organized structure
- `CreateTasksAsync()` - Creates Wyvern tasks for each area
- `GenerateReport()` - Creates comprehensive analysis report
- `SaveAnalysisAsync()` - Persists analysis to disk as `analysis.json` (NEW in v2.4.1)
- `LoadAnalysisAsync()` - Loads analysis from disk asynchronously (NEW in v2.4.1)
- `TryLoadAnalysis()` - Constructor-called recovery from disk on startup (NEW in v2.4.1)

**Analysis Persistence (v2.4.1):**
- Analysis is automatically saved to `{ProjectsPath}/{project}/analysis.json`
- Survives server restarts - `TryLoadAnalysis()` called on Wyvern creation
- `Analysis` property auto-loads from disk if in-memory is null
- Silent error handling - persistence failures don't disrupt operation

**Usage:**
```csharp
var wyvern = WyvernFactory.CreateWyvern(
    "my-web-app",
    "./projects/my-web-app/specification.md",
    outputPath: "./projects/my-web-app"
);

// Analyze
var analysis = await Wyvern.AnalyzeProjectAsync();
Console.WriteLine($"Found {analysis.TotalTasks} tasks across {analysis.Areas.Count} areas");

// Create tasks
var taskFiles = await Wyvern.CreateTasksAsync();
// Creates: ./projects/my-web-app/backend-tasks.md
//          ./projects/my-web-app/frontend-tasks.md
//          etc.

// Generate report
var report = Wyvern.GenerateReport();
File.WriteAllText("./projects/my-web-app/analysis.md", report);
```

### 2. WyvernAgent (`Agents/WyvernAgent.cs`)

Specialized agent that analyzes specifications and produces structured JSON output.

**System Prompt:**
- Acts as senior project architect
- Breaks down specifications methodically
- Categories work into logical areas
- Identifies task dependencies
- Orders by dependency level
- **Always includes README.md task** with run/usage instructions
- **Always organizes files** into proper folder structures
- Outputs structured JSON

**Output Format:**
```json
{
  "projectName": "Web App",
  "areas": [
    {
      "name": "Documentation",
      "tasks": [
        {
          "id": "docs-1",
          "name": "Create README.md",
          "description": "Create comprehensive README with setup instructions, usage guide, dependencies, and running instructions",
          "agentType": "documentation",
          "complexity": "low",
          "dependencies": [],
          "dependencyLevel": 0,
          "priority": "critical"
        }
      ]
    },
    {
      "name": "Backend",
      "tasks": [
        {
          "id": "backend-1",
          "name": "Setup project structure",
          "description": "Organize code into folders: src/, tests/, docs/, config/",
          "agentType": "csharp",
          "complexity": "low",
          "dependencies": [],
          "dependencyLevel": 0,
          "priority": "high"
        },
        {
          "id": "backend-2",
          "name": "Create database schema",
          "description": "Design PostgreSQL schema...",
          "agentType": "csharp",
          "complexity": "medium",
          "dependencies": ["backend-1"],
          "dependencyLevel": 1,
          "priority": "critical"
        }
      ]
    }
  ],
  "totalTasks": 15,
  "estimatedComplexity": "medium"
}
```

### 3. WyvernFactory (`Factories/WyvernFactory.cs`)

Factory for creating and managing Wyvern instances.

**Features:**
- Thread-safe with lock
- One Wyvern per project (enforced)
- Tracks all Wyverns by project name
- Configurable LLM provider

**Usage:**
```csharp
var WyvernFactory = new WyvernFactory(
    defaultProvider: "openai",
    defaultConfig: new Dictionary<string, string>
    {
        ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        ["model"] = "gpt-4o"
    }
);

// Create Wyvern
var wyvern = WyvernFactory.CreateWyvern(
    "my-project",
    "./specifications/my-project.md"
);

// Query
var existingWyvern = WyvernFactory.GetWyvern("my-project");
var allWyverns = WyvernFactory.GetAllWyverns();
Console.WriteLine($"Total Wyverns: {WyvernFactory.TotalWyverns}");
```

## Workflow

### Complete End-to-End Flow

```
1. User discusses project with Dragon
     ↓
2. Dragon creates specification
     → ./projects/web-app/specification.md
     ↓
3. Create Wyvern for project
     Wyvern = WyvernFactory.CreateWyvern("web-app", "./projects/web-app/specification.md")
     ↓
4. Wyvern analyzes specification
     analysis = await Wyvern.AnalyzeProjectAsync()
     ↓
5. Wyvern creates organized tasks
     taskFiles = await Wyvern.CreateTasksAsync()
     → ./projects/web-app/backend-tasks.md
     → ./projects/web-app/frontend-tasks.md
     → ./projects/web-app/database-tasks.md
     ↓
6. DrakeFactory creates Drakes for task files
     drake1 = drakeFactory.CreateDrake("./projects/web-app/backend-tasks.md", "backend-drake")
     drake2 = drakeFactory.CreateDrake("./projects/web-app/frontend-tasks.md", "frontend-drake")
     ↓
7. Drakes summon Kobolds
     Kobolds execute tasks
     ↓
8. DrakeMonitoringService tracks progress
     Updates task status
     Unsummons completed Kobolds
```

### Example: Web App Project

**Step 1: Dragon Specification**
```markdown
# Project: E-Commerce Web App

## Requirements
- User authentication (email/password)
- Product catalog with search
- Shopping cart
- Payment integration (Stripe)
- Admin panel

## Tech Stack
- Frontend: React + TypeScript
- Backend: ASP.NET Core API
- Database: PostgreSQL
```

**Step 2: Wyvern Analysis**
```csharp
var wyvern = WyvernFactory.CreateWyvern("ecommerce", "./projects/ecommerce/specification.md");
var analysis = await Wyvern.AnalyzeProjectAsync();
```

**Result:**
```
Areas:
- Backend (8 tasks)
  Level 0: Create database schema, Setup API project
  Level 1: Implement auth endpoints, Product CRUD
  Level 2: Shopping cart API, Payment integration
  
- Frontend (7 tasks)
  Level 0: Setup React project, Create routing
  Level 1: Auth UI, Product catalog
  Level 2: Cart UI, Checkout flow
  
- Database (3 tasks)
  Level 0: Design schema
  Level 1: Create migrations, Seed data
```

**Step 3: Create Tasks**
```csharp
var taskFiles = await Wyvern.CreateTasksAsync();
// → ./projects/ecommerce/backend-tasks.md
// → ./projects/ecommerce/frontend-tasks.md
// → ./projects/ecommerce/database-tasks.md
```

**Step 4: Drakes Monitor**
```csharp
var backendDrake = drakeFactory.CreateDrake(taskFiles["Backend"], "backend-drake");
var frontendDrake = drakeFactory.CreateDrake(taskFiles["Frontend"], "frontend-drake");

// Monitoring service runs every 60 seconds
// Kobolds get summoned and execute tasks
```

## Work Area Categorization

Wyvern divides projects into standard areas:

### Backend
- API endpoints
- Business logic
- Data access layer
- Authentication/Authorization
- Integration services

**Agent Types:** `csharp`, `cpp`, `javascript`

### Frontend
- UI components
- Pages/views
- User interactions
- State management
- Routing

**Agent Types:** `react`, `angular`, `html`, `css`, `javascript`

### Database
- Schema design
- Migrations
- Indexes
- Seed data
- Queries

**Agent Types:** `csharp`, `coding`

### Infrastructure
- Deployment configuration
- CI/CD pipelines
- Docker containers
- Cloud resources
- Monitoring setup

**Agent Types:** `coding`, `csharp`

### Testing
- Unit tests
- Integration tests
- E2E tests
- Test data
- Mocking

**Agent Types:** `csharp`, `javascript`, `react`

### Documentation
- API documentation
- User guides
- README files
- Architecture docs
- Setup instructions

**Agent Types:** `coding`, `diagramming`

### Security
- Authentication implementation
- Authorization rules
- Data encryption
- Security headers
- Vulnerability fixes

**Agent Types:** `csharp`, `javascript`

### Analysis
- Architecture diagrams
- Data flow diagrams
- ERD diagrams
- User journey maps
- System design

**Agent Types:** `diagramming`

## Dependency Management

### Dependency Levels

Wyvern organizes tasks by dependency level:

```
Level 0: No dependencies
  Can start immediately
  Foundation tasks
  
Level 1: Depends on Level 0
  Requires Level 0 complete
  Building on foundation
  
Level 2: Depends on Level 1
  Requires Levels 0 & 1
  Feature implementation
  
Level N: Depends on Level N-1
  Higher-level features
  Integration tasks
```

### Example Dependency Chain

```
Backend:
  Level 0:
    - [backend-1] Create database schema
    - [backend-2] Setup API project structure
  
  Level 1:
    - [backend-3] Implement user model (depends on: backend-1)
    - [backend-4] Create auth endpoints (depends on: backend-2, backend-3)
  
  Level 2:
    - [backend-5] Implement product catalog (depends on: backend-1, backend-4)
    - [backend-6] Shopping cart API (depends on: backend-5)
```

### Parallel Execution

Tasks at the same level with no shared dependencies can run in parallel:

```
Level 1:
  - Task A (depends on: X)
  - Task B (depends on: Y)
  - Task C (depends on: Z)
  
→ A, B, C can all run simultaneously if X, Y, Z are different
```

## Configuration

### Program.cs Registration (Optional)

```csharp
builder.Services.AddSingleton<WyvernFactory>(sp =>
{
    return new WyvernFactory(
        defaultProvider: "openai",
        defaultConfig: new Dictionary<string, string>
        {
            ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            ["model"] = "gpt-4o"
        },
        defaultOptions: new AgentOptions
        {
            WorkingDirectory = "./workspace",
            Verbose = false
        }
    );
});
```

### Environment Variables

```bash
# Required
export OPENAI_API_KEY="sk-..."

# Or alternative providers
export ANTHROPIC_API_KEY="sk-ant-..."
export AZURE_OPENAI_API_KEY="..."
export AZURE_OPENAI_ENDPOINT="https://..."
```

## Generated Reports

Wyvern generates comprehensive analysis reports:

```markdown
# 🐲 Wyvern Analysis Report: Web App

**Analyzed At:** 2026-01-26 19:00:00
**Specification:** ./specifications/web-app.md
**Total Tasks:** 15
**Estimated Complexity:** medium

## Work Areas

### Backend (8 tasks)

**Level 0** (can start immediately):
- **[backend-1]** Create database schema
  - Agent: `csharp`
  - Priority: critical, Complexity: medium
  - Description: Design PostgreSQL schema with tables...

**Level 1** (can start after level 0 completes):
- **[backend-2]** Implement auth endpoints
  - Agent: `csharp`
  - Priority: high, Complexity: medium
  - Dependencies: backend-1
  - Description: Create JWT-based authentication...

## Dependency Graph

```
Backend:
  [backend-1] Create database schema
    [backend-2] Implement auth ← backend-1
      [backend-3] Product API ← backend-1, backend-2
```
```

## API Reference

### Wyvern

```csharp
public class Wyvern
{
    // Constructor (use WyvernFactory instead)
    public Wyvern(
        string projectName,
        string specificationPath,
        WyvernAgent analyzerAgent,
        string provider,
        Dictionary<string, string> config,
        AgentOptions options,
        string outputPath
    )

    // Methods
    public async Task<WyvernAnalysis> AnalyzeProjectAsync()
    public async Task<Dictionary<string, string>> CreateTasksAsync()
    public string GenerateReport()

    // Persistence Methods (v2.4.1)
    public async Task SaveAnalysisAsync()      // Save to analysis.json
    public async Task<bool> LoadAnalysisAsync() // Load from disk
    public void TryLoadAnalysis()               // Called in constructor

    // Properties
    public WyvernAnalysis? Analysis { get; }   // Auto-loads from disk if null
    public string ProjectName { get; }
    public string SpecificationPath { get; }
    public string AnalysisPath { get; }        // Path to analysis.json (v2.4.1)
}
```

### WyvernFactory

```csharp
public class WyvernFactory
{
    // Constructor
    public WyvernFactory(
        string defaultProvider = "openai",
        Dictionary<string, string>? defaultConfig = null,
        AgentOptions? defaultOptions = null
    )

    // Methods
    public Wyvern CreateWyvern(
        string projectName,
        string specificationPath,
        string outputPath = "./tasks",
        string? provider = null
    )
    public Wyvern? GetWyvern(string projectName)
    public IEnumerable<Wyvern> GetAllWyverns()
    public bool RemoveWyvern(string projectName)

    // Properties
    public int TotalWyverns { get; }
}
```

### WyvernAnalysis

```csharp
public class WyvernAnalysis
{
    public string ProjectName { get; set; }
    public List<WorkArea> Areas { get; set; }
    public int TotalTasks { get; set; }
    public string EstimatedComplexity { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public string SpecificationPath { get; set; }
}
```

## File Structure

```
DraCode.KoboldLair/                    # Core Library
├── Agents/
│   └── WyvernAgent.cs                 # Project analyzer agent
├── Factories/
│   ├── WyvernFactory.cs               # Creates Wyvern instances
│   └── WyrmFactory.cs                 # Creates Wyrm analyzers
├── Orchestrators/
│   ├── Wyvern.cs                      # Main orchestration class
│   └── WyrmRunner.cs                  # Task running orchestrator
├── Models/
│   ├── Tasks/
│   │   ├── TaskRecord.cs              # Individual task record
│   │   └── TaskTracker.cs             # Task tracking
│   └── Agents/
│       └── WyvernAnalysis.cs          # Analysis result model

DraCode.KoboldLair.Server/             # WebSocket Server
├── Services/
│   ├── WyrmService.cs                 # Wyrm analysis service
│   └── WyvernProcessingService.cs     # Background processing (60s)
└── Program.cs                         # Service registration
```

### Data Storage

```
{ProjectsPath}/                        # Configurable, default: ./projects
├── projects.json                      # Project registry
└── {project-name}/                    # Per-project folder
    ├── specification.md               # Project specification
    ├── specification.features.json    # Feature list
    ├── {area}-tasks.md                # Task files (backend-tasks.md, etc.)
    ├── analysis.md                    # Wyvern analysis report (human-readable)
    ├── analysis.json                  # Wyvern analysis (machine-readable, v2.4.1)
    └── workspace/                     # Generated code output
```

## Summary

**Wyvern's Role in KoboldLair:**
- 🐲 **Analyzes** Dragon specifications
- 📊 **Categorizes** work into logical areas
- 🔗 **Identifies** task dependencies
- 📋 **Orders** tasks by dependency level
- 🎯 **Creates** executable tasks via Wyvern
- 🐉 **Enables** Drakes to monitor organized work

**Key Benefits:**
- ✅ One Wyvern per project (clear ownership)
- ✅ Automatic work categorization
- ✅ Dependency-aware task ordering
- ✅ Seamless integration with Drakes
- ✅ Comprehensive analysis reports

Start your project organization with Wyvern! 🐲