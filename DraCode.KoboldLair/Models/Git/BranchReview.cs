namespace DraCode.KoboldLair.Models.Git
{
    /// <summary>
    /// A branch as the Git tab lists it (TASK-156): how far it is ahead of and behind main, its last commit, and whether it
    /// would merge into main cleanly — worked out without touching the working tree.
    /// </summary>
    public sealed class BranchSummary
    {
        public string Name { get; init; } = "";
        public int Ahead { get; init; }
        public int Behind { get; init; }
        public string LastCommitHash { get; init; } = "";
        public string LastCommitMessage { get; init; } = "";
        public DateTime? LastCommitDate { get; init; }
        /// <summary>True when it merges into main without conflicts, false when it does not, null when git could not tell.</summary>
        public bool? MergesCleanly { get; init; }
        /// <summary>The files a merge into main would conflict in.</summary>
        public List<string> Conflicts { get; init; } = new();
        /// <summary>From a <c>feature/{id}-{name}</c> branch name.</summary>
        public string? FeatureId { get; init; }
        public string? FeatureName { get; init; }
    }

    /// <summary>One file a branch changes against main, with its own part of the unified diff.</summary>
    public sealed class BranchFileChange
    {
        public string Path { get; init; } = "";
        /// <summary>The path before a rename or copy.</summary>
        public string? OldPath { get; init; }
        /// <summary>Added, Modified, Deleted, Renamed, Copied or Changed.</summary>
        public string Status { get; init; } = "Modified";
        public int Added { get; init; }
        public int Deleted { get; init; }
        public bool Binary { get; init; }
        /// <summary>The file's part of the diff, from its <c>diff --git</c> line; cut when over the limit.</summary>
        public string Patch { get; init; } = "";
        public bool Truncated { get; init; }
    }

    /// <summary>A branch's changes against main: <c>git diff main...branch</c>, file by file.</summary>
    public sealed class BranchDiff
    {
        public string Branch { get; init; } = "";
        public string Base { get; init; } = "main";
        public List<BranchFileChange> Files { get; init; } = new();
        /// <summary>True when the whole diff was over the limit and later files carry no patch.</summary>
        public bool Truncated { get; init; }
    }
}
