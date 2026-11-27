using System.Diagnostics;
using System.Text;

namespace DraCode.KoboldLair.Services.Checks
{
    /// <summary>
    /// What a checker reads of a finished task's work (TASK-218, shared since TASK-221): the files it touched — its target
    /// paths, its plan's files, what git shows changed in the workspace — and git's diff of the files that existed before.
    /// Never anything outside the workspace; capped so one task cannot flood a check.
    /// </summary>
    public static class TaskMaterial
    {
        /// <summary>The most of one file it reads, and of all files together.</summary>
        public const int MaxFileChars = 12_000, MaxTotalChars = 48_000;

        /// <summary>The most of the diff it reads.</summary>
        public const int MaxDiffChars = 24_000;

        /// <summary>The files the task touched, relative to the workspace, each once.</summary>
        public static async Task<IReadOnlyList<string>> TouchedFilesAsync(TaskCheckInput input, CancellationToken cancellationToken = default) =>
            TaskAcceptanceService.TargetPaths(input.Task.Task ?? "")
                .Concat(input.PlannedFiles ?? [])
                .Concat(await ChangedFilesAsync(input.WorkspacePath, cancellationToken))
                .Select(f => f.Replace('\\', '/'))
                // A path leaving the workspace is never read; checked before "./" is dropped, which would hide a "../"
                .Where(f => !f.Split('/').Contains("..") && !Path.IsPathRooted(f))
                .Select(f => f.StartsWith("./", StringComparison.Ordinal) ? f[2..] : f)
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Appends each touched file's content under "### Files the task touched", capped.</summary>
        public static async Task AppendFilesAsync(StringBuilder text, TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            var files = await TouchedFilesAsync(input, cancellationToken);
            text.AppendLine().AppendLine("### Files the task touched");
            var total = 0;
            foreach (var file in files)
            {
                var full = Path.Combine(input.WorkspacePath, file);
                if (!File.Exists(full))
                {
                    text.AppendLine($"#### {file}").AppendLine("(does not exist)");
                    continue;
                }
                var content = await File.ReadAllTextAsync(full, cancellationToken);
                if (content.Contains('\0'))
                    continue; // a binary file says nothing a reader can judge
                if (total >= MaxTotalChars)
                {
                    text.AppendLine($"#### {file}").AppendLine("(not read: the material is at its limit)");
                    continue;
                }
                var room = Math.Min(MaxFileChars, MaxTotalChars - total);
                var shown = content.Length > room ? content[..room] + "\n… (cut here)" : content;
                total += Math.Min(content.Length, room);
                text.AppendLine($"#### {file}").AppendLine("```").AppendLine(shown).AppendLine("```");
            }
            if (files.Count == 0)
                text.AppendLine("(no file could be identified)");
        }

        /// <summary>
        /// Git's diff of the workspace's uncommitted changes to files it already tracked (the task's edits), capped; null
        /// outside git or when there are none (new files show in full among the touched files).
        /// </summary>
        public static async Task<string?> DiffAsync(string workspace, CancellationToken cancellationToken = default)
        {
            var diff = await GitAsync(workspace, "diff --no-color --no-ext-diff HEAD -- .", cancellationToken);
            if (string.IsNullOrWhiteSpace(diff))
                return null;
            return diff.Length > MaxDiffChars ? diff[..MaxDiffChars] + "\n… (cut here)" : diff;
        }

        /// <summary>What git shows changed or new under the workspace (uncommitted: the task's own work); none outside git.</summary>
        private static async Task<IReadOnlyList<string>> ChangedFilesAsync(string workspace, CancellationToken cancellationToken)
        {
            var output = await GitAsync(workspace, "status --porcelain --untracked-files=all --no-renames -- .", cancellationToken);
            if (output == null)
                return [];
            // Paths are relative to the repository root; keep those under the workspace, relative to it
            var root = (await GitAsync(workspace, "rev-parse --show-toplevel", cancellationToken))?.Trim();
            var prefix = string.IsNullOrEmpty(root) ? "" : Path.GetRelativePath(Path.GetFullPath(root), workspace).Replace('\\', '/');
            prefix = prefix == "." ? "" : prefix.TrimEnd('/') + "/";
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Length > 3 && line[0] != 'D' && line[1] != 'D')
                .Select(line => line[3..].Trim().Trim('"'))
                .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(path => path[prefix.Length..])
                .ToList();
        }

        /// <summary>A git command's output in the workspace; null when it is not a repository or the command fails.</summary>
        private static async Task<string?> GitAsync(string workspace, string arguments, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(workspace))
                return null;
            try
            {
                using var git = Process.Start(new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workspace, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
                });
                if (git == null)
                    return null;
                var output = await git.StandardOutput.ReadToEndAsync(cancellationToken);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await git.WaitForExitAsync(timeout.Token);
                return git.ExitCode == 0 ? output : null;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }
    }
}
