using DraCode.Agent;
using DraCode.Agent.Agents;
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
            RebuildTools();
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
                new GitDiffTool(_gitService, getFolder),
                new GitCommitTool(_gitService, getFolder),
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
2. **Preview changes** - view diffs and commit logs before merging
3. **Create commits** - stage and commit changes in project repos
4. **Check merge conflicts** - test if branches can merge cleanly
5. **Merge branches** - merge feature branches to main
6. **Delete branches** - cleanup merged branches

## Tools Available:
- **git_status**: View git branches and status (actions: branches, status, check_merge, init)
- **git_diff**: View branch diffs, commit logs, and change summaries (actions: diff, log, summary)
- **git_commit**: Stage and commit changes (actions: commit, stage, commit_staged)
- **git_merge**: Merge feature branches (actions: merge, delete)

## Workflow:

### Viewing Branches:
- Use git_status with action:'branches' to list unmerged feature branches
- Use git_status with action:'status' to see current branch state

### Previewing Changes:
- Use git_diff with action:'summary' for a quick overview (files changed, can merge, commit count)
- Use git_diff with action:'log' to see commit history on a branch
- Use git_diff with action:'diff' to see actual code changes

### Merging Branches:
1. **ALWAYS** preview changes first with git_diff action:'summary'
2. Check for conflicts with git_status action:'check_merge'
3. Only proceed with merge if no conflicts detected
4. Use git_merge with action:'merge' to merge to main
5. Optionally delete the branch after merge with action:'delete'

### Safety Rules:
- **NEVER** merge without previewing changes and checking for conflicts first
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
        /// Process a task from Dragon coordinator with latency tracking
        /// </summary>
        public async Task<string> ProcessTaskAsync(string task, List<Message>? context = null)
        {
            var startTime = DateTime.UtcNow;
            SendMessage("debug", "[Sentinel] START | Task: " + (task.Length > 80 ? task.Substring(0, 80) + "..." : task));

            var messages = context ?? new List<Message>();
            var result = await ContinueAsync(messages, task, maxIterations: 10);

            var duration = DateTime.UtcNow - startTime;
            SendMessage("debug", $"[Sentinel] COMPLETE | Duration: {duration.TotalMilliseconds:F0}ms");

            var lastMessage = result.LastOrDefault(m => m.Role == "assistant");
            var text = OrchestratorAgent.ExtractTextFromContent(lastMessage?.Content);
            return string.IsNullOrEmpty(text) ? "Task completed." : text;
        }
    }
}
