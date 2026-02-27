using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing specification version history
    /// </summary>
    public class SpecificationHistoryTool : Tool
    {
        private readonly Dictionary<string, Specification> _specifications;
        private readonly object _specificationsLock;

        public SpecificationHistoryTool(Dictionary<string, Specification> specifications)
        {
            _specifications = specifications;
            _specificationsLock = new object();
        }

        public override string Name => "view_specification_history";

        public override string Description =>
            "Views the version history of a project specification, showing when changes were made.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                name = new
                {
                    type = "string",
                    description = "Specification/project name"
                }
            },
            required = new[] { "name" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("name", out var nameObj))
            {
                return "Error: name is required";
            }

            var name = nameObj.ToString() ?? "";

            lock (_specificationsLock)
            {
                if (!_specifications.TryGetValue(name, out var spec))
                {
                    return $"Error: Specification '{name}' not found";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"# Specification History: {name}");
                result.AppendLine();
                result.AppendLine($"**Current Version**: {spec.Version}");
                result.AppendLine($"**Content Hash**: {spec.ContentHash}");
                result.AppendLine($"**Last Updated**: {spec.UpdatedAt:u}");
                result.AppendLine();

                if (spec.VersionHistory.Any())
                {
                    result.AppendLine("## Version History");
                    result.AppendLine();
                    result.AppendLine("| Version | Timestamp | Hash | Description |");
                    result.AppendLine("|---------|-----------|------|-------------|");

                    foreach (var entry in spec.VersionHistory.OrderByDescending(v => v.Version))
                    {
                        var hashDisplay = entry.ContentHash.Length > 16
                            ? entry.ContentHash[..16] + "..."
                            : entry.ContentHash;
                        result.AppendLine($"| {entry.Version} | {entry.Timestamp:u} | {hashDisplay} | {entry.ChangeDescription ?? "-"} |");
                    }
                }
                else
                {
                    result.AppendLine("*No version history available (history tracking starts after this update)*");
                }

                return result.ToString();
            }
        }
    }
}
