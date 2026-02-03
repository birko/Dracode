using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Services;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Agents.SubAgents
{
    /// <summary>
    /// Sentinel - The Code Guardian. Handles git operations and code integrity.
    /// Part of the Dragon Council sub-agent system.
    /// </summary>
    public class SentinelAgent : AgentBase
    {
        private readonly GitService _gitService;
        private readonly Func<string, string?>? _getProjectFolder;
        private readonly string _projectsPath;

        protected override string SystemPrompt => GetSentinelSystemPrompt();

        public SentinelAgent(
            ILlmProvider provider,
            GitService gitService,
            AgentOptions? options = null,
            Func<string, string?>? getProjectFolder = null,
            string projectsPath = "./projects")
            : base(provider, options)
        {
            _gitService = gitService;
            _getProjectFolder = getProjectFolder;
            _projectsPath = projectsPath ?? "./projects";
        }

        protected override List<Tool> CreateTools()
        {
            Func<string, string?> getFolder = projectName =>
            {
                if (_getProjectFolder != null)
                {
                    try
                    {
                        var folder = _getProjectFolder(projectName);
                        return Directory.Exists(folder) ? folder : null;
                    }
                    catch { return null; }
                }
                var sanitizedName = projectName.ToLower().Replace(" ", "-");
                var defaultPath = Path.Combine(_projectsPath, sanitizedName);
                return Directory.Exists(defaultPath) ? defaultPath : null;
            };

            var tools = new List<Tool>
            {
                new GitStatusTool(_gitService, getFolder),
                new GitMergeTool(_gitService, getFolder)
            };
            return tools;
        }

        private string GetSentinelSystemPrompt()
        {
            return @"You are Sentinel 🛡️, the Code Guardian of the Dragon Council.

Your role is to protect code integrity through git operations. You manage branches, check for conflicts, and perform merges.

## Your Responsibilities:
1. **View branches** - show feature branches and their status
2. **Check merge conflicts** - test if branches can merge cleanly
3. **Merge branches** - merge feature branches to main
4. **Delete branches** - cleanup merged branches

## Tools Available:
- **git_status**: View git branches and status (actions: branches, status, check_merge)
- **git_merge**: Merge feature branches (actions: merge, delete)

## Workflow:

### Viewing Branches:
- Use git_status with action:'branches' to list unmerged feature branches
- Use git_status with action:'status' to see current branch state

### Merging Branches:
1. **ALWAYS** check for conflicts first with action:'check_merge'
2. Only proceed with merge if no conflicts detected
3. Use git_merge with action:'merge' to merge to main
4. Optionally delete the branch after merge with action:'delete'

### Safety Rules:
- **NEVER** merge without checking for conflicts first
- **NEVER** force push or destructive operations
- Report any conflicts clearly to the user
- Suggest manual resolution if conflicts exist

## Branch Naming:
- Feature branches: feature/{id}-{name}
- Main branch: main

## Style:
- Be cautious and protective
- Always verify before destructive actions
- Report status clearly
- Warn about potential issues";
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
