using System.Text;
using System.Text.RegularExpressions;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Services
{
    /// <summary>What the acceptance check found (TASK-025).</summary>
    /// <param name="Unmet">Promises the work demonstrably broke, each with its evidence: the task is not done.</param>
    /// <param name="NeedsHuman">What no machine could settle; a person approves or rejects (FEATURE-031 D4).</param>
    /// <param name="Checked">What was checked and held.</param>
    public sealed record AcceptanceResult(IReadOnlyList<string> Unmet, IReadOnlyList<string> NeedsHuman, IReadOnlyList<string> Checked)
    {
        public bool Passed => Unmet.Count == 0 && NeedsHuman.Count == 0;
    }

    /// <summary>
    /// Holds a finished task to what its own description promises (TASK-025, FEATURE-031 D2), in the shape Wyvern writes
    /// it: target file paths, a "Must implement:" / "Acceptance criteria:" list, and an "Exports:" clause. A missing or
    /// empty target file fails. A declared export fails only when its target file exists, the clause parsed, and the name
    /// is nowhere in the workspace. A name found only in another file, a clause that will not parse, and every prose
    /// criterion go to a person. Stateless; reads the workspace only.
    /// </summary>
    public sealed partial class TaskAcceptanceService
    {
        private const string CodeExtensions =
            "ts|tsx|js|jsx|mjs|cjs|cs|py|php|java|kt|go|rs|cpp|cc|c|h|hpp|css|scss|less|html|vue|svelte|rb|swift|sql|sh|json|yml|yaml|md";

        [GeneratedRegex($@"(?<![\w/.\-])((?:\.?\.?/)?(?:[\w.\-]+/)*[\w\-]+(?:\.[\w\-]+)*\.(?:{CodeExtensions}))(?![\w])", RegexOptions.IgnoreCase)]
        private static partial Regex PathToken();

        // "Node.js" and its kind name a framework, not a file
        [GeneratedRegex(@"^(Node|Vue|Next|Nuxt|Express|Three|Chart|Moment|Socket|Angular|React|Ember|Backbone|Alpine|Solid|Svelte|Deno|Bun|Nest)\.js$", RegexOptions.IgnoreCase)]
        private static partial Regex FrameworkName();

        [GeneratedRegex(@"\b(Must implement|Acceptance criteria|Criteria|Constraints|Exports|Public API|Depends on|Notes?)\s*:", RegexOptions.IgnoreCase)]
        private static partial Regex ClauseLabel();

        [GeneratedRegex(@"^\s*([A-Za-z_$][\w$]*)\s*(.*)$", RegexOptions.Singleline)]
        private static partial Regex LeadingName();

        [GeneratedRegex(@"^(class|interface|enum|type|struct|record|function|module|component|hook|const)\b", RegexOptions.IgnoreCase)]
        private static partial Regex KindWord();

        [GeneratedRegex(@"\bwith\s+([A-Za-z_$][\w$]*)", RegexOptions.IgnoreCase)]
        private static partial Regex WithMember();

        /// <summary>
        /// How a reopen's note starts when appended to the task (Drake: "Acceptance check failed (attempt n): …"). The
        /// check reads the description only up to the first one: a note quotes paths and names, and read as part of the
        /// Exports clause it would fail the fixed task again.
        /// </summary>
        public const string ReopenNotePrefix = "Acceptance check failed (attempt";

        public Task<AcceptanceResult> EvaluateAsync(TaskRecord task, string workspacePath, CancellationToken cancellationToken = default)
        {
            var unmet = new List<string>();
            var needsHuman = new List<string>();
            var checkedOk = new List<string>();
            var description = task.Task ?? "";
            var firstNote = description.IndexOf(ReopenNotePrefix, StringComparison.Ordinal);
            if (firstNote >= 0)
                description = description[..firstNote];
            var clauses = Clauses(description);

            // Target files: every path the description names outside its Exports clause
            var exportsClause = clauses.FirstOrDefault(c => IsExports(c.Label));
            var pathText = exportsClause == null ? description : description.Remove(exportsClause.Start, exportsClause.Length);
            var targets = PathToken().Matches(pathText)
                .Select(m => Normalize(m.Groups[1].Value))
                .Where(p => !FrameworkName().IsMatch(p))
                // A path leaving the workspace is not the task's output to judge, and is never read
                .Where(p => !p.Split('/').Contains(".."))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var presentTargets = new List<string>();
            foreach (var target in targets)
            {
                var full = Path.Combine(workspacePath, target);
                if (!File.Exists(full))
                    unmet.Add($"Target file {target} is missing.");
                else if (new FileInfo(full).Length == 0)
                    unmet.Add($"Target file {target} is empty.");
                else
                {
                    presentTargets.Add(target);
                    checkedOk.Add($"Target file {target} exists.");
                }
            }

            if (exportsClause != null)
                CheckExports(exportsClause.Text, targets, presentTargets, task, workspacePath, unmet, needsHuman, checkedOk);

            // Prose: what the task must do is a person's to judge (FEATURE-031 D4; an LLM judge is D6, deferred)
            foreach (var clause in clauses.Where(c => IsProse(c.Label)))
                needsHuman.AddRange(SplitItems(clause.Text));

            return Task.FromResult(new AcceptanceResult(unmet, needsHuman, checkedOk));
        }

        private void CheckExports(string clause, List<string> targets, List<string> presentTargets, TaskRecord task,
            string workspacePath, List<string> unmet, List<string> needsHuman, List<string> checkedOk)
        {
            var names = new List<string>();
            foreach (var item in SplitItems(clause))
            {
                var match = LeadingName().Match(item);
                if (!match.Success)
                {
                    needsHuman.Add($"Exports clause not understood: \"{clause}\"");
                    return;
                }
                names.Add(match.Groups[1].Value);
                var rest = match.Groups[2].Value;
                if (KindWord().IsMatch(rest) && WithMember().Match(rest) is { Success: true } member)
                    names.Add(member.Groups[1].Value);
            }

            // A target that is missing has already failed; its exports are not judged separately
            if (targets.Count > 0 && presentTargets.Count < targets.Count)
                return;

            var searchIn = presentTargets.Count > 0
                ? presentTargets
                : task.OutputFiles.Where(f => File.Exists(Path.Combine(workspacePath, f))).ToList();
            if (searchIn.Count == 0)
            {
                needsHuman.Add($"No file to check the exports in: \"{clause}\"");
                return;
            }

            var contents = searchIn.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(workspacePath, f)));
            foreach (var name in names.Distinct())
            {
                if (contents.Any(c => Declares(c.Value, name)))
                {
                    checkedOk.Add($"Export {name} found in {string.Join(", ", searchIn)}.");
                    continue;
                }

                var elsewhere = FindElsewhere(workspacePath, searchIn, name);
                if (elsewhere != null)
                    needsHuman.Add($"Export {name} is not in {string.Join(", ", searchIn)} but is declared in {elsewhere}: is that where it belongs?");
                else
                    unmet.Add($"Export {name} is not declared in {string.Join(", ", searchIn)}.");
            }
        }

        private static bool Declares(string content, string name)
        {
            var n = Regex.Escape(name);
            return Regex.IsMatch(content, $@"\b(class|interface|function|def|enum|type|struct|record|const|let|var|val|fun|func|fn)\s+{n}\b")
                || Regex.IsMatch(content, $@"(?<![\w$.]){n}\s*(\(|<|=|:)");
        }

        private static string? FindElsewhere(string workspacePath, List<string> searched, string name)
        {
            var skip = new HashSet<string>(searched.Select(s => Path.GetFullPath(Path.Combine(workspacePath, s))), StringComparer.OrdinalIgnoreCase);
            var extensions = new HashSet<string>(CodeExtensions.Split('|').Select(e => "." + e), StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(workspacePath, "*", SearchOption.AllDirectories))
            {
                if (skip.Contains(Path.GetFullPath(file)) || !extensions.Contains(Path.GetExtension(file))
                    || file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
                    continue;
                if (Declares(File.ReadAllText(file), name))
                    return Path.GetRelativePath(workspacePath, file).Replace('\\', '/');
            }
            return null;
        }

        private sealed record Clause(string Label, string Text, int Start, int Length);

        /// <summary>Each labelled clause, running to the next label or the end.</summary>
        private static List<Clause> Clauses(string description)
        {
            var labels = ClauseLabel().Matches(description);
            var clauses = new List<Clause>();
            for (var i = 0; i < labels.Count; i++)
            {
                var start = labels[i].Index;
                var end = i + 1 < labels.Count ? labels[i + 1].Index : description.Length;
                var body = description[(labels[i].Index + labels[i].Length)..end].Trim();
                clauses.Add(new Clause(labels[i].Groups[1].Value, body.TrimEnd('.').Trim(), start, end - start));
            }
            return clauses;
        }

        private static bool IsExports(string label) =>
            label.Equals("Exports", StringComparison.OrdinalIgnoreCase) || label.Equals("Public API", StringComparison.OrdinalIgnoreCase);

        private static bool IsProse(string label) =>
            label.Equals("Must implement", StringComparison.OrdinalIgnoreCase)
            || label.Equals("Acceptance criteria", StringComparison.OrdinalIgnoreCase)
            || label.Equals("Criteria", StringComparison.OrdinalIgnoreCase);

        /// <summary>Splits on commas, semicolons and line breaks outside brackets.</summary>
        private static List<string> SplitItems(string text)
        {
            var items = new List<string>();
            var depth = 0;
            var current = new StringBuilder();
            foreach (var ch in text)
            {
                if (ch is '(' or '<' or '[' or '{') depth++;
                else if (ch is ')' or '>' or ']' or '}') depth = Math.Max(0, depth - 1);

                if (depth == 0 && ch is ',' or ';' or '\n')
                {
                    Flush();
                    continue;
                }
                current.Append(ch);
            }
            Flush();
            return items;

            void Flush()
            {
                var item = current.ToString().Trim().TrimStart('-', '*', ' ').TrimEnd('.').Trim();
                if (item.Length > 0)
                    items.Add(item);
                current.Clear();
            }
        }

        private static string Normalize(string path)
        {
            path = path.Replace('\\', '/');
            while (path.StartsWith("./", StringComparison.Ordinal))
                path = path[2..];
            return path.TrimStart('/');
        }
    }
}
