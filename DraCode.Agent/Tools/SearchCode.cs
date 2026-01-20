using DraCode.Agent.Helpers;
using System.Text.RegularExpressions;

namespace DraCode.Agent.Tools
{
    public class SearchCode : Tool
    {
        public override string Name => "search_code";
        public override string Description => "Search text in files within the workspace and return matching lines with file paths and line numbers.";
        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "Text or regex to search for"
                },
                directory = new
                {
                    type = "string",
                    description = "Optional subdirectory relative to workspace root to search in"
                },
                pattern = new
                {
                    type = "string",
                    description = "Optional file glob pattern (e.g., *.cs)",
                    @default = "*"
                },
                recursive = new
                {
                    type = "boolean",
                    description = "Search recursively",
                    @default = true
                },
                regex = new
                {
                    type = "boolean",
                    description = "Treat query as regular expression",
                    @default = false
                },
                case_sensitive = new
                {
                    type = "boolean",
                    description = "Case sensitive search",
                    @default = false
                }
            },
            required = new[] { "query" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var query = input["query"].ToString();
                var relDir = input.TryGetValue("directory", out var dirVal) ? dirVal?.ToString() : null;
                var pattern = input.TryGetValue("pattern", out var patVal) ? (patVal?.ToString() ?? "*") : "*";
                var recursive = !input.TryGetValue("recursive", out var recVal) || !bool.TryParse(recVal?.ToString(), out var recParsed) || recParsed;
                var useRegex = input.TryGetValue("regex", out var regexVal) && bool.TryParse(regexVal?.ToString(), out var regexParsed) && regexParsed;
                var caseSensitive = input.TryGetValue("case_sensitive", out var csVal) && bool.TryParse(csVal?.ToString(), out var csParsed) && csParsed;

                if (string.IsNullOrWhiteSpace(query))
                    throw new ArgumentException("query is required");

                var targetDir = string.IsNullOrWhiteSpace(relDir) ? workingDirectory : Path.Combine(workingDirectory, relDir!);

                if (!PathHelper.IsPathSafe(targetDir, workingDirectory))
                    return $"Error: Access denied. Directory must be in {workingDirectory}";

                if (!Directory.Exists(targetDir))
                    return $"Error: Directory not found: {relDir ?? "."}";

                var files = Directory.EnumerateFiles(targetDir, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
                var results = new List<string>();

                Regex? rx = null;
                if (useRegex)
                {
                    var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    rx = new Regex(query, options);
                }

                foreach (var file in files)
                {
                    // Skip binary-like files by extension heuristic
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".pdf" || ext == ".zip")
                        continue;

                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(file);
                    }
                    catch
                    {
                        continue; // unreadable file
                    }

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        bool match = useRegex ? rx!.IsMatch(line) : (caseSensitive ? line.Contains(query) : line.Contains(query, StringComparison.InvariantCultureIgnoreCase));
                        if (match)
                        {
                            var rel = Path.GetRelativePath(workingDirectory, file);
                            results.Add($"{rel}:{i + 1}: {line}");
                        }
                    }
                }

                return string.Join(Environment.NewLine, results);
            }
            catch (Exception ex)
            {
                return $"Error searching code: {ex.Message}";
            }
        }
    }
}
