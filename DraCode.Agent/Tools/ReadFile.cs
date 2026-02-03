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
                var filePath = input["file_path"].ToString();
                if (string.IsNullOrEmpty(filePath))
                    throw new ArgumentException("file_path is required");

                var fullPath = Path.Combine(workingDirectory, filePath);

                if (!PathHelper.IsPathSafe(fullPath, workingDirectory, Options?.AllowedExternalPaths))
                    return "Error: Access denied. Path must be in workspace or an allowed external path.";

                return File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }
    }
}
