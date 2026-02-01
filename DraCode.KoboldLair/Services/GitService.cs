using System.Diagnostics;
using System.Text.RegularExpressions;
using DraCode.KoboldLair.Models.Git;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Service for git operations on project repositories
    /// </summary>
    public class GitService
    {
        private readonly ILogger<GitService> _logger;
        private bool? _gitInstalled;

        public GitService(ILogger<GitService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Checks if git is installed and available on the system
        /// </summary>
        public async Task<bool> IsGitInstalledAsync()
        {
            if (_gitInstalled.HasValue)
                return _gitInstalled.Value;

            try
            {
                var result = await RunGitCommandAsync(".", "--version");
                _gitInstalled = result.Success;
                if (_gitInstalled.Value)
                {
                    _logger.LogInformation("Git detected: {Version}", result.Output.Trim());
                }
                return _gitInstalled.Value;
            }
            catch
            {
                _gitInstalled = false;
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory is a git repository
        /// </summary>
        public async Task<bool> IsRepositoryAsync(string projectFolder)
        {
            var gitDir = Path.Combine(projectFolder, ".git");
            if (Directory.Exists(gitDir))
                return true;

            var result = await RunGitCommandAsync(projectFolder, "rev-parse", "--git-dir");
            return result.Success;
        }

        /// <summary>
        /// Initializes a new git repository in the project folder
        /// </summary>
        public async Task<bool> InitRepositoryAsync(string projectFolder)
        {
            if (!await IsGitInstalledAsync())
            {
                _logger.LogWarning("Cannot init repository - git is not installed");
                return false;
            }

            if (await IsRepositoryAsync(projectFolder))
            {
                _logger.LogDebug("Repository already exists at {Path}", projectFolder);
                return true;
            }

            var result = await RunGitCommandAsync(projectFolder, "init", "-b", "main");
            if (result.Success)
            {
                _logger.LogInformation("Initialized git repository at {Path}", projectFolder);

                // Configure user for this repo if not set globally
                await RunGitCommandAsync(projectFolder, "config", "user.email", "koboldlair@local");
                await RunGitCommandAsync(projectFolder, "config", "user.name", "KoboldLair");
            }
            else
            {
                _logger.LogError("Failed to init repository: {Error}", result.Error);
            }

            return result.Success;
        }

        /// <summary>
        /// Creates a new branch from the current HEAD
        /// </summary>
        public async Task<bool> CreateBranchAsync(string projectFolder, string branchName)
        {
            if (!await IsRepositoryAsync(projectFolder))
            {
                _logger.LogWarning("Cannot create branch - not a git repository: {Path}", projectFolder);
                return false;
            }

            // Check if branch already exists
            var checkResult = await RunGitCommandAsync(projectFolder, "branch", "--list", branchName);
            if (!string.IsNullOrWhiteSpace(checkResult.Output))
            {
                _logger.LogDebug("Branch {Branch} already exists", branchName);
                return true;
            }

            var result = await RunGitCommandAsync(projectFolder, "branch", branchName);
            if (result.Success)
            {
                _logger.LogInformation("Created branch {Branch} in {Path}", branchName, projectFolder);
            }
            else
            {
                _logger.LogError("Failed to create branch {Branch}: {Error}", branchName, result.Error);
            }

            return result.Success;
        }

        /// <summary>
        /// Checks out a branch
        /// </summary>
        public async Task<bool> CheckoutBranchAsync(string projectFolder, string branchName)
        {
            if (!await IsRepositoryAsync(projectFolder))
                return false;

            var result = await RunGitCommandAsync(projectFolder, "checkout", branchName);
            if (result.Success)
            {
                _logger.LogDebug("Checked out branch {Branch}", branchName);
            }
            else
            {
                _logger.LogError("Failed to checkout branch {Branch}: {Error}", branchName, result.Error);
            }

            return result.Success;
        }

        /// <summary>
        /// Gets the current branch name
        /// </summary>
        public async Task<string?> GetCurrentBranchAsync(string projectFolder)
        {
            var result = await RunGitCommandAsync(projectFolder, "branch", "--show-current");
            return result.Success ? result.Output.Trim() : null;
        }

        /// <summary>
        /// Stages all changes in the repository
        /// </summary>
        public async Task<bool> StageAllAsync(string projectFolder)
        {
            var result = await RunGitCommandAsync(projectFolder, "add", "-A");
            return result.Success;
        }

        /// <summary>
        /// Stages specific files
        /// </summary>
        public async Task<bool> StageFilesAsync(string projectFolder, params string[] files)
        {
            var args = new List<string> { "add" };
            args.AddRange(files);
            var result = await RunGitCommandAsync(projectFolder, args.ToArray());
            return result.Success;
        }

        /// <summary>
        /// Creates a commit with the given message
        /// </summary>
        /// <param name="projectFolder">Repository path</param>
        /// <param name="message">Commit message</param>
        /// <param name="authorName">Optional author name (defaults to KoboldLair)</param>
        public async Task<bool> CommitChangesAsync(string projectFolder, string message, string? authorName = null)
        {
            // Check if there are changes to commit
            var statusResult = await RunGitCommandAsync(projectFolder, "status", "--porcelain");
            if (string.IsNullOrWhiteSpace(statusResult.Output))
            {
                _logger.LogDebug("No changes to commit in {Path}", projectFolder);
                return true; // No changes is not an error
            }

            var author = authorName ?? "KoboldLair";
            var result = await RunGitCommandAsync(projectFolder, "commit", "-m", message, $"--author={author} <{author.ToLower().Replace(" ", "-")}@koboldlair.local>");

            if (result.Success)
            {
                _logger.LogInformation("Created commit in {Path}: {Message}", projectFolder, message.Split('\n')[0]);
            }
            else
            {
                _logger.LogError("Failed to commit: {Error}", result.Error);
            }

            return result.Success;
        }

        /// <summary>
        /// Gets all branches that are not merged into main
        /// </summary>
        public async Task<List<GitBranch>> GetUnmergedBranchesAsync(string projectFolder)
        {
            var branches = new List<GitBranch>();

            if (!await IsRepositoryAsync(projectFolder))
                return branches;

            // Get unmerged branches
            var result = await RunGitCommandAsync(projectFolder, "branch", "--no-merged", "main");
            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
                return branches;

            var branchNames = result.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim().TrimStart('*').Trim())
                .Where(b => !string.IsNullOrEmpty(b) && b != "main");

            foreach (var branchName in branchNames)
            {
                var branch = await GetBranchInfoAsync(projectFolder, branchName);
                if (branch != null)
                {
                    branches.Add(branch);
                }
            }

            return branches;
        }

        /// <summary>
        /// Gets detailed information about a branch
        /// </summary>
        public async Task<GitBranch?> GetBranchInfoAsync(string projectFolder, string branchName)
        {
            var branch = new GitBranch { Name = branchName };

            // Get last commit info
            var logResult = await RunGitCommandAsync(projectFolder, "log", "-1", "--format=%h|%s|%ci", branchName);
            if (logResult.Success && !string.IsNullOrWhiteSpace(logResult.Output))
            {
                var parts = logResult.Output.Trim().Split('|');
                if (parts.Length >= 3)
                {
                    branch.LastCommitHash = parts[0];
                    branch.LastCommitMessage = parts[1];
                    if (DateTime.TryParse(parts[2], out var date))
                    {
                        branch.LastCommitDate = date;
                    }
                }
            }

            // Get commits ahead of main
            var aheadResult = await RunGitCommandAsync(projectFolder, "rev-list", "--count", $"main..{branchName}");
            if (aheadResult.Success && int.TryParse(aheadResult.Output.Trim(), out var ahead))
            {
                branch.CommitsAheadOfMain = ahead;
            }

            // Check for conflicts with main (dry-run merge)
            var mergeCheck = await CanMergeBranchAsync(projectFolder, branchName);
            branch.HasConflictsWithMain = !mergeCheck.CanMerge;

            // Extract feature info from branch name (feature/{id}-{name})
            var match = Regex.Match(branchName, @"^feature/([^-]+)-(.+)$");
            if (match.Success)
            {
                branch.FeatureId = match.Groups[1].Value;
                branch.FeatureName = match.Groups[2].Value.Replace("-", " ");
            }

            return branch;
        }

        /// <summary>
        /// Checks if a branch can be merged into the target branch without conflicts
        /// </summary>
        public async Task<MergeCheckResult> CanMergeBranchAsync(string projectFolder, string sourceBranch, string targetBranch = "main")
        {
            var result = new MergeCheckResult();

            if (!await IsRepositoryAsync(projectFolder))
            {
                result.ErrorMessage = "Not a git repository";
                return result;
            }

            // Save current branch
            var currentBranch = await GetCurrentBranchAsync(projectFolder);

            try
            {
                // Checkout target branch
                await CheckoutBranchAsync(projectFolder, targetBranch);

                // Try merge with --no-commit --no-ff to test
                var mergeResult = await RunGitCommandAsync(projectFolder, "merge", "--no-commit", "--no-ff", sourceBranch);

                if (mergeResult.Success)
                {
                    result.CanMerge = true;

                    // Check if it could be fast-forward
                    var ffResult = await RunGitCommandAsync(projectFolder, "merge-base", "--is-ancestor", targetBranch, sourceBranch);
                    result.CanFastForward = ffResult.Success;

                    // Get commit count
                    var countResult = await RunGitCommandAsync(projectFolder, "rev-list", "--count", $"{targetBranch}..{sourceBranch}");
                    if (int.TryParse(countResult.Output.Trim(), out var count))
                    {
                        result.CommitsToMerge = count;
                    }
                }
                else
                {
                    result.CanMerge = false;

                    // Get conflict files
                    var conflictResult = await RunGitCommandAsync(projectFolder, "diff", "--name-only", "--diff-filter=U");
                    if (!string.IsNullOrWhiteSpace(conflictResult.Output))
                    {
                        result.PotentialConflicts = conflictResult.Output
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .ToList();
                    }
                }

                // Abort the test merge
                await RunGitCommandAsync(projectFolder, "merge", "--abort");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                // Try to abort any in-progress merge
                await RunGitCommandAsync(projectFolder, "merge", "--abort");
            }
            finally
            {
                // Restore original branch
                if (!string.IsNullOrEmpty(currentBranch))
                {
                    await CheckoutBranchAsync(projectFolder, currentBranch);
                }
            }

            return result;
        }

        /// <summary>
        /// Merges a source branch into the target branch using merge commit strategy
        /// </summary>
        public async Task<MergeResult> MergeBranchAsync(string projectFolder, string sourceBranch, string targetBranch = "main")
        {
            if (!await IsRepositoryAsync(projectFolder))
            {
                return MergeResult.Failed("Not a git repository");
            }

            // First check if merge is possible
            var canMerge = await CanMergeBranchAsync(projectFolder, sourceBranch, targetBranch);
            if (!canMerge.CanMerge)
            {
                return MergeResult.WithConflicts(canMerge.PotentialConflicts);
            }

            // Save current branch
            var currentBranch = await GetCurrentBranchAsync(projectFolder);

            try
            {
                // Checkout target branch
                if (!await CheckoutBranchAsync(projectFolder, targetBranch))
                {
                    return MergeResult.Failed($"Failed to checkout {targetBranch}");
                }

                // Perform merge with merge commit (--no-ff ensures merge commit even if fast-forward possible)
                var mergeResult = await RunGitCommandAsync(projectFolder, "merge", "--no-ff", "-m", $"Merge branch '{sourceBranch}' into {targetBranch}", sourceBranch);

                if (mergeResult.Success)
                {
                    // Get the merge commit hash
                    var hashResult = await RunGitCommandAsync(projectFolder, "rev-parse", "HEAD");
                    var hash = hashResult.Output.Trim();

                    _logger.LogInformation("Merged {Source} into {Target}: {Hash}", sourceBranch, targetBranch, hash[..8]);
                    return MergeResult.Successful(hash);
                }
                else
                {
                    // Abort failed merge
                    await RunGitCommandAsync(projectFolder, "merge", "--abort");
                    return MergeResult.Failed(mergeResult.Error);
                }
            }
            finally
            {
                // Restore original branch if different from target
                if (!string.IsNullOrEmpty(currentBranch) && currentBranch != targetBranch)
                {
                    await CheckoutBranchAsync(projectFolder, currentBranch);
                }
            }
        }

        /// <summary>
        /// Aborts a merge in progress
        /// </summary>
        public async Task<bool> AbortMergeAsync(string projectFolder)
        {
            var result = await RunGitCommandAsync(projectFolder, "merge", "--abort");
            return result.Success;
        }

        /// <summary>
        /// Deletes a branch (after successful merge)
        /// </summary>
        public async Task<bool> DeleteBranchAsync(string projectFolder, string branchName, bool force = false)
        {
            var flag = force ? "-D" : "-d";
            var result = await RunGitCommandAsync(projectFolder, "branch", flag, branchName);
            if (result.Success)
            {
                _logger.LogInformation("Deleted branch {Branch}", branchName);
            }
            return result.Success;
        }

        /// <summary>
        /// Sanitizes a feature name for use as a branch name
        /// </summary>
        public string SanitizeBranchName(string name)
        {
            // Convert to lowercase, replace spaces and special chars with hyphens
            var sanitized = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-");
            // Remove leading/trailing hyphens
            sanitized = sanitized.Trim('-');
            // Limit length
            if (sanitized.Length > 50)
            {
                sanitized = sanitized[..50].TrimEnd('-');
            }
            return sanitized;
        }

        /// <summary>
        /// Creates a feature branch name from feature ID and name
        /// Format: feature/{id}-{sanitized-name}
        /// </summary>
        public string CreateFeatureBranchName(string featureId, string featureName)
        {
            // Use first 8 chars of ID for brevity
            var shortId = featureId.Length > 8 ? featureId[..8] : featureId;
            var sanitizedName = SanitizeBranchName(featureName);
            return $"feature/{shortId}-{sanitizedName}";
        }

        /// <summary>
        /// Gets the git status (staged, unstaged, untracked files)
        /// </summary>
        public async Task<string> GetStatusAsync(string projectFolder)
        {
            var result = await RunGitCommandAsync(projectFolder, "status", "--short");
            return result.Success ? result.Output : result.Error;
        }

        /// <summary>
        /// Runs a git command and returns the result
        /// </summary>
        private async Task<(bool Success, string Output, string Error)> RunGitCommandAsync(string workingDirectory, params string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var arg in args)
                {
                    psi.ArgumentList.Add(arg);
                }

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, "", "Failed to start git process");
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }
    }
}
