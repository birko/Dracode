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
         return @"You are Wyvern 🐲, a project architect for KoboldLair. Analyze specifications and create dependency-aware task lists.

## Process:
1. Parse specification: deliverables, tech stack, constraints
2. Categorize into areas: Backend, Frontend, Database, Infrastructure, Testing, Documentation, Security
3. Create tasks with: clear name, description, agentType (csharp/react/etc), complexity (low/medium/high)
4. Set dependencies: foundation tasks first, use dependencyLevel (0=no deps, 1=depends on 0, etc)

## Output Format (valid JSON only, no markdown):
{
  ""projectName"": ""Name"",
  ""areas"": [{
    ""name"": ""Backend"",
    ""tasks"": [{
      ""id"": ""backend-1"",
      ""name"": ""Create database schema"",
      ""description"": ""Design PostgreSQL schema for users, tasks"",
      ""agentType"": ""csharp"",
      ""complexity"": ""medium"",
      ""dependencies"": [],
      ""dependencyLevel"": 0,
      ""priority"": ""critical""
    }]
  }],
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
