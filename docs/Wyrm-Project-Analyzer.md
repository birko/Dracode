# 🐲 Wyrm - Project Analyzer & Task Organizer

## Overview

Wyrm is a specialized project analyzer that reads Dragon specifications and transforms them into organized, dependency-aware task lists. **One Wyrm per project** - it categorizes work into areas (backend, frontend, etc.), identifies dependencies, and creates executable tasks for Drakes to monitor.

**Wyrms work automatically** - they're created and assigned by WyrmProcessingService (background service running every 60 seconds) when new specifications are detected.

## Architecture

```
Dragon creates specification (./specifications/*.md)
    ↓
ProjectService.RegisterProject() (automatic)
    ↓
WyrmProcessingService checks every 60 seconds
    ↓
Detects new project → Creates Wyrm instance
    ↓
WyrmAnalyzerAgent analyzes specification
    ↓
Categorizes into work areas:
  • Backend
  • Frontend
  • Database
  • Infrastructure
  • Testing
  • Documentation
    ↓
Identifies dependencies
    ↓
Orders tasks by dependency level
    ↓
Creates task files (./workspace/{project}/*-tasks.md)
    ↓
DrakeMonitoringService detects tasks
    ↓
Drakes monitor & assign to Kobolds
```

## Components

### 1. Wyrm (`Projects/Wyrm.cs`)

Main class that orchestrates the entire analysis and task creation process.

**One Wyrm per Project** - manages the complete lifecycle from specification to executable tasks.

**Key Methods:**
- `AnalyzeProjectAsync()` - Analyzes specification, returns organized structure
- `CreateTasksAsync()` - Creates Wyvern tasks for each area
- `GenerateReport()` - Creates comprehensive analysis report

**Usage:**
```csharp
var wyrm = wyrmFactory.CreateWyrm(
    "my-web-app",
    "./specifications/web-app-spec.md",
    outputPath: "./tasks"
);

// Analyze
var analysis = await wyrm.AnalyzeProjectAsync();
Console.WriteLine($"Found {analysis.TotalTasks} tasks across {analysis.Areas.Count} areas");

// Create tasks
var taskFiles = await wyrm.CreateTasksAsync();
// Creates: ./tasks/my-web-app-backend-tasks.md
//          ./tasks/my-web-app-frontend-tasks.md
//          etc.

// Generate report
var report = wyrm.GenerateReport();
File.WriteAllText("./wyrm-analysis-report.md", report);
```

### 2. WyrmAnalyzerAgent (`Agents/WyrmAnalyzerAgent.cs`)

Specialized agent that analyzes specifications and produces structured JSON output.

**System Prompt:**
- Acts as senior project architect
- Breaks down specifications methodically
- Categories work into logical areas
- Identifies task dependencies
- Orders by dependency level
- Outputs structured JSON

**Output Format:**
```json
{
  "projectName": "Web App",
  "areas": [
    {
      "name": "Backend",
      "tasks": [
        {
          "id": "backend-1",
          "name": "Create database schema",
          "description": "Design PostgreSQL schema...",
          "agentType": "csharp",
          "complexity": "medium",
          "dependencies": [],
          "dependencyLevel": 0,
          "priority": "critical"
        }
      ]
    }
  ],
  "totalTasks": 15,
  "estimatedComplexity": "medium"
}
```

### 3. WyrmFactory (`Factories/WyrmFactory.cs`)

Factory for creating and managing Wyrm instances.

**Features:**
- Thread-safe with lock
- One Wyrm per project (enforced)
- Tracks all Wyrms by project name
- Configurable LLM provider

**Usage:**
```csharp
var wyrmFactory = new WyrmFactory(
    defaultProvider: "openai",
    defaultConfig: new Dictionary<string, string>
    {
        ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        ["model"] = "gpt-4o"
    }
);

// Create Wyrm
var wyrm = wyrmFactory.CreateWyrm(
    "my-project",
    "./specifications/my-project.md"
);

// Query
var existingWyrm = wyrmFactory.GetWyrm("my-project");
var allWyrms = wyrmFactory.GetAllWyrms();
Console.WriteLine($"Total Wyrms: {wyrmFactory.TotalWyrms}");
```

## Workflow

### Complete End-to-End Flow

