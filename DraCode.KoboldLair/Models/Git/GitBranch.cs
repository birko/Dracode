namespace DraCode.KoboldLair.Models.Git
{
    /// <summary>
    /// Represents a git branch with metadata
    /// </summary>
    public class GitBranch
    {
        /// <summary>
        /// Branch name (e.g., "feature/abc123-user-auth")
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Short hash of the last commit on this branch
        /// </summary>
        public string LastCommitHash { get; set; } = "";

        /// <summary>
        /// Message of the last commit
        /// </summary>
        public string LastCommitMessage { get; set; } = "";

        /// <summary>
        /// Date of the last commit
        /// </summary>
        public DateTime LastCommitDate { get; set; }

        /// <summary>
        /// Number of commits ahead of main branch
        /// </summary>
        public int CommitsAheadOfMain { get; set; }

        /// <summary>
        /// Whether this branch has merge conflicts with main
        /// </summary>
        public bool HasConflictsWithMain { get; set; }

        /// <summary>
        /// Feature ID this branch is associated with (if any)
        /// </summary>
        public string? FeatureId { get; set; }

        /// <summary>
        /// Feature name this branch is associated with (if any)
        /// </summary>
        public string? FeatureName { get; set; }
    }

    /// <summary>
    /// Result of a merge operation
    /// </summary>
    public class MergeResult
    {
        /// <summary>
        /// Whether the merge was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Whether there were conflicts that prevented the merge
        /// </summary>
        public bool HasConflicts { get; set; }

        /// <summary>
        /// List of files with conflicts (if any)
        /// </summary>
        public List<string> ConflictFiles { get; set; } = new();

        /// <summary>
        /// Error message if the merge failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The merge commit hash (if successful)
        /// </summary>
        public string? MergeCommitHash { get; set; }

        /// <summary>
        /// Creates a successful merge result
        /// </summary>
        public static MergeResult Successful(string commitHash) => new()
        {
            Success = true,
            HasConflicts = false,
            MergeCommitHash = commitHash
        };

        /// <summary>
        /// Creates a failed merge result due to conflicts
        /// </summary>
        public static MergeResult WithConflicts(List<string> conflictFiles) => new()
        {
            Success = false,
            HasConflicts = true,
            ConflictFiles = conflictFiles,
            ErrorMessage = $"Merge conflicts in {conflictFiles.Count} file(s)"
        };

        /// <summary>
        /// Creates a failed merge result due to an error
        /// </summary>
        public static MergeResult Failed(string error) => new()
        {
            Success = false,
            HasConflicts = false,
            ErrorMessage = error
        };
    }

    /// <summary>
    /// Result of a merge check (dry-run)
    /// </summary>
    public class MergeCheckResult
    {
        /// <summary>
        /// Whether the merge can be performed without conflicts
        /// </summary>
        public bool CanMerge { get; set; }

        /// <summary>
        /// Whether a fast-forward merge is possible
        /// </summary>
        public bool CanFastForward { get; set; }

        /// <summary>
        /// Files that would conflict if merged
        /// </summary>
        public List<string> PotentialConflicts { get; set; } = new();

        /// <summary>
        /// Number of commits that would be merged
        /// </summary>
        public int CommitsToMerge { get; set; }

        /// <summary>
        /// Error message if check failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
