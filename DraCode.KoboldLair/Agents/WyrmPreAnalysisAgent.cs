using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// Specialized agent for Wyrm pre-analysis of project specifications.
    /// Reads specifications and produces structured JSON recommendations (wyrm-recommendation.json)
    /// that guide downstream agents (Wyvern, Drake, Kobolds).
    ///
    /// This is distinct from WyrmAgent, which is used by Drake for task delegation (agent type selection).
    /// </summary>
    public class WyrmPreAnalysisAgent : OrchestratorAgent
    {
        public WyrmPreAnalysisAgent(ILlmProvider provider, AgentOptions? options = null)
            : base(provider, options)
        {
        }

        protected override string SystemPrompt => GetWyrmPreAnalysisSystemPrompt();

        protected override List<Tool> CreateTools()
        {
            // No tools needed - Wyrm pre-analysis only reads the specification
            // (passed as user message) and outputs JSON recommendations.
            return new List<Tool>();
        }

        private string GetWyrmPreAnalysisSystemPrompt()
        {
            return $@"You are Wyrm 🐍, a technical pre-analyzer for the KoboldLair multi-agent system.

## Your Role in the Pipeline:
You are the FIRST agent to analyze a new project specification. Your recommendations guide ALL downstream agents:
- **Wyvern** uses your output to create task breakdowns
- **Drake** uses your constraints to enforce spec compliance
- **Kobolds** see your constraints as mandatory rules they must not violate

Your analysis quality directly impacts every subsequent step. Be thorough and precise.

{GetDepthGuidance()}

## Your Responsibilities:
1. Read the specification THOROUGHLY — every section, bullet point, and requirement
2. Extract ALL programming languages, frameworks, libraries, and tools mentioned
3. Identify EVERY explicit constraint and restriction (""no frameworks"", ""vanilla only"", etc.)
4. Identify features explicitly marked as out of scope
5. Recommend appropriate specialized agent types for each work area
6. Assess project complexity
7. Define verification steps for the detected tech stack

## VALID Agent Types (use ONLY these for RecommendedAgentTypes):
- **Systems**: csharp, cpp, assembler, php, python
- **Web**: javascript, typescript, html, css, react, angular
- **Media**: svg, bitmap, image, media
- **Quality**: debug, test, refactor, documentation
- **Other**: diagramming, coding (general fallback)

IMPORTANT: Agent types are SPECIFIC technologies, NOT area names.
- WRONG: ""frontend"", ""backend"", ""database""
- CORRECT: ""react"", ""csharp"", ""typescript""

## Output Rules:
- Your response MUST be pure JSON — no markdown code blocks, no explanations, no text before or after
- Every field must be populated accurately based on the specification
- Constraints and OutOfScope are CRITICAL — downstream agents rely on them to avoid spec violations

## Verification Steps:
Include verification commands appropriate for the detected tech stack:
- checkType: ""build"" | ""test"" | ""lint"" | ""syntax""
- command: Shell command to execute
- successCriteria: ""exit_code_0"" or ""contains:expected text""
- priority: ""Critical"" (builds), ""High"" (tests), ""Medium"" (lint)
- timeoutSeconds: Command timeout (default: 300)
- description: What this check validates

Examples:
- .NET: {{checkType:""build"", command:""dotnet build"", priority:""Critical""}}
- Node.js/TypeScript: {{checkType:""build"", command:""npx tsc --noEmit"", priority:""Critical""}}
- Python: {{checkType:""test"", command:""pytest"", priority:""High""}}";
        }

        /// <summary>
        /// Analyzes a specification and returns structured recommendations as JSON.
        /// </summary>
        /// <param name="specificationContent">Content of the specification file</param>
        /// <param name="crossProjectContext">Optional cross-project learning context</param>
        /// <returns>JSON string with Wyrm recommendations</returns>
        public async Task<string> AnalyzeSpecificationAsync(string specificationContent, string? crossProjectContext = null)
        {
            var prompt = $@"Analyze this project specification and return your recommendations as JSON.

## SPECIFICATION:

{specificationContent}
{crossProjectContext ?? ""}

## OUTPUT FORMAT (JSON only):

{{
  ""AnalysisSummary"": ""2-3 sentence summary of what this project IS and its key technical approach"",
  ""RecommendedLanguages"": [""list every programming language explicitly mentioned - e.g. typescript, python, csharp""],
  ""RecommendedAgentTypes"": {{
    ""area-name"": ""agent-type""
  }},
  ""TechnicalStack"": [""every framework, library, build tool, API mentioned - e.g. Vite, BroadcastChannel API, localStorage""],
  ""SuggestedAreas"": [""work areas like Backend, Frontend, Database, Infrastructure""],
  ""Complexity"": ""Low | Medium | High"",
  ""Constraints"": [""every explicit constraint or restriction from the spec - e.g. No external runtime dependencies, No CSS frameworks""],
  ""OutOfScope"": [""features explicitly marked as out of scope or future work""],
  ""VerificationSteps"": [],
  ""Notes"": ""any additional observations""
}}

## CRITICAL RULES:

1. **AnalysisSummary MUST NOT be empty**. Summarize the project in 2-3 sentences.
2. **RecommendedLanguages**: List SPECIFIC languages from the spec (typescript, css, html, python, csharp, etc.), NOT ""general"".
3. **RecommendedAgentTypes**: Map each work area to a VALID agent type:
   - Systems: csharp, cpp, assembler, php, python
   - Web: javascript, typescript, html, css, react, angular
   - Media: svg, bitmap, image, media
   - Other: diagramming, coding (general fallback), documentation
   - Example: {{""typescript-modules"": ""typescript"", ""html-pages"": ""html"", ""styling"": ""css"", ""docs"": ""documentation""}}
   - Do NOT use area names (""frontend"", ""backend"") as agent types.
4. **TechnicalStack MUST NOT be empty** if the spec mentions ANY technology. Extract build tools, APIs, patterns.
5. **Constraints**: Extract EVERY restriction (""no frameworks"", ""vanilla only"", ""no server"", etc.). This is critical - downstream agents will use these to avoid spec violations.
6. **OutOfScope**: Extract features explicitly excluded. Downstream agents must NOT implement these.

Response must be pure JSON - no code blocks or explanations.";

            var messages = await RunAsync(prompt, maxIterations: 1);

            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            var content = ExtractTextFromContent(lastMessage?.Content);

            if (string.IsNullOrWhiteSpace(content))
                content = "{}";

            return ExtractJson(content);
        }
    }
}
