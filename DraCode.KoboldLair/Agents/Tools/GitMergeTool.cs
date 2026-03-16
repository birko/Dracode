using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for Dragon to merge feature branches to main
    /// </summary>
    public class GitMergeTool : Tool
    {
        private readonly GitService? _gitService;
        private readonly Func<string, string?>? _getProjectFolder;

        public GitMergeTool(GitService? gitService, Func<string, string?>? getProjectFolder = null)
        {
            _gitService = gitService;
            _getProjectFolder = getProjectFolder;
        }

        public override string Name => "git_merge";

        public override string Description =>
            "Merge a feature branch into main. Always checks for conflicts first and only proceeds if merge is clean.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'merge' (merge branch to main), 'delete' (delete merged branch)",
                    @enum = new[] { "merge", "delete" }
                },
                project_name = new
                {
                    type = "string",
                    description = "Name of the project"
                },
                branch_name = new
                {
                    type = "string",
                    description = "Name of the branch to merge or delete"
                }
            },
            required = new[] { "action", "project_name", "branch_name" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            if (_gitService == null)
            {
                return "Git integration is not available.";
            }

            if (!input.TryGetValue("action", out var actionObj) ||
                !input.TryGetValue("project_name", out var projectNameObj) ||
                !input.TryGetValue("branch_name", out var branchNameObj))
            {
                return "Error: action, project_name, and branch_name are required";
            }

            var action = actionObj.ToString()?.ToLower();
            var projectName = projectNameObj.ToString() ?? "";
            var branchName = branchNameObj.ToString() ?? "";

            // Get project folder
            var projectFolder = _getProjectFolder?.Invoke(projectName);
            if (string.IsNullOrEmpty(projectFolder))
            {
                return $"Error: Could not find project folder for '{projectName}'";
            }

            // Check if git is available
            if (!await _gitService.IsGitInstalledAsync())
            {
                return "Git is not installed on this system.";
            }

            if (!await _gitService.IsRepositoryAsync(projectFolder))
            {
                return $"Project '{projectName}' does not have a git repository.";
            }

            return action switch
            {
                "merge" => await MergeBranchAsync(projectFolder, projectName, branchName),
                "delete" => await DeleteBranchAsync(projectFolder, projectName, branchName),
                _ => $"Error: Unknown action '{action}'"
            };
        }

        private async Task<string> MergeBranchAsync(string projectFolder, string projectName, string branchName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Merging {branchName} → main");
            sb.AppendLine();

            // First check if merge is possible
            var canMerge = await _gitService!.CanMergeBranchAsync(projectFolder, branchName);

            if (!canMerge.CanMerge)
            {
                sb.AppendLine("**Merge aborted**: Conflicts detected");
                sb.AppendLine();

                if (canMerge.PotentialConflicts.Count > 0)
                {
                    sb.AppendLine("**Conflicting files**:");
                    foreach (var file in canMerge.PotentialConflicts)
                    {
                        sb.AppendLine($"- {file}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("Please resolve conflicts before merging.");
                return sb.ToString();
            }

            // Proceed with merge
            var result = await _gitService.MergeBranchAsync(projectFolder, branchName);

            if (result.Success)
            {
                sb.AppendLine("**Merge successful!**");
                sb.AppendLine();
                sb.AppendLine($"- Merge commit: {result.MergeCommitHash?[..8]}");
                sb.AppendLine($"- Commits merged: {canMerge.CommitsToMerge}");
                sb.AppendLine();
                sb.AppendLine($"Branch '{branchName}' has been merged into main.");
                sb.AppendLine();
                sb.AppendLine("You can now delete the feature branch using the 'delete' action if it's no longer needed.");

                SendMessage("success", $"Merged {branchName} to main in project {projectName}");
            }
            else
            {
                sb.AppendLine("**Merge failed**");
                sb.AppendLine();

                if (result.HasConflicts)
                {
                    sb.AppendLine("Conflicts were detected during merge:");
                    foreach (var file in result.ConflictFiles)
                    {
                        sb.AppendLine($"- {file}");
                    }
                }
                else if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    sb.AppendLine($"Error: {result.ErrorMessage}");
                }
            }

            return sb.ToString();
        }

        private async Task<string> DeleteBranchAsync(string projectFolder, string projectName, string branchName)
        {
            var sb = new StringBuilder();

            // Don't allow deleting main
            if (branchName == "main" || branchName == "master")
            {
                return $"Error: Cannot delete the main branch.";
            }

            // Check current branch
            var currentBranch = await _gitService!.GetCurrentBranchAsync(projectFolder);
            if (currentBranch == branchName)
            {
                // Switch to main first
                await _gitService.CheckoutBranchAsync(projectFolder, "main");
            }

            var deleted = await _gitService.DeleteBranchAsync(projectFolder, branchName);

            if (deleted)
            {
                sb.AppendLine($"Branch '{branchName}' has been deleted from project '{projectName}'.");
                SendMessage("success", $"Deleted branch {branchName} in project {projectName}");
            }
            else
            {
                sb.AppendLine($"**Failed to delete branch '{branchName}'**");
                sb.AppendLine();
                sb.AppendLine("The branch may not exist or may not be fully merged.");
                sb.AppendLine("If you're sure you want to delete it, you may need to force delete.");
            }

            return sb.ToString();
        }
    }
}
