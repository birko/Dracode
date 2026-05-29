using Birko.AI.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing full verification reports for projects.
    /// </summary>
    public class ViewVerificationReportTool : Tool
    {
        private readonly Func<string, (bool Success, string? Report)>? _getVerificationReport;

        public ViewVerificationReportTool(
            Func<string, (bool Success, string? Report)>? getVerificationReport)
        {
            _getVerificationReport = getVerificationReport;
        }

        public override string Name => "view_verification_report";

        public override string Description =>
            "View the full verification report for a project. " +
            "Shows detailed results of all verification checks including build, test, and lint results.";

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

        public override Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            var project = input.TryGetValue("project", out var projObj) ? projObj?.ToString() : null;

            if (string.IsNullOrEmpty(project))
            {
                return Task.FromResult("Error: 'project' parameter is required.");
            }

            if (_getVerificationReport == null)
            {
                return Task.FromResult("Error: Verification report service not available.");
            }

            try
            {
                var (success, report) = _getVerificationReport(project);

                if (!success)
                {
                    return Task.FromResult($"❌ Project '{project}' not found.");
                }

                if (string.IsNullOrEmpty(report))
                {
                    return Task.FromResult($"⚠️ No verification report available for '{project}'.\n\n" +
                           "This can happen if:\n" +
                           "- Verification has not been run yet\n" +
                           "- Verification is currently in progress\n\n" +
                           "Use the 'retry_verification' tool with action='status' to check verification status.");
                }

                return Task.FromResult(report);
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error retrieving verification report: {ex.Message}");
            }
        }
    }
}
