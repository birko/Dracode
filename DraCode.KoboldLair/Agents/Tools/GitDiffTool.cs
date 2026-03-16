using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing git diffs between branches and commit logs before merging.
    /// </summary>
    public class GitDiffTool : Tool
    {
        private readonly GitService? _gitService;
        private readonly Func<string, string?>? _getProjectFolder;

        public GitDiffTool(GitService? gitService, Func<string, string?>? getProjectFolder = null)
        {
            _gitService = gitService;
            _getProjectFolder = getProjectFolder;
        }

        public override string Name => "git_diff";

        public override string Description =>
            "View git diffs and commit logs between branches. Use 'diff' to see what changed in a feature branch compared to main, " +
            "or 'log' to see the commit history. Useful before merging to review changes.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'diff' (show file changes), 'log' (show commits), 'summary' (quick stat overview)",
                    @enum = new[] { "diff", "log", "summary" }
                },
                project_name = new
                {
                    type = "string",
                    description = "Name of the project"
                },
                branch_name = new
                {
                    type = "string",
                    description = "Source branch to compare (e.g., 'feature/auth')"
                },
                target_branch = new
                {
                    type = "string",
                    description = "Target branch to compare against (default: 'main')"
                }
            },
            required = new[] { "action", "project_name", "branch_name" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            if (_gitService == null)
                return "Git integration is not available.";

            if (!input.TryGetValue("action", out var actionObj) ||
                !input.TryGetValue("project_name", out var projectNameObj) ||
                !input.TryGetValue("branch_name", out var branchObj))
            {
                return "Error: action, project_name, and branch_name are required.";
            }

            var action = actionObj.ToString()?.ToLowerInvariant();
            var projectName = projectNameObj.ToString() ?? "";
            var branchName = branchObj.ToString() ?? "";
            var targetBranch = input.TryGetValue("target_branch", out var targetObj)
                ? targetObj.ToString() ?? "main"
                : "main";

            var projectFolder = _getProjectFolder?.Invoke(projectName);
            if (string.IsNullOrEmpty(projectFolder))
                return $"Error: Could not find project folder for '{projectName}'.";

            if (!await _gitService.IsGitInstalledAsync())
                return "Git is not installed on this system.";

            if (!await _gitService.IsRepositoryAsync(projectFolder))
                return $"Project '{projectName}' does not have a git repository.";

            return action switch
            {
                "diff" => await ExecuteDiffAsync(projectFolder, branchName, targetBranch),
                "log" => await ExecuteLogAsync(projectFolder, branchName, targetBranch),
                "summary" => await ExecuteSummaryAsync(projectFolder, branchName, targetBranch),
                _ => $"Unknown action: {action}. Use 'diff', 'log', or 'summary'."
            };
        }

        private async Task<string> ExecuteDiffAsync(string projectFolder, string branchName, string targetBranch)
        {
            try
            {
                var (diff, stat, filesChanged) = await _gitService!.GetBranchDiffAsync(projectFolder, branchName, targetBranch);

                if (string.IsNullOrWhiteSpace(diff) && filesChanged == 0)
                    return $"No differences between `{branchName}` and `{targetBranch}`.";

                var sb = new StringBuilder();
                sb.AppendLine($"**Diff: `{branchName}` vs `{targetBranch}`** ({filesChanged} files changed)\n");

                // Show stat summary
                if (!string.IsNullOrWhiteSpace(stat))
                {
                    sb.AppendLine("### Summary");
                    sb.AppendLine("```");
                    sb.AppendLine(stat.Trim());
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                // Show diff (truncated if too large)
                if (!string.IsNullOrWhiteSpace(diff))
                {
                    sb.AppendLine("### Changes");
                    if (diff.Length > 4000)
                    {
                        sb.AppendLine("```diff");
                        sb.AppendLine(diff[..4000]);
                        sb.AppendLine("```");
                        sb.AppendLine($"\n*... diff truncated ({diff.Length} chars total). Use 'summary' for a compact overview.*");
                    }
                    else
                    {
                        sb.AppendLine("```diff");
                        sb.AppendLine(diff.Trim());
                        sb.AppendLine("```");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting diff: {ex.Message}";
            }
        }

        private async Task<string> ExecuteLogAsync(string projectFolder, string branchName, string targetBranch)
        {
            try
            {
                var commits = await _gitService!.GetBranchLogAsync(projectFolder, branchName, targetBranch);

                if (commits.Count == 0)
                    return $"No commits in `{branchName}` that are not in `{targetBranch}`.";

                var sb = new StringBuilder();
                sb.AppendLine($"**Commits: `{branchName}` vs `{targetBranch}`** ({commits.Count} commits)\n");
                sb.AppendLine("| SHA | Author | Date | Message |");
                sb.AppendLine("|-----|--------|------|---------|");

                foreach (var commit in commits.Take(30))
                {
                    var sha = commit.Sha.Length > 7 ? commit.Sha[..7] : commit.Sha;
                    var msg = commit.Message.Length > 50 ? commit.Message[..47] + "..." : commit.Message;
                    sb.AppendLine($"| `{sha}` | {commit.Author} | {commit.Date:MM-dd HH:mm} | {msg} |");
                }

                if (commits.Count > 30)
                    sb.AppendLine($"\n*... and {commits.Count - 30} more commits*");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting log: {ex.Message}";
            }
        }

        private async Task<string> ExecuteSummaryAsync(string projectFolder, string branchName, string targetBranch)
        {
            try
            {
                var (_, stat, filesChanged) = await _gitService!.GetBranchDiffAsync(projectFolder, branchName, targetBranch);
                var commits = await _gitService.GetBranchLogAsync(projectFolder, branchName, targetBranch);
                var mergeCheck = await _gitService.CanMergeBranchAsync(projectFolder, branchName, targetBranch);

                var sb = new StringBuilder();
                sb.AppendLine($"## Branch Summary: `{branchName}` → `{targetBranch}`\n");
                sb.AppendLine($"- **Commits:** {commits.Count}");
                sb.AppendLine($"- **Files changed:** {filesChanged}");
                sb.AppendLine($"- **Can merge:** {(mergeCheck.CanMerge ? "✅ Yes" : "❌ No")}");

                if (mergeCheck.CanMerge)
                    sb.AppendLine($"- **Merge type:** {(mergeCheck.CanFastForward ? "Fast-forward" : "Merge commit")}");

                if (mergeCheck.PotentialConflicts.Count > 0)
                {
                    sb.AppendLine($"- **Conflicts:** {string.Join(", ", mergeCheck.PotentialConflicts.Take(5))}");
                    if (mergeCheck.PotentialConflicts.Count > 5)
                        sb.AppendLine($"  ... and {mergeCheck.PotentialConflicts.Count - 5} more");
                }

                if (!string.IsNullOrWhiteSpace(stat))
                {
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine(stat.Trim());
                    sb.AppendLine("```");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting branch summary: {ex.Message}";
            }
        }
    }
}
