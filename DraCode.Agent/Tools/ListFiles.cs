using DraCode.Agent.Helpers;

namespace DraCode.Agent.Tools
{
    public class ListFiles : Tool
    {
        public override string Name => "list_files";
        public override string Description => "List files in the workspace or a subdirectory.";
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
                var relDir = input != null && input.TryGetValue("directory", out var dirVal) ? dirVal?.ToString() : null;
                var recursive = input != null && input.TryGetValue("recursive", out var recVal) && bool.TryParse(recVal?.ToString(), out var recParsed) && recParsed;

                var targetDir = string.IsNullOrWhiteSpace(relDir) ? workingDirectory : Path.Combine(workingDirectory, relDir!);

                if (!PathHelper.IsPathSafe(targetDir, workingDirectory, Options?.AllowedExternalPaths))
                    return "Error: Access denied. Path must be in workspace or an allowed external path.";

                if (!Directory.Exists(targetDir))
                    return $"Error: Directory not found: {relDir ?? "."}";

                var files = Directory.EnumerateFiles(targetDir, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Select(p => Path.GetRelativePath(workingDirectory, p))
                    .OrderBy(p => p)
                    .ToList();

                return string.Join(Environment.NewLine, files);
            }
            catch (Exception ex)
            {
                return $"Error listing files: {ex.Message}";
            }
        }
    }
}
