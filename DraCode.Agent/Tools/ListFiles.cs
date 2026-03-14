using DraCode.Agent.Helpers;

namespace DraCode.Agent.Tools
{
    public class ListFiles : Tool
    {
        public override string Name => "list_files";
        public override string Description => "List files in the workspace or a subdirectory. Shows both original filename and lowercase reference for case-insensitive matching.";
        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                directory = new
                {
                    type = "string",
                    description = "Optional path relative to workspace root to list files from"
                },
                recursive = new
                {
                    type = "boolean",
                    description = "List files recursively",
                    @default = false
                }
            }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var relDir = input != null && input.TryGetValue("directory", out var dirVal) ? dirVal?.ToString()?.Trim() : null;
                var recursive = input != null && input.TryGetValue("recursive", out var recVal) && bool.TryParse(recVal?.ToString(), out var recParsed) && recParsed;

                // Normalize the working directory to an absolute path first
                var normalizedWorkingDir = Path.GetFullPath(workingDirectory);

                string targetDir;
                if (string.IsNullOrWhiteSpace(relDir) || relDir == "." || relDir == "./")
                {
                    targetDir = normalizedWorkingDir;
                }
                else
                {
                    // Handle LLMs that use Unix-style absolute paths to reference workspace
                    // e.g., "/workspace" should be treated as relative to workspace, not as an absolute path
                    var combinedPath = relDir!.StartsWith('/') && !relDir.StartsWith("//")
                        ? relDir.Substring(1) // Strip leading slash to make it relative
                        : relDir;

                    // Combine and normalize
                    targetDir = Path.GetFullPath(Path.Combine(normalizedWorkingDir, combinedPath));
                }

                // Debug logging
                var externalPathsCount = Options?.AllowedExternalPaths?.Count ?? 0;
                var externalPathsList = externalPathsCount > 0 ? string.Join(", ", Options!.AllowedExternalPaths!) : "none";

                if (!PathHelper.IsPathSafe(targetDir, normalizedWorkingDir, Options?.AllowedExternalPaths))
                    return $"Error: Access denied. Path must be in workspace or an allowed external path.\n\n[DEBUG] targetDir: {targetDir}\n[DEBUG] workingDirectory: {normalizedWorkingDir}\n[DEBUG] AllowedExternalPaths ({externalPathsCount}): {externalPathsList}";

                if (!Directory.Exists(targetDir))
                    return $"Error: Directory not found: {relDir ?? "."}\n\n[DEBUG] targetDir: {targetDir}\n[DEBUG] workingDirectory: {normalizedWorkingDir}";

                var files = Directory.EnumerateFiles(targetDir, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Select(p => Path.GetRelativePath(normalizedWorkingDir, p))
                    .OrderBy(p => p)
                    .ToList();

                if (files.Count == 0)
                    return $"No files found in: {relDir ?? "."}";

                // Build a clearer output format with case-insensitivity hint
                var result = new System.Text.StringBuilder();
                var displayDir = string.IsNullOrEmpty(relDir) ? "." : relDir;

                // Add case-insensitivity note for Windows
                if (OperatingSystem.IsWindows())
                {
                    result.AppendLine($"📁 Files in {displayDir} (case-insensitive - TODO.md, Todo.md, todo.md are equivalent):");
                }
                else
                {
                    result.AppendLine($"📁 Files in {displayDir}:");
                }
                result.AppendLine();

                foreach (var file in files)
                {
                    // Show original filename with lowercase in parentheses for clarity
                    result.AppendLine($"  {file}  [{Path.GetFileName(file).ToLowerInvariant()}]");
                }

                result.AppendLine();
                result.AppendLine($"Total: {files.Count} file(s)");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error listing files: {ex.Message}";
            }
        }
    }
}
