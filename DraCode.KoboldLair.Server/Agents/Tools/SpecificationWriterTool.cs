using DraCode.Agent.Tools;
using DraCode.KoboldLair.Server.Models.Projects;

namespace DraCode.KoboldLair.Server.Agents.Tools
{
    /// <summary>
    /// Legacy tool for backward compatibility - redirects to SpecificationManagementTool
    /// </summary>
    public class SpecificationWriterTool : Tool
    {
        private readonly SpecificationManagementTool _managementTool;

        public SpecificationWriterTool(string specificationsPath, Dictionary<string, Specification> specifications)
        {
            _managementTool = new SpecificationManagementTool(specificationsPath, specifications);
        }

        public override string Name => "write_specification";

        public override string Description =>
            "Writes a project or task specification to a markdown file in the specifications directory. " +
            "Use this when you have gathered enough information from the user.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                filename = new
                {
                    type = "string",
                    description = "Filename for the specification (e.g., 'web-app-project.md', 'api-refactor-task.md'). Use kebab-case."
                },
                content = new
                {
                    type = "string",
                    description = "Full markdown content of the specification document"
                }
            },
            required = new[] { "filename", "content" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("filename", out var filenameObj) ||
                !input.TryGetValue("content", out var contentObj))
            {
                return "Error: Both filename and content are required";
            }

            var filename = filenameObj.ToString() ?? "";
            var name = Path.GetFileNameWithoutExtension(filename);

            // Redirect to create action
            var managementInput = new Dictionary<string, object>
            {
                { "action", "create" },
                { "name", name },
                { "content", contentObj }
            };

            return _managementTool.Execute(workingDirectory, managementInput);
        }
    }
}
