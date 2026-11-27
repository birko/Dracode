using System.Diagnostics;
using System.Text;
using Birko.AI.Providers;
using DraCode.KoboldLair.Agents;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services.Checks
{
    /// <summary>
    /// The first checker (TASK-218, FEATURE-078 D8; the lifecycle's verify-intent): did the finished task build what its
    /// description asked — nothing missing, nothing extra, nothing built wrong — each finding quoting the requirement.
    /// It reads the task's description, the answers given for it, and the files the task touched (its target paths,
    /// its plan's files, and what git shows changed in the workspace). Each check runs a fresh agent with the project's
    /// model; with no model it cannot run, and the task goes to a person.
    /// </summary>
    public sealed class IntentChecker : ITaskChecker
    {
        /// <summary>The most of one file it reads, and of all files together.</summary>
        public const int MaxFileChars = 12_000, MaxTotalChars = 48_000;

        private readonly Func<string, ILlmProvider?> _providerFor;
        private readonly ILogger? _logger;

        /// <param name="providerFor">The model the check runs with, for a project.</param>
        public IntentChecker(Func<string, ILlmProvider?> providerFor, ILogger? logger = null)
        {
            _providerFor = providerFor;
            _logger = logger;
        }

        public string Name => "intent";

        /// <summary>One model call over the task's files: the first agent check.</summary>
        public int Cost => 10;

        public async Task<IReadOnlyList<TaskFinding>> CheckAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            var provider = _providerFor(input.ProjectId)
                ?? throw new InvalidOperationException("no model is configured for the check");
            var findings = await new IntentCheckerAgent(provider, await MaterialAsync(input, cancellationToken))
                .CheckAsync(input, cancellationToken);
            _logger?.LogInformation("Intent check on task {TaskId}: {Count} finding(s)",
                input.Task.Id[..Math.Min(8, input.Task.Id.Length)], findings.Count);
            return findings;
        }

        /// <summary>What the check reads beyond the description: the answers given, then each touched file's content.</summary>
        public static async Task<string> MaterialAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            var text = new StringBuilder();
            text.AppendLine("### Answers given for this task (part of what was asked)");
            if (input.Answers is { Count: > 0 } answers)
                foreach (var answer in answers)
                    text.AppendLine($"- {answer}");
            else
                text.AppendLine("(none)");

            var files = TaskAcceptanceService.TargetPaths(input.Task.Task ?? "")
                .Concat(input.PlannedFiles ?? [])
                .Concat(await ChangedFilesAsync(input.WorkspacePath, cancellationToken))
                .Select(f => f.Replace('\\', '/'))
                // A path leaving the workspace is never read; checked before "./" is dropped, which would hide a "../"
                .Where(f => !f.Split('/').Contains("..") && !Path.IsPathRooted(f))
                .Select(f => f.StartsWith("./", StringComparison.Ordinal) ? f[2..] : f)
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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
            return text.ToString();
        }

        /// <summary>What git shows changed or new under the workspace (uncommitted: the task's own work); none outside git.</summary>
        private static async Task<IReadOnlyList<string>> ChangedFilesAsync(string workspace, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(workspace))
                return [];
            try
            {
                using var git = Process.Start(new ProcessStartInfo("git", "status --porcelain --untracked-files=all --no-renames -- .")
                {
                    WorkingDirectory = workspace, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
                });
                if (git == null)
                    return [];
                var output = await git.StandardOutput.ReadToEndAsync(cancellationToken);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await git.WaitForExitAsync(timeout.Token);
                if (git.ExitCode != 0)
                    return [];
                // Paths are relative to the repository root; keep those under the workspace, relative to it
                var root = await RootAsync(workspace, cancellationToken);
                var prefix = root == null ? "" : Path.GetRelativePath(root, workspace).Replace('\\', '/');
                prefix = prefix == "." ? "" : prefix.TrimEnd('/') + "/";
                return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.Length > 3 && line[0] != 'D' && line[1] != 'D')
                    .Select(line => line[3..].Trim().Trim('"'))
                    .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(path => path[prefix.Length..])
                    .ToList();
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return [];
            }
        }

        private static async Task<string?> RootAsync(string workspace, CancellationToken cancellationToken)
        {
            using var git = Process.Start(new ProcessStartInfo("git", "rev-parse --show-toplevel")
            {
                WorkingDirectory = workspace, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            });
            if (git == null)
                return null;
            var root = (await git.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await git.WaitForExitAsync(cancellationToken);
            return git.ExitCode == 0 && root.Length > 0 ? Path.GetFullPath(root) : null;
        }
    }

    /// <summary>The agent behind <see cref="IntentChecker"/>: judges one finished task's files against what it asked.</summary>
    public sealed class IntentCheckerAgent(ILlmProvider provider, string material) : CheckerAgent(provider)
    {
        public override string Name => "intent";
        public override int Cost => 10;

        protected override string Role => "the intent checker: did the task build what its description asked";

        protected override string Process => @"1. List each requirement the task states: its target files, what it must implement, its acceptance criteria and exports, and the answers given for it.
2. For each requirement, find the code that carries it in the files you are given.
3. Read the files for behaviour no requirement asked for.
4. Stop: report what is missing, extra or wrong, or nothing.";

        protected override string Rules => @"1. ""missing"": a requirement with no code that carries it, or carried only in part. Quote the requirement and name the file where it belongs.
2. ""wrong"": code that carries a requirement but does not do what it says. Quote the requirement, name the file and line, and say how it diverges.
3. ""extra"": behaviour no requirement asked for. Name the file and line and what it does. Supporting code a requirement needs (imports, helpers, tests of the asked behaviour) is not extra.
4. Judge the requirement against the code, never against what the task claims or a plan says was done.
5. A requirement you cannot judge from these files (runtime behaviour, another task's file) is not a finding.";

        protected override Task<string> GatherAsync(Checks.TaskCheckInput input, CancellationToken cancellationToken) =>
            Task.FromResult(material);
    }
}
