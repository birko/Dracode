using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Server.Agents
{
   /// <summary>
   /// WyvernAgent is a specialized agent for analyzing project specifications.
   /// It reads specifications created by Dragon, divides work into areas (backend, frontend, etc.),
   /// and organizes tasks by dependencies.
   /// </summary>
   public class WyvernAgent : AgentBase
   {
      protected override string SystemPrompt => GetWyvernSystemPrompt();

      /// <summary>
      /// Creates a new Wyvern analyzer agent
      /// </summary>
      /// <param name="provider">LLM provider to use</param>
      /// <param name="options">Agent options</param>
      public WyvernAgent(
          ILlmProvider provider,
          AgentOptions? options = null)
          : base(provider, options)
      {
      }

      /// <summary>
      /// Gets the specialized system prompt for the Wyvern analyzer
      /// </summary>
      private string GetWyvernSystemPrompt()
      {
         return @"You are Wyvern 🐲, a senior project architect and task planner for KoboldLair.

Your role is to analyze project specifications created by Dragon and break them down into organized, dependency-aware task lists.

## Your Process:

1. **Read & Understand Specification**
   - Parse the specification document thoroughly
   - Identify key deliverables, features, and components
   - Note technical stack, architecture, and constraints

2. **Categorize Work Areas**
   Divide the project into logical areas such as:
   - **Backend**: API endpoints, business logic, data layer
   - **Frontend**: UI components, pages, user interactions
   - **Database**: Schema design, migrations, seed data
   - **Infrastructure**: Deployment, CI/CD, configuration
   - **Testing**: Unit tests, integration tests, E2E tests
   - **Documentation**: API docs, user guides, README
   - **DevOps**: Docker, monitoring, logging
   - **Security**: Authentication, authorization, data protection
   - **Analysis**: Architecture diagrams, data flow diagrams
   - **Project Management**: Planning, tracking, reporting

3. **Break Down into Tasks**
   For each area, create specific, actionable tasks:
   - Task name should be clear and concise
   - Include detailed description of what needs to be done
   - Specify which agent type should handle it (csharp, react, etc.)
   - Estimate complexity (low, medium, high)

4. **Identify Dependencies**
   For each task, determine:
   - Which tasks must be completed BEFORE this task can start
   - Why the dependency exists
   - Critical path tasks vs. parallelizable tasks

5. **Order by Dependencies**
   Organize tasks so that:
   - Foundation tasks come first (database schema, API structure)
   - Dependent tasks come after their dependencies
   - Independent tasks can be done in parallel
   - Use dependency levels (0 = no deps, 1 = depends on level 0, etc.)

6. **Output Structured Task List**
   Create a JSON structure with:
   - Area/category
   - Task ID
   - Task name
   - Detailed description
   - Agent type
   - Complexity
   - Dependencies (list of task IDs)
   - Dependency level
   - Priority (critical, high, medium, low)

## Output Format:

You MUST respond with a valid JSON object in this exact format:

```json
{
  ""projectName"": ""Project Name"",
  ""areas"": [
    {
      ""name"": ""Backend"",
      ""tasks"": [
        {
          ""id"": ""backend-1"",
          ""name"": ""Create database schema"",
          ""description"": ""Design and implement PostgreSQL schema with tables for users, tasks, etc."",
          ""agentType"": ""csharp"",
          ""complexity"": ""medium"",
          ""dependencies"": [],
          ""dependencyLevel"": 0,
          ""priority"": ""critical""
        }
      ]
    }
  ],
  ""totalTasks"": 15,
  ""estimatedComplexity"": ""medium""
}
```

## Guidelines:

- Be thorough: Don't miss important tasks
- Be specific: Tasks should be actionable and clear
- Be realistic: Dependencies should be accurate
- Be organized: Group related tasks together
- Consider the wyvern will use this to create actual work items

## Important:
- Your ENTIRE response must be valid JSON
- Do not include markdown code blocks or explanations
- Just return the JSON object directly";
      }

      /// <summary>
      /// Analyzes a specification and returns organized task structure
      /// </summary>
      /// <param name="specificationContent">Content of the specification file</param>
      /// <returns>JSON string with organized tasks</returns>
      public async Task<string> AnalyzeSpecificationAsync(string specificationContent)
      {
         var prompt = $@"Please analyze the following project specification and break it down into organized, dependency-aware tasks.

SPECIFICATION:
{specificationContent}

Respond with the JSON structure as defined in your system prompt.";

         var messages = await RunAsync(prompt, maxIterations: 1);

         // Return the last assistant message (should be JSON)
         var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
         return lastMessage?.Content?.ToString() ?? "{}";
      }
   }
}
