using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for skipping verification on imported or legacy projects.
    /// </summary>
    public class SkipVerificationTool : Tool
    {
        private readonly Func<string, bool>? _skipVerification;

        public SkipVerificationTool(
            Func<string, bool>? skipVerification)
        {
            _skipVerification = skipVerification;
        }

        public override string Name => "skip_verification";

        public override string Description =>
            "Skip verification for a project and mark it as complete. " +
            "Useful for imported projects or when verification is not applicable. " +
            "This permanently skips verification for the project.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project = new
                {
                    type = "string",
                    description = "Project name or ID"
                }
            },
            required = new[] { "project" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var project = input.TryGetValue("project", out var projObj) ? projObj?.ToString() : null;

            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required.";
            }

            if (_skipVerification == null)
            {
                return "Error: Verification skip service not available.";
            }

            try
            {
                var success = _skipVerification(project);

                if (success)
                {
                    return $"✅ **Verification skipped for '{project}'**\n\n" +
                           "The project has been marked as Verified without running checks.\n" +
                           "Project status has been updated to 'Verified'.";
                }
                else
                {
                    return $"❌ Could not skip verification for '{project}'.\n\n" +
                           "This can happen if:\n" +
                           "- The project doesn't exist\n" +
                           "- The project is not in 'AwaitingVerification' status\n\n" +
                           "Use the 'retry_verification' tool with action='list' to see projects eligible for verification skip.";
                }
            }
            catch (Exception ex)
            {
                return $"Error skipping verification: {ex.Message}";
            }
        }
    }
}
