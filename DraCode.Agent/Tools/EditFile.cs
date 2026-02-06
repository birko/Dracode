using DraCode.Agent.Helpers;

namespace DraCode.Agent.Tools
{
    public class EditFile : Tool
    {
        public override string Name => "edit_file";
        public override string Description => "Edit a file by replacing a specific text block. Use this to make surgical changes to existing files without overwriting the entire content.";
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
                old_text = new
                {
                    type = "string",
                    description = "The exact text to find and replace. Must match exactly (including whitespace)."
                },
                new_text = new
                {
                    type = "string",
                    description = "The text to replace the old_text with"
                }
            },
            required = new[] { "file_path", "old_text", "new_text" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var filePath = input["file_path"].ToString();
                var oldText = input["old_text"].ToString() ?? string.Empty;
                var newText = input["new_text"].ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("file_path is required");

                if (string.IsNullOrEmpty(oldText))
                    throw new ArgumentException("old_text is required");

                var fullPath = Path.Combine(workingDirectory, filePath);

                if (!PathHelper.IsPathSafe(fullPath, workingDirectory, Options?.AllowedExternalPaths))
                    return "Error: Access denied. Path must be in workspace or an allowed external path.";

                if (!File.Exists(fullPath))
                    return $"Error: File does not exist: {filePath}";

                var content = File.ReadAllText(fullPath);

                // Check if old_text exists in the file
                if (!content.Contains(oldText))
                {
                    // Try to provide helpful feedback
                    var lines = content.Split('\n');
                    var preview = lines.Length > 10 
                        ? string.Join("\n", lines.Take(10)) + "\n... (truncated)"
                        : content;
                    return $"Error: old_text not found in file. Make sure it matches exactly (including whitespace).\n\nFile preview:\n{preview}";
                }

                // Count occurrences
                var occurrences = CountOccurrences(content, oldText);
                if (occurrences > 1)
                {
                    return $"Error: old_text appears {occurrences} times in the file. Please provide a more specific text block that appears only once.";
                }

                // Replace the text
                var newContent = content.Replace(oldText, newText);
                File.WriteAllText(fullPath, newContent);

                return "OK";
            }
            catch (Exception ex)
            {
                return $"Error editing file: {ex.Message}";
            }
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }
    }
}
