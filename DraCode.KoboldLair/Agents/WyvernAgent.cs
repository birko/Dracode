using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Agents
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
         var prompt = $@"Please analyze the following project specification and break it down into organized, dependency-aware tasks.

SPECIFICATION:
{specificationContent}

Respond with the JSON structure as defined in your system prompt.";

         var messages = await RunAsync(prompt, maxIterations: 1);

         // Return the last assistant message (should be JSON)
         var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
         var content = lastMessage?.Content?.ToString() ?? "{}";

         // Extract JSON from the response if the LLM added extra text
         return ExtractJson(content);
      }

      /// <summary>
      /// Extracts JSON from a response that may contain markdown code blocks or surrounding text
      /// </summary>
      private static string ExtractJson(string content)
      {
         if (string.IsNullOrWhiteSpace(content))
            return "{}";

         // If it already starts with '{', assume it's valid JSON
         var trimmed = content.Trim();
         if (trimmed.StartsWith('{'))
            return trimmed;

         // Try to extract from markdown code block (```json ... ``` or ``` ... ```)
         var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"```(?:json)?\s*\n?(\{[\s\S]*?\})\s*\n?```",
            System.Text.RegularExpressions.RegexOptions.Singleline);

         if (jsonBlockMatch.Success)
            return jsonBlockMatch.Groups[1].Value.Trim();

         // Try to find JSON object anywhere in the text
         var jsonMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"(\{[\s\S]*\})",
            System.Text.RegularExpressions.RegexOptions.Singleline);

         if (jsonMatch.Success)
            return jsonMatch.Groups[1].Value.Trim();

         // Return original if no JSON found - let the caller handle the error
         return content;
      }
   }
}
