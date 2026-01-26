# wyvern agent

The wyvern agent is an intelligent task router that analyzes incoming task descriptions and automatically selects the most appropriate specialized agent to handle the work. It tracks all tasks and generates a markdown status report.

## How It Works

1. **Receives a task** - You provide a natural language task description
2. **Analyzes the task** - The Wyvern examines the requirements and identifies the primary technology/goal
3. **Selects an agent** - Chooses from 10+ specialized agents based on the analysis
4. **Tracks status** - Updates task status through lifecycle: unassigned → notinitialized → working → done
5. **Generates report** - Creates a markdown file with task status table
6. **Delegates execution** - Automatically creates and runs the selected agent with the task

## Task Status Lifecycle

Tasks progress through the following states:

1. **⚪ unassigned** - Task received, no agent selected yet
2. **🔵 notinitialized** - Agent selected but not started
3. **🟡 working** - Agent is actively executing the task
4. **🟢 done** - Task completed successfully
5. **🔴 error** - An error occurred during execution

## Markdown Report Format

The Wyvern generates a markdown file with:

### Status Table
```markdown
| Task | Assigned Agent | Status |
|------|----------------|--------|
| Create React component | react | 🟢 done |
| Write C++ matrix class | cpp | 🟡 working |
| Design navigation bar | css | 🔵 notinitialized |
```

### Summary Statistics
- Total Tasks
- Count by status
- Error details (if any)

## Available Specialized Agents

- **coding** - General coding tasks, multiple languages
- **csharp** - C# and .NET development
- **cpp** - C++ development
- **assembler** - Assembly language programming
- **javascript/typescript** - Vanilla JS/TS (no frameworks)
- **css** - CSS styling and layout
- **html** - HTML markup and structure
- **react** - React development
- **angular** - Angular development
- **diagramming** - UML, ERD, DFD, user stories

## Usage Examples

### Basic Usage with Markdown Output

```csharp
using DraCode.Agent.Agents;

// Run task with automatic status tracking and markdown report
var (agentType, WyvernConv, delegatedConv, tracker) = await WyvernRunner.RunAsync(
    provider: "openai",
    task: "Create a React component for a user profile card with avatar and bio",
    options: new AgentOptions { WorkingDirectory = "./src", Verbose = true },
    config: new Dictionary<string, string> { ["apiKey"] = "your-api-key" },
    outputMarkdownPath: "./task-status.md"  // Markdown report saved here
);

Console.WriteLine($"Task was handled by: {agentType}");
// task-status.md is updated at each status transition
```

### Running Multiple Tasks

```csharp
var tasks = new[]
{
    "Create React login component",
    "Write C# authentication service",
    "Design responsive CSS layout",
    "Create UML class diagram"
};

var (results, tracker) = await WyvernRunner.RunMultipleAsync(
    provider: "openai",
    tasks: tasks,
    options: new AgentOptions { WorkingDirectory = "./src" },
    config: new Dictionary<string, string> { ["apiKey"] = "your-api-key" },
    outputMarkdownPath: "./all-tasks.md"
);

// All tasks tracked in single markdown file
Console.WriteLine($"Completed {results.Count} tasks");
```

### Manual Task Tracking

```csharp
// Use your own TaskTracker for custom tracking
var tracker = new TaskTracker();

var (agentType, _, conversation, _) = await WyvernRunner.RunAsync(
    provider: "openai",
    task: "Implement Angular service",
    tracker: tracker  // Pass your tracker
);

// Generate markdown manually
var markdown = tracker.GenerateMarkdown("My Custom Report");
Console.WriteLine(markdown);

// Or save to file
tracker.SaveToFile("./my-report.md", "My Custom Report");
```

### Getting Recommendation Only

```csharp
// Just get agent recommendation without executing
var (agentType, reasoning) = await WyvernRunner.GetRecommendationAsync(
    provider: "claude",
    task: "Optimize database queries in the user service",
    options: new AgentOptions { WorkingDirectory = "./src" },
    config: new Dictionary<string, string> { ["apiKey"] = "your-api-key" }
);

Console.WriteLine($"Recommended: {agentType}");
Console.WriteLine($"Because: {reasoning}");
// Output: "Recommended: csharp" (if project uses C#)
```

### Manual Orchestration

```csharp
// Create Wyvern manually for more control
var Wyvern = AgentFactory.Create(
    provider: "openai",
    options: new AgentOptions { WorkingDirectory = "./src", Verbose = true },
    config: new Dictionary<string, string> { ["apiKey"] = "your-api-key" },
    agentType: "Wyvern"
);

// Run Wyvern to select agent
var conversation = await Wyvern.RunAsync(
    "Build a CSS Grid layout for a dashboard with sidebar"
);

// Get the selection
var selection = SelectAgentTool.GetLastSelection();
var selectedAgent = selection["agent_type"].ToString();

// Now you can create and run the selected agent yourself
var specializedAgent = AgentFactory.Create(
    provider: "openai",
    options: new AgentOptions { WorkingDirectory = "./src", Verbose = true },
    config: new Dictionary<string, string> { ["apiKey"] = "your-api-key" },
    agentType: selectedAgent
);

await specializedAgent.RunAsync(selection["task"].ToString());
```

## Selection Examples

The Wyvern intelligently maps tasks to agents:

| Task Description | Selected Agent | Why |
|-----------------|---------------|-----|
| "Create a React hook for fetching user data" | react | Mentions React framework |
| "Write a C++ class for matrix operations" | cpp | C++ specific task |
| "Design a responsive navigation bar" | css | Styling and layout |
| "Build a REST API with ASP.NET Core" | csharp | .NET framework mentioned |
| "Create UML class diagram for the system" | diagramming | Diagram creation |
| "Write TypeScript types for API responses" | javascript | TypeScript mentioned |
| "Implement Angular service with RxJS" | angular | Angular framework |
| "Create semantic HTML for article page" | html | HTML structure focus |
| "Write x86 assembly for fast string copy" | assembler | Assembly language |
| "Refactor the authentication module" | coding | General, no specific tech |

## Benefits

✅ **Automatic Selection** - No need to manually choose the right agent  
✅ **Expert System Prompts** - Each agent has specialized knowledge  
✅ **Consistent Interface** - Same API regardless of task type  
✅ **Transparent Process** - See why each agent was selected  
✅ **Status Tracking** - Monitor task progress through lifecycle  
✅ **Markdown Reports** - Shareable task status documentation  
✅ **Multi-task Support** - Track multiple tasks in one report  
✅ **Error Handling** - Errors captured in status report  
✅ **Fallback Handling** - Defaults to general coding agent if unclear

## Configuration

The Wyvern uses the same configuration as other agents:

```csharp
var options = new AgentOptions
{
    WorkingDirectory = "./project",
    Verbose = true,
    MaxIterations = 10,
    Interactive = true,
    ModelDepth = 5  // Controls reasoning depth
};

var config = new Dictionary<string, string>
{
    ["apiKey"] = "your-api-key",
    ["model"] = "gpt-4o"  // or claude-3-5-sonnet-latest, etc.
};
```

## Tips

- **Be specific in task descriptions** - Mention frameworks, languages, or technologies for better selection
- **Trust the selection** - The Wyvern is trained to make good choices
- **Check reasoning** - Review why an agent was selected to ensure it matches intent
- **Use general "coding" for mixed tasks** - When multiple technologies are involved
