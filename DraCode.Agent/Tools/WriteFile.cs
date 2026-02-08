using DraCode.Agent.Helpers;

namespace DraCode.Agent.Tools
{
    public class WriteFile : Tool
    {
        public override string Name => "write_file";
        public override string Description => "Write text content to a file in the workspace. IMPORTANT: Before using write_file, check if the file exists using list_files or read_file. If the file exists, use edit_file instead to preserve existing content. Only use write_file for new files or complete rewrites.";
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
                },
                check_exists = new
                {
                    type = "boolean",
                    description = "Check if file exists and warn if it does (prevents accidental overwrites)",
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
                var checkExists = !input.TryGetValue("check_exists", out var checkExistsValue) || !bool.TryParse(checkExistsValue?.ToString(), out var parsedCheck) || parsedCheck;

                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("file_path is required");

                var fullPath = Path.Combine(workingDirectory, filePath);

                if (!PathHelper.IsPathSafe(fullPath, workingDirectory, Options?.AllowedExternalPaths))
                    return "Error: Access denied. Path must be in workspace or an allowed external path.";

                // Check if file already exists and warn (helps prevent accidental overwrites)
                if (checkExists && File.Exists(fullPath))
                {
                    return $"Warning: File '{filePath}' already exists. If you want to modify it, use edit_file to make surgical changes that preserve existing content. If you want to completely replace the file, call write_file again with check_exists=false. Consider reading the file first to see what's already there.";
                }

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
