using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents
{
   /// <summary>
   /// WyvernAgent is a specialized agent for analyzing project specifications.
   /// It reads specifications created by Dragon, divides work into areas (backend, frontend, etc.),
   /// and organizes tasks by dependencies.
   /// </summary>
   public class WyvernAgent : OrchestratorAgent
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
         return @"You are Wyvern 🐲, a project architect for KoboldLair. Analyze specifications and create dependency-aware task lists with optimal file structure.

## Process:
1. Parse specification: deliverables, tech stack, constraints
2. Design optimal file/folder structure based on project type
3. Categorize into areas: Backend, Frontend, Database, Infrastructure, Testing, Documentation, Security
4. Create tasks with: clear name, description, agentType (csharp/react/etc), complexity (low/medium/high)
5. Assign priority: critical (blocking/infrastructure), high (core features), normal (standard), low (polish/nice-to-have)
6. Set dependencies: foundation tasks first, use dependencyLevel (0=no deps, 1=depends on 0, etc)

## Priority Guidelines:
- **Critical**: Blocking tasks, core infrastructure, project setup, README documentation
  * Examples: project structure, core config files, essential dependencies
- **High**: Core features that are important but not blocking
  * Examples: main API endpoints, primary UI components, database schemas
- **Normal**: Standard features and functionality (default for most tasks)
  * Examples: secondary features, utility functions, standard CRUD operations
- **Low**: Nice-to-have features, polish, additional documentation
  * Examples: styling improvements, optional features, extended documentation

## REQUIRED Tasks:
- ALWAYS include a README.md task (highest priority) with instructions on how to run/use the result
- ALWAYS organize files into proper folder structures:
  * Entry point files (index.html, main.py, app.js, Program.cs, etc.) should be in the root folder
  * Web projects: js/, css/, html/, assets/, docs/
  * Backend projects: src/, tests/, docs/, config/
  * Libraries: src/, tests/, examples/, docs/
  * Adapt folder structure to project type and conventions

## File Structure Planning:
You MUST propose an optimal file structure based on the project type:
- **Web Projects**: Organize by feature or layer (components/, pages/, services/, utils/, assets/)
- **Backend APIs**: Standard structure (src/controllers/, src/models/, src/services/, tests/)
- **Libraries**: Clear separation (src/, tests/, examples/, docs/)
- **Full-stack**: Separate client/ and server/ or frontend/ and backend/
- Include naming conventions (PascalCase for C# classes, camelCase for JS, kebab-case for files)
- Specify directory purposes (what goes where)
- Provide file location guidelines for different types

## Output Format (valid JSON only, no markdown):
{
  ""projectName"": ""ProjectName"",
  ""structure"": {
    ""namingConventions"": { ""file-type"": ""convention"", ... },
    ""directoryPurposes"": { ""path/"": ""purpose"", ... },
    ""fileLocationGuidelines"": { ""type"": ""path/"", ... },
    ""architectureNotes"": ""Architecture overview and organization patterns""
  },
  ""areas"": [
    {
      ""name"": ""Documentation"",
      ""tasks"": [
        {
          ""id"": ""docs-1"",
          ""name"": ""Create README.md"",
          ""description"": ""Comprehensive README: setup, usage, dependencies, run instructions"",
          ""agentType"": ""documentation"",
          ""complexity"": ""low"",
          ""dependencies"": [],
          ""dependencyLevel"": 0,
          ""priority"": ""critical""
        }
      ]
    },
    {
      ""name"": ""Backend"",
      ""tasks"": [
        {
          ""id"": ""backend-1"",
          ""name"": ""Setup project structure"",
          ""description"": ""Create folder structure: src/, tests/, docs/, config/"",
          ""agentType"": ""csharp"",
          ""complexity"": ""low"",
          ""dependencies"": [],
          ""dependencyLevel"": 0,
          ""priority"": ""high""
        },
        { ... more tasks ... }
      ]
    },
    { ... more areas ... }
  ],
  ""totalTasks"": 15,
  ""estimatedComplexity"": ""medium""
}

Response must be pure JSON - no code blocks or explanations.";
      }

      /// <summary>
      /// Analyzes a specification and returns organized task structure
      /// </summary>
      /// <param name="specificationContent">Content of the specification file</param>
      /// <returns>JSON string with organized tasks</returns>
      public async Task<string> AnalyzeSpecificationAsync(string specificationContent)
      {
         var prompt = $@"Analyze this specification and return ONLY a JSON object (no markdown, no explanations, no text before or after):

SPECIFICATION:
{specificationContent}

IMPORTANT: Your entire response must be valid JSON starting with {{ and ending with }}. Do not include any other text.";

         var messages = await RunAsync(prompt, maxIterations: 1);

         // Return the last assistant message (should be JSON)
         var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");

         // Extract text from content blocks (Content is object but contains List<ContentBlock>)
         var content = ExtractTextFromContent(lastMessage?.Content);

         if (string.IsNullOrWhiteSpace(content))
            content = "{}";

         // Extract JSON from the response if the LLM added extra text
         return ExtractJson(content);
      }
   }
}
