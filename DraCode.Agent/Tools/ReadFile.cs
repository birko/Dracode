using DraCode.Agent.Helpers;

namespace DraCode.Agent.Tools
{
    public class ReadFile : Tool
    {
        public override string Name => "read_file";
        public override string Description => "Read the contents of a file in the workspace. Use this to examine existing code or files.";
        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                file_path = new
                {
                    type = "string",
                    description = "Path to the file relative to workspace root"
                }
            },
            required = new[] { "file_path" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var filePath = input["file_path"].ToString()?.Trim();
                if (string.IsNullOrEmpty(filePath))
                    throw new ArgumentException("file_path is required");

                // Normalize the working directory
                var normalizedWorkingDir = Path.GetFullPath(workingDirectory);

                // Handle LLMs that use Unix-style absolute paths to reference workspace
                // e.g., "/workspace/file.txt" should be treated as "file.txt" relative to workspace
                var relativePath = filePath.StartsWith('/') && !filePath.StartsWith("//")
                    ? filePath.Substring(1)
                    : filePath;

                var fullPath = Path.GetFullPath(Path.Combine(normalizedWorkingDir, relativePath));

                // Debug logging
                var externalPathsCount = Options?.AllowedExternalPaths?.Count ?? 0;
                var externalPathsList = externalPathsCount > 0 ? string.Join(", ", Options!.AllowedExternalPaths!) : "none";

                if (!PathHelper.IsPathSafe(fullPath, normalizedWorkingDir, Options?.AllowedExternalPaths))
                    return $"Error: Access denied. Path must be in workspace or an allowed external path.\n\n[DEBUG] fullPath: {fullPath}\n[DEBUG] workingDirectory: {normalizedWorkingDir}\n[DEBUG] AllowedExternalPaths ({externalPathsCount}): {externalPathsList}";

                return File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }
    }
}
