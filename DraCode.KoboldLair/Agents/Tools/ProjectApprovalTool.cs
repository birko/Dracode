using Birko.AI.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for Dragon to approve project specifications.
    /// Changes project status from Prototype to New, allowing Wyvern to process it.
    /// </summary>
    public class ProjectApprovalTool : Tool
    {
        private readonly Func<string, bool>? _approveProject;

        public ProjectApprovalTool(Func<string, bool>? approveProject)
        {
            _approveProject = approveProject;
        }

        public override string Name => "approve_specification";

        public override string Description =>
            "Approves a project specification after user confirms it is correct. " +
            "This changes the project status from 'Prototype' to 'New', allowing Wyvern to start processing. " +
            "IMPORTANT: Only use this after explicitly asking the user if the specification is complete and correct.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project_name = new
                {
                    type = "string",
                    description = "Name of the project to approve"
                },
                confirmation = new
                {
                    type = "string",
                    description = "User's confirmation response (must indicate approval)",
                    @enum = new[] { "yes", "confirmed", "approved", "correct", "looks good" }
                }
            },
            required = new[] { "project_name", "confirmation" }
        };

        public override Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input, CancellationToken cancellationToken = default)
        {
            if (_approveProject == null)
            {
                return Task.FromResult("Error: Project approval is not configured.");
            }

            if (!input.TryGetValue("project_name", out var nameObj))
            {
                return Task.FromResult("Error: project_name is required");
            }

            if (!input.TryGetValue("confirmation", out var confirmObj))
            {
                return Task.FromResult("Error: confirmation is required. Ask the user to confirm the specification first.");
            }

            var projectName = nameObj.ToString() ?? "";
            var confirmation = confirmObj.ToString()?.ToLowerInvariant() ?? "";

            // Validate confirmation
            var validConfirmations = new[] { "yes", "confirmed", "approved", "correct", "looks good" };
            if (!validConfirmations.Contains(confirmation))
            {
                return Task.FromResult($"Error: Invalid confirmation '{confirmation}'. User must explicitly confirm the specification is correct.");
            }

            try
            {
                var success = _approveProject(projectName);
                if (success)
                {
                    return Task.FromResult($"✅ Project '{projectName}' has been approved!\n\n" +
                           "The specification is now ready for processing. Wyvern will analyze it and create tasks for the Kobolds.\n" +
                           "You can continue refining other projects or wait for the analysis to complete.");
                }
                else
                {
                    return Task.FromResult($"Error: Could not approve project '{projectName}'. " +
                           "Make sure the project exists and is in 'Prototype' status.");
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error approving project: {ex.Message}");
            }
        }
    }
}
