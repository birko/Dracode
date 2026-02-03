using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Agents.SubAgents
{
    /// <summary>
    /// Seeker - The Project Scout. Discovers and analyzes existing codebases.
    /// Part of the Dragon Council sub-agent system.
    /// </summary>
    public class SeekerAgent : AgentBase
    {
        private readonly Func<string, string, string?>? _registerExistingProject;

        protected override string SystemPrompt => GetSeekerSystemPrompt();

        public SeekerAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            Func<string, string, string?>? registerExistingProject = null)
            : base(provider, options)
        {
            _registerExistingProject = registerExistingProject;
        }

        protected override List<Tool> CreateTools()
        {
            var tools = new List<Tool>
            {
                new AddExistingProjectTool(_registerExistingProject)
            };
            return tools;
        }

        private string GetSeekerSystemPrompt()
        {
            return @"You are Seeker 🔍, the Project Scout of the Dragon Council.

Your role is to discover and analyze existing codebases. You help users import their existing projects into KoboldLair.

## Your Responsibilities:
1. **Scan directories** to analyze existing codebases
2. **Identify technologies** - languages, frameworks, project structure
3. **Register projects** in KoboldLair

## Tool Available:
- **add_existing_project**: Scan and register existing projects (actions: scan, register)

## Workflow:

### Scanning a Project:
1. User provides a directory path
2. Use add_existing_project with action:'scan' to analyze the directory
3. Report findings: technologies, file structure, project type

### Registering a Project:
1. After scanning, ask user to confirm the project name
2. Use add_existing_project with action:'register' to add it to KoboldLair
3. Return the project ID to Dragon for further processing

## What You Detect:
- **Languages**: C#, TypeScript, JavaScript, Python, etc.
- **Frameworks**: .NET, React, Angular, Node.js, etc.
- **Project files**: .csproj, package.json, requirements.txt, etc.
- **Structure**: src folders, test folders, documentation

## Style:
- Be curious and thorough in analysis
- Report findings clearly
- Ask for confirmation before registering
- Suggest a good project name based on what you find";
        }

        /// <summary>
        /// Process a task from Dragon coordinator
        /// </summary>
        public async Task<string> ProcessTaskAsync(string task, List<Message>? context = null)
        {
            var messages = context ?? new List<Message>();
            var result = await ContinueAsync(messages, task, maxIterations: 10);

            var lastMessage = result.LastOrDefault(m => m.Role == "assistant");
            return ExtractTextFromContent(lastMessage?.Content);
        }

        private string ExtractTextFromContent(object? content)
        {
            if (content == null) return "Task completed.";
            if (content is string text) return text;
            if (content is ContentBlock block) return block.Text ?? "";
            if (content is IEnumerable<ContentBlock> blocks)
            {
                return string.Join("\n", blocks
                    .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                    .Select(b => b.Text));
            }
            return content.ToString() ?? "";
        }
    }
}
