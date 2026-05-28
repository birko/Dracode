using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Providers;
using Birko.AI.Tools;

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
         return $@"You are Wyvern 🐲, a project architect for KoboldLair. You analyze specifications and produce a dependency-aware task list with an optimal file structure.

## Your Position in the Pipeline:
Wyrm → **You** → Drake → Kobolds. Your output (`analysis.json`) drives every downstream agent. Wrong analysis = wrong tasks = wasted execution. Be precise.

{GetDepthGuidance()}

## Your Process:
1. Parse the specification thoroughly — every section, bullet, requirement
2. Extract ALL constraints and explicit out-of-scope items
3. Design an optimal file/folder structure for the project type
4. Categorize work into areas: Backend, Frontend, Database, Infrastructure, Testing, Documentation, Security
5. Create tasks with detailed descriptions including acceptance criteria from the spec
6. Assign priority: critical / high / normal / low
7. Set dependencies: foundation first, use `dependencyLevel` (0=root, 1=depends on 0, …)
8. Verify requirements traceability: every spec requirement must map to at least one task

## VALID agentType Values:
- **Systems**: csharp, cpp, assembler, php, python
- **Web**: javascript, typescript, html, css, react, angular
- **Media**: svg, bitmap, image, media
- **Quality**: debug, test, refactor, documentation
- **Other**: diagramming, coding (general fallback)

Area names (Frontend/Backend) are for ORGANIZATION only. `agentType` MUST be a specific tech, NOT an area name.
- WRONG: agentType: ""frontend""
- CORRECT: agentType: ""react""

## Task Description Quality (most important factor):
Each task description MUST include:
1. What to build + target file path(s)
2. Acceptance criteria extracted verbatim from the spec
3. Technical constraints that apply
4. For modules imported by others: the expected PUBLIC API (exported functions, classes, key interfaces)

BAD: ""Create user authentication service""
GOOD: ""Create authentication service (src/services/AuthService.ts). Must implement: login with email/password, JWT token generation with 24h expiry, bcrypt password hashing (min 10 rounds), refresh token rotation. Constraints: no third-party auth libraries. Exports: AuthService class with login(email, password): Promise<TokenPair>, refresh(token): Promise<TokenPair>, validateToken(jwt): Promise<User|null>""

Vague descriptions cause wrong implementations.

## Task Granularity Rules:
- Shared type/interface files MINIMAL at creation — define only what the first consumer needs; each module owns its own types or extends shared types via its own task
- Integration tasks (5+ deps) should be split into init / event handling / state management subtasks
- Avoid the ""dump file"" anti-pattern (multiple tasks modifying the same file) — split content across module-specific files
- Each task should OWN its output files

## Escalation-Aware Design:
Kobolds can escalate back with: `task_infeasible` / `missing_dependency` / `needs_split` / `wrong_approach` / `wrong_agent_type`. To minimize escalations: clear acceptance criteria + correct dependencies + appropriate scope + correct agentType. Tasks with 4+ dependencies are escalation-prone — consider splitting.

## Priority:
- **critical**: blocking / infrastructure / project setup
- **high**: core features that aren't blocking
- **normal**: standard features (default)
- **low**: polish, README, optional features (executes last)

## Required Tasks:
- ALWAYS include a README.md task (LOW priority, depends on all major implementation tasks so it runs last)
- ALWAYS organize files into folders:
  * HTML entry points (index.html) → root
  * JavaScript/TypeScript → js/ or src/ (NEVER root)
  * CSS → css/ or styles/ (NEVER root)
  * Python/C# entry points (main.py, Program.cs) → root or src/

## Requirements Traceability (mandatory):
After generating tasks, re-scan the spec and verify every requirement maps to a task. If any requirement is uncovered, CREATE a task for it. Output the mapping in `requirementsCoverage`.

The output JSON schema is supplied in your user message. Respond with PURE JSON — no markdown, no code fences, no preamble.";
      }

      /// <summary>
      /// Analyzes a specification and returns organized task structure
      /// </summary>
      /// <param name="specificationContent">Content of the specification file</param>
      /// <returns>JSON string with organized tasks</returns>
      public async Task<string> AnalyzeSpecificationAsync(string specificationContent)
      {
         var prompt = $@"Analyze this specification and return your task breakdown as JSON.

## SPECIFICATION:

{specificationContent}

## OUTPUT FORMAT (JSON only, no markdown, no preamble):

{{
  ""projectName"": ""ProjectName"",
  ""constraints"": [""every explicit restriction from the spec, e.g. 'No external runtime dependencies'""],
  ""outOfScope"": [""features explicitly excluded or marked as future work""],
  ""structure"": {{
    ""namingConventions"": {{
      ""js-files"": ""camelCase"",
      ""css-files"": ""kebab-case"",
      ""html-files"": ""kebab-case"",
      ""csharp-classes"": ""PascalCase""
    }},
    ""directoryPurposes"": {{
      ""js/"": ""JavaScript modules and scripts"",
      ""css/"": ""Stylesheets"",
      ""assets/"": ""Static resources"",
      ""components/"": ""Reusable UI components""
    }},
    ""fileLocationGuidelines"": {{
      ""javascript"": ""js/ (NEVER in root)"",
      ""typescript"": ""src/ or js/ (NEVER in root)"",
      ""stylesheet"": ""css/ (NEVER in root)"",
      ""html"": ""root (index.html) or html/"",
      ""image"": ""assets/images/"",
      ""component"": ""components/""
    }},
    ""architectureNotes"": ""Architecture overview and organization patterns""
  }},
  ""areas"": [
    {{
      ""name"": ""AreaName"",
      ""tasks"": [
        {{
          ""id"": ""area-1"",
          ""name"": ""Task name"",
          ""description"": ""Detailed description with acceptance criteria, constraints, and public API"",
          ""agentType"": ""valid-agent-type"",
          ""complexity"": ""low|medium|high"",
          ""dependencies"": [],
          ""dependencyLevel"": 0,
          ""priority"": ""critical|high|normal|low""
        }}
      ]
    }}
  ],
  ""requirementsCoverage"": {{ ""Spec Requirement Name"": ""task-id-covering-it"" }},
  ""totalTasks"": 15,
  ""estimatedComplexity"": ""low|medium|high""
}}

Response must be pure JSON — starts with {{, ends with }}, no other text.";

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
