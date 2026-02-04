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

      /// <summary>
      /// Extracts JSON from a response that may contain markdown code blocks or surrounding text
      /// </summary>
      private static string ExtractJson(string content)
      {
         if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Wyvern returned empty response. The LLM may have failed to generate an analysis.");

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

         // No JSON found - provide helpful error with preview of what was received
         var preview = content.Length > 100 ? content.Substring(0, 100) + "..." : content;
         throw new InvalidOperationException($"Wyvern did not return valid JSON. Response started with: {preview}");
      }

      /// <summary>
      /// Extracts text content from a Message.Content object which may be various types.
      /// Handles List&lt;ContentBlock&gt;, IEnumerable&lt;ContentBlock&gt;, string, and other edge cases
      /// without falling back to ToString() which would produce type names.
      /// </summary>
      private static string ExtractTextFromContent(object? content)
      {
         if (content == null)
            return string.Empty;

         // Direct string content
         if (content is string text)
            return text;

         // Single ContentBlock
         if (content is ContentBlock block)
            return block.Text ?? string.Empty;

         // List<ContentBlock> or IEnumerable<ContentBlock>
         if (content is IEnumerable<ContentBlock> contentBlocks)
         {
            return string.Join("\n", contentBlocks
                .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                .Select(b => b.Text));
         }

         // Handle case where Content was serialized/deserialized as List<object> or JsonElement
         if (content is System.Text.Json.JsonElement jsonElement)
         {
            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
               var texts = new List<string>();
               foreach (var element in jsonElement.EnumerateArray())
               {
                  if (element.TryGetProperty("type", out var typeEl) &&
                      typeEl.GetString() == "text" &&
                      element.TryGetProperty("text", out var textEl))
                  {
                     var t = textEl.GetString();
                     if (!string.IsNullOrEmpty(t))
                        texts.Add(t);
                  }
               }
               return string.Join("\n", texts);
            }
            else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
               return jsonElement.GetString() ?? string.Empty;
            }
         }

         // Handle IEnumerable<object> which might contain ContentBlocks or anonymous objects
         if (content is IEnumerable<object> objects)
         {
            var texts = new List<string>();
            foreach (var obj in objects)
            {
               if (obj is ContentBlock cb && cb.Type == "text" && !string.IsNullOrEmpty(cb.Text))
               {
                  texts.Add(cb.Text);
               }
               else if (obj is string str)
               {
                  texts.Add(str);
               }
               // Check for anonymous object or dynamic with Type and Text properties
               else if (obj != null)
               {
                  var objType = obj.GetType();
                  var typeProp = objType.GetProperty("Type") ?? objType.GetProperty("type");
                  var textProp = objType.GetProperty("Text") ?? objType.GetProperty("text");

                  if (typeProp != null && textProp != null)
                  {
                     var typeVal = typeProp.GetValue(obj)?.ToString();
                     if (typeVal == "text")
                     {
                        var textVal = textProp.GetValue(obj)?.ToString();
                        if (!string.IsNullOrEmpty(textVal))
                           texts.Add(textVal);
                     }
                  }
               }
            }
            if (texts.Count > 0)
               return string.Join("\n", texts);
         }

         // Last resort: if content looks like it might already be JSON or text, return it
         // But NEVER call ToString() on collection types as that produces type names
         var contentType = content.GetType();
         if (contentType.IsGenericType &&
             (contentType.GetGenericTypeDefinition() == typeof(List<>) ||
              contentType.Name.Contains("Enumerable")))
         {
            // This is a collection we couldn't process - return empty rather than type name
            return string.Empty;
         }

         // For primitive types and simple objects, ToString is safe
         return content.ToString() ?? string.Empty;
      }
   }
}