```
1. User discusses project with Dragon
     ↓
2. Dragon creates specification
     → ./specifications/web-app.md
     ↓
3. Create Wyrm for project
     wyrm = wyrmFactory.CreateWyrm("web-app", "./specifications/web-app.md")
     ↓
4. Wyrm analyzes specification
     analysis = await wyrm.AnalyzeProjectAsync()
     ↓
5. Wyrm creates organized tasks
     taskFiles = await wyrm.CreateTasksAsync()
     → ./tasks/web-app-backend-tasks.md
     → ./tasks/web-app-frontend-tasks.md
     → ./tasks/web-app-database-tasks.md
     ↓
6. DrakeFactory creates Drakes for task files
     drake1 = drakeFactory.CreateDrake("./tasks/web-app-backend-tasks.md", "backend-drake")
     drake2 = drakeFactory.CreateDrake("./tasks/web-app-frontend-tasks.md", "frontend-drake")
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

**Step 2: Wyrm Analysis**
```csharp
var wyrm = wyrmFactory.CreateWyrm("ecommerce", "./specifications/ecommerce.md");
var analysis = await wyrm.AnalyzeProjectAsync();
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
var taskFiles = await wyrm.CreateTasksAsync();
// → ./tasks/ecommerce-backend-tasks.md
// → ./tasks/ecommerce-frontend-tasks.md
// → ./tasks/ecommerce-database-tasks.md
```

**Step 4: Drakes Monitor**
```csharp
var backendDrake = drakeFactory.CreateDrake(taskFiles["Backend"], "backend-drake");
var frontendDrake = drakeFactory.CreateDrake(taskFiles["Frontend"], "frontend-drake");

// Monitoring service runs every 60 seconds
// Kobolds get summoned and execute tasks
```

## Work Area Categorization

Wyrm divides projects into standard areas:

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

Wyrm organizes tasks by dependency level:

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
builder.Services.AddSingleton<WyrmFactory>(sp =>
{
    return new WyrmFactory(
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

Wyrm generates comprehensive analysis reports:

```markdown
# 🐲 Wyrm Analysis Report: Web App

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

### Wyrm

```csharp
public class Wyrm
{
    // Constructor (use WyrmFactory instead)
    public Wyrm(
        string projectName,
        string specificationPath,
        WyrmAnalyzerAgent analyzerAgent,
        string provider,
        Dictionary<string, string> config,
        AgentOptions options,
        string outputPath
    )

    // Methods
    public async Task<WyrmAnalysis> AnalyzeProjectAsync()
    public async Task<Dictionary<string, string>> CreateTasksAsync()
    public string GenerateReport()

    // Properties
    public WyrmAnalysis? Analysis { get; }
    public string ProjectName { get; }
    public string SpecificationPath { get; }
}
```

### WyrmFactory

```csharp
public class WyrmFactory
{
    // Constructor
    public WyrmFactory(
        string defaultProvider = "openai",
        Dictionary<string, string>? defaultConfig = null,
        AgentOptions? defaultOptions = null
    )

    // Methods
    public Wyrm CreateWyrm(
        string projectName,
        string specificationPath,
        string outputPath = "./tasks",
        string? provider = null
    )
    public Wyrm? GetWyrm(string projectName)
    public IEnumerable<Wyrm> GetAllWyrms()
    public bool RemoveWyrm(string projectName)

    // Properties
    public int TotalWyrms { get; }
}
```

### WyrmAnalysis

```csharp
public class WyrmAnalysis
{
    public string ProjectName { get; set; }
    public List<WorkArea> Areas { get; set; }
    public int TotalTasks { get; set; }
    public string EstimatedComplexity { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public string SpecificationPath { get; set; }
}
```

## Summary

**Wyrm's Role in KoboldTown:**
- 🐲 **Analyzes** Dragon specifications
- 📊 **Categorizes** work into logical areas
- 🔗 **Identifies** task dependencies
- 📋 **Orders** tasks by dependency level
- 🎯 **Creates** executable tasks via Wyvern
- 🐉 **Enables** Drakes to monitor organized work

**Key Benefits:**
- ✅ One Wyrm per project (clear ownership)
- ✅ Automatic work categorization
- ✅ Dependency-aware task ordering
- ✅ Seamless integration with Drakes
- ✅ Comprehensive analysis reports

Start your project organization with Wyrm! 🐲