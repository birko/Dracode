using DraCode.Agent.Helpers;

namespace DraCode.Agent.Tools
{
    public class WriteFile : Tool
    {
        public override string Name => "write_file";
        public override string Description => "Write text content to a file in the workspace. Overwrites existing content.";
        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                file_path = new
                {
                    type = "string",
                    description = "Path to the file relative to workspace root"
                },
                content = new
                {
                    type = "string",
                    description = "Text content to write to the file"
                },
                create_directories = new
                {
                    type = "boolean",
                    description = "Create directories if they do not exist",
                    @default = true
                }
            },
            required = new[] { "file_path", "content" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var filePath = input["file_path"].ToString();
                var content = input.TryGetValue("content", out object? value) ? value?.ToString() ?? string.Empty : string.Empty;
                var createDirs = !input.TryGetValue("create_directories", out var createDirsValue) || !bool.TryParse(createDirsValue?.ToString(), out var parsed) || parsed;

                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("file_path is required");

                var fullPath = Path.Combine(workingDirectory, filePath);

                if (!PathHelper.IsPathSafe(fullPath, workingDirectory))
                    return $"Error: Access denied. File must be in {workingDirectory}";

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    if (createDirs)
                        Directory.CreateDirectory(dir);
                    else
                        return $"Error: Directory does not exist: {dir}";
                }

                File.WriteAllText(fullPath, content);
                return "OK";
            }
            catch (Exception ex)
            {
                return $"Error writing file: {ex.Message}";
            }
        }
    }
}
