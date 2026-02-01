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
                return $"No unmerged feature branches found in project '{projectName}'.\n\nAll features have been merged to main.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"## Unmerged Feature Branches in '{projectName}'");
            sb.AppendLine();

            foreach (var branch in branches.OrderByDescending(b => b.LastCommitDate))
            {
                var conflictStatus = branch.HasConflictsWithMain ? "Has conflicts" : "Can merge";
                var featureInfo = !string.IsNullOrEmpty(branch.FeatureName)
                    ? $" (Feature: {branch.FeatureName})"
                    : "";

                sb.AppendLine($"### {branch.Name}{featureInfo}");
                sb.AppendLine($"- Commits ahead of main: {branch.CommitsAheadOfMain}");
                sb.AppendLine($"- Last commit: {branch.LastCommitMessage}");
                sb.AppendLine($"- Last updated: {branch.LastCommitDate:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"- Merge status: {conflictStatus}");
                sb.AppendLine();
            }

            var canMergeCount = branches.Count(b => !b.HasConflictsWithMain);
            var conflictCount = branches.Count(b => b.HasConflictsWithMain);

            sb.AppendLine("---");
            sb.AppendLine($"**Summary**: {branches.Count} branches total, {canMergeCount} can merge cleanly, {conflictCount} have conflicts");

            return sb.ToString();
        }

        private string GetStatus(string projectFolder, string projectName)
        {
            var currentBranch = _gitService!.GetCurrentBranchAsync(projectFolder).GetAwaiter().GetResult();
            var status = _gitService.GetStatusAsync(projectFolder).GetAwaiter().GetResult();

            var sb = new StringBuilder();
            sb.AppendLine($"## Git Status for '{projectName}'");
            sb.AppendLine();
            sb.AppendLine($"**Current branch**: {currentBranch ?? "unknown"}");
            sb.AppendLine();

            if (string.IsNullOrWhiteSpace(status))
            {
                sb.AppendLine("Working tree is clean - no uncommitted changes.");
            }
            else
            {
                sb.AppendLine("**Uncommitted changes**:");
                sb.AppendLine("```");
                sb.AppendLine(status);
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

            var sb = new StringBuilder();
            sb.AppendLine($"## Merge Check: {branchName} → main");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                sb.AppendLine($"**Error**: {result.ErrorMessage}");
                return sb.ToString();
            }

            if (result.CanMerge)
            {
                sb.AppendLine("**Result**: Branch can be merged cleanly");
                sb.AppendLine();
                sb.AppendLine($"- Commits to merge: {result.CommitsToMerge}");
                sb.AppendLine($"- Fast-forward possible: {(result.CanFastForward ? "Yes" : "No")}");
                sb.AppendLine();
                sb.AppendLine("You can proceed with the merge using the `git_merge` tool.");
            }
            else
            {
                sb.AppendLine("**Result**: Merge would have conflicts");
                sb.AppendLine();

                if (result.PotentialConflicts.Count > 0)
                {
                    sb.AppendLine("**Conflicting files**:");
                    foreach (var file in result.PotentialConflicts)
                    {
                        sb.AppendLine($"- {file}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("The conflicts need to be resolved before merging.");
            }

            return sb.ToString();
        }
    }
}
