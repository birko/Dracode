using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for creating git commits in project repositories.
    /// Supports staging all changes or specific files, with commit message.
    /// </summary>
    public class GitCommitTool : Tool
    {
        private readonly GitService? _gitService;
        private readonly Func<string, string?>? _getProjectFolder;

        public GitCommitTool(GitService? gitService, Func<string, string?>? getProjectFolder = null)
        {
            _gitService = gitService;
            _getProjectFolder = getProjectFolder;
        }

        public override string Name => "git_commit";

        public override string Description =>
            "Create a git commit in a project repository. Actions: 'commit' stages all changes and commits with a message, " +
            "'stage' stages specific files, 'commit_staged' commits only already-staged changes.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'commit' (stage all + commit), 'stage' (stage specific files), 'commit_staged' (commit staged changes only)",
                    @enum = new[] { "commit", "stage", "commit_staged" }
                },
                project_name = new
                {
                    type = "string",
                    description = "Name of the project"
                },
                message = new
                {
                    type = "string",
                    description = "Commit message (required for 'commit' and 'commit_staged')"
                },
                files = new
                {
                    type = "string",
                    description = "Comma-separated list of file paths to stage (for 'stage' action)"
                }
            },
            required = new[] { "action", "project_name" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            if (_gitService == null)
                return "Git integration is not available.";

            if (!input.TryGetValue("action", out var actionObj) ||
                !input.TryGetValue("project_name", out var projectNameObj))
            {
                return "Error: action and project_name are required.";
            }

            var action = actionObj.ToString()?.ToLowerInvariant();
            var projectName = projectNameObj.ToString() ?? "";

            var projectFolder = _getProjectFolder?.Invoke(projectName);
            if (string.IsNullOrEmpty(projectFolder))
                return $"Error: Could not find project folder for '{projectName}'.";

            if (!await _gitService.IsGitInstalledAsync())
                return "Git is not installed on this system.";

            if (!await _gitService.IsRepositoryAsync(projectFolder))
                return $"Project '{projectName}' does not have a git repository. Use git_status with action 'init' first.";

            return action switch
            {
                "commit" => await ExecuteCommitAllAsync(projectFolder, projectName, input),
                "stage" => await ExecuteStageAsync(projectFolder, projectName, input),
                "commit_staged" => await ExecuteCommitStagedAsync(projectFolder, projectName, input),
                _ => $"Unknown action: {action}. Use 'commit', 'stage', or 'commit_staged'."
            };
        }

        private async Task<string> ExecuteCommitAllAsync(string projectFolder, string projectName, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("message", out var msgObj) || string.IsNullOrEmpty(msgObj?.ToString()))
                return "Error: 'message' is required for commit.";

            var message = msgObj.ToString()!;

            try
            {
                // Check if there's anything to commit
                var status = await _gitService!.GetStatusAsync(projectFolder);
                if (string.IsNullOrWhiteSpace(status))
                    return $"Nothing to commit in '{projectName}'. Working tree is clean.";

                // Stage all changes
                var staged = await _gitService.StageAllAsync(projectFolder);
                if (!staged)
                    return $"Failed to stage changes in '{projectName}'.";

                // Commit
                var committed = await _gitService.CommitChangesAsync(projectFolder, message);
                if (!committed)
                    return $"Failed to commit changes in '{projectName}'.";

                // Get the commit SHA
                var sha = await _gitService.GetLastCommitShaAsync(projectFolder);
                var branch = await _gitService.GetCurrentBranchAsync(projectFolder);

                SendMessage("git_committed", $"Committed to '{projectName}': {message}");
                return $"✅ Committed to `{branch}` in '{projectName}'\n**SHA:** `{sha?[..7] ?? "unknown"}`\n**Message:** {message}";
            }
            catch (Exception ex)
            {
                return $"Error committing: {ex.Message}";
            }
        }

        private async Task<string> ExecuteStageAsync(string projectFolder, string projectName, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("files", out var filesObj) || string.IsNullOrEmpty(filesObj?.ToString()))
                return "Error: 'files' is required for stage action (comma-separated paths).";

            var files = filesObj.ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (files.Length == 0)
                return "Error: No files specified to stage.";

            try
            {
                var staged = await _gitService!.StageFilesAsync(projectFolder, files);
                if (!staged)
                    return $"Failed to stage files in '{projectName}'.";

                var sb = new StringBuilder();
                sb.AppendLine($"✅ Staged {files.Length} file(s) in '{projectName}':");
                foreach (var file in files)
                    sb.AppendLine($"  + {file}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error staging files: {ex.Message}";
            }
        }

        private async Task<string> ExecuteCommitStagedAsync(string projectFolder, string projectName, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("message", out var msgObj) || string.IsNullOrEmpty(msgObj?.ToString()))
                return "Error: 'message' is required for commit.";

            var message = msgObj.ToString()!;

            try
            {
                var committed = await _gitService!.CommitChangesAsync(projectFolder, message);
                if (!committed)
                    return $"Failed to commit staged changes in '{projectName}'. Are there any staged changes?";

                var sha = await _gitService.GetLastCommitShaAsync(projectFolder);
                var branch = await _gitService.GetCurrentBranchAsync(projectFolder);

                SendMessage("git_committed", $"Committed staged changes to '{projectName}': {message}");
                return $"✅ Committed staged changes to `{branch}` in '{projectName}'\n**SHA:** `{sha?[..7] ?? "unknown"}`\n**Message:** {message}";
            }
            catch (Exception ex)
            {
                return $"Error committing: {ex.Message}";
            }
        }
    }
}
