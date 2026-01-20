namespace DraCode.Agent.Tools
{
    public class DisplayText : Tool
    {
        public override string Name => "display_text";
        
        public override string Description => "Display text or information to the user without writing to a file. Use this to show messages, results, summaries, or any output that should be visible but not saved.";
        
        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                text = new
                {
                    type = "string",
                    description = "The text to display to the user"
                },
                title = new
                {
                    type = "string",
                    description = "Optional title or header for the displayed text"
                }
            },
            required = new[] { "text" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var text = input["text"].ToString();
                var title = input.TryGetValue("title", out var titleVal) ? titleVal?.ToString() : null;

                if (string.IsNullOrWhiteSpace(text))
                    return "Error: text parameter is required";

                Console.WriteLine();
                
                if (!string.IsNullOrWhiteSpace(title))
                {
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"  {title}");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                }
                
                Console.WriteLine(text);
                
                if (!string.IsNullOrWhiteSpace(title))
                {
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                }
                
                Console.WriteLine();

                return "Text displayed successfully";
            }
            catch (Exception ex)
            {
                return $"Error displaying text: {ex.Message}";
            }
        }
    }
}
