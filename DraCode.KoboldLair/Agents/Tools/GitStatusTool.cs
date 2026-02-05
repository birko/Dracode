using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for Dragon to view git status, branches, and check merge readiness
    /// </summary>
    public class GitStatusTool : Tool
    {
        private readonly GitService? _gitService;
        private readonly Func<string, string?>? _getProjectFolder;

        public GitStatusTool(GitService? gitService, Func<string, string?>? getProjectFolder = null)
        {
            _gitService = gitService;
            _getProjectFolder = getProjectFolder;
        }

        public override string Name => "git_status";

        public override string Description =>
            "View git status for a project: list unmerged feature branches, check current status, or test if a branch can be merged cleanly.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'branches' (list unmerged feature branches), 'status' (current branch and changes), 'check_merge' (test if branch can merge without conflicts)",
                    @enum = new[] { "branches", "status", "check_merge" }
                },
                project_name = new
                {
                    type = "string",
                    description = "Name of the project"
                },
                branch_name = new
                {
                    type = "string",
                    description = "Branch name (required for check_merge action)"
                }
            },
            required = new[] { "action", "project_name" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (_gitService == null)
            {
                return "Git integration is not available.";
            }

            if (!input.TryGetValue("action", out var actionObj) || !input.TryGetValue("project_name", out var projectNameObj))
            {
                return "Error: action and project_name are required";
            }

            var action = actionObj.ToString()?.ToLower();
            var projectName = projectNameObj.ToString() ?? "";

            // Get project folder
            var projectFolder = _getProjectFolder?.Invoke(projectName);
            if (string.IsNullOrEmpty(projectFolder))
            {
                return $"Error: Could not find project folder for '{projectName}'";
            }

            // Check if git is available
            if (!_gitService.IsGitInstalledAsync().GetAwaiter().GetResult())
            {
                return "Git is not installed on this system.";
            }

            if (!_gitService.IsRepositoryAsync(projectFolder).GetAwaiter().GetResult())
            {
                return $"Project '{projectName}' does not have a git repository.";
            }

            return action switch
            {
                "branches" => ListUnmergedBranches(projectFolder, projectName),
                "status" => GetStatus(projectFolder, projectName),
                "check_merge" => CheckMerge(projectFolder, projectName, input),
                _ => $"Error: Unknown action '{action}'"
            };
        }

        private string ListUnmergedBranches(string projectFolder, string projectName)
        {
            var branches = _gitService!.GetUnmergedBranchesAsync(projectFolder).GetAwaiter().GetResult();

            if (branches.Count == 0)
            {
                return $"No unmerged branches in '{projectName}'. All merged to main.";
            }

            var canMergeCount = branches.Count(b => !b.HasConflictsWithMain);
            var conflictCount = branches.Count(b => b.HasConflictsWithMain);

            var sb = new StringBuilder();
            sb.AppendLine($"**Unmerged branches in '{projectName}':** {branches.Count} total ({canMergeCount} mergeable, {conflictCount} conflicts)\n");
            sb.AppendLine("| Branch | Feature | Ahead | Updated | Status |");
            sb.AppendLine("|--------|---------|-------|---------|--------|");

            foreach (var branch in branches.OrderByDescending(b => b.LastCommitDate))
            {
                var status = branch.HasConflictsWithMain ? "❌ Conflicts" : "✅ Ready";
                var feature = !string.IsNullOrEmpty(branch.FeatureName) ? Truncate(branch.FeatureName, 15) : "-";
                sb.AppendLine($"| {Truncate(branch.Name, 20)} | {feature} | {branch.CommitsAheadOfMain} | {branch.LastCommitDate:MM-dd HH:mm} | {status} |");
            }

            return sb.ToString();
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text[..(maxLength - 2)] + "..";
        }

        private string GetStatus(string projectFolder, string projectName)
        {
            var currentBranch = _gitService!.GetCurrentBranchAsync(projectFolder).GetAwaiter().GetResult();
            var status = _gitService.GetStatusAsync(projectFolder).GetAwaiter().GetResult();

            var sb = new StringBuilder();
            sb.AppendLine($"**{projectName}** on `{currentBranch ?? "unknown"}`");

            if (string.IsNullOrWhiteSpace(status))
            {
                sb.Append(" - Clean (no changes)");
            }
            else
            {
                sb.AppendLine("\n```");
                sb.AppendLine(status.Trim());
                sb.AppendLine("```");
            }

            return sb.ToString();
        }

        private string CheckMerge(string projectFolder, string projectName, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("branch_name", out var branchNameObj))
            {
                return "Error: branch_name is required for check_merge action";
            }

            var branchName = branchNameObj.ToString() ?? "";
            var result = _gitService!.CanMergeBranchAsync(projectFolder, branchName).GetAwaiter().GetResult();

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                return $"❌ Merge check failed: {result.ErrorMessage}";
            }

            if (result.CanMerge)
            {
                var ff = result.CanFastForward ? "fast-forward" : "merge commit";
                return $"✅ `{branchName}` → main: Ready ({result.CommitsToMerge} commits, {ff})";
            }

            var conflicts = result.PotentialConflicts.Count > 0
                ? $" Conflicts: {string.Join(", ", result.PotentialConflicts.Take(5))}" + (result.PotentialConflicts.Count > 5 ? $" +{result.PotentialConflicts.Count - 5} more" : "")
                : "";
            return $"❌ `{branchName}` → main: Has conflicts.{conflicts}";
        }
    }
}
