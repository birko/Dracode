using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for Dragon to delegate tasks to specialized sub-agents (the Dragon Council).
    /// </summary>
    public class DelegateToCouncilTool : Tool
    {
        private readonly Func<string, string, Task<string>>? _delegateToSubAgent;

        /// <summary>
        /// Creates a new DelegateToCouncilTool
        /// </summary>
        /// <param name="delegateToSubAgent">Function to delegate (subAgentName, task) => result</param>
        public DelegateToCouncilTool(Func<string, string, Task<string>>? delegateToSubAgent)
        {
            _delegateToSubAgent = delegateToSubAgent;
        }

        public override string Name => "delegate_to_council";

        public override string Description =>
            "Delegate a task to a specialized member of the Dragon Council. " +
            "Use this to route tasks to the appropriate specialist: " +
            "Sage (specifications/features), Seeker (scan projects), Sentinel (git), Warden (agent config).";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                council_member = new
                {
                    type = "string",
                    description = "Which council member to delegate to",
                    @enum = new[] { "sage", "seeker", "sentinel", "warden" }
                },
                task = new
                {
                    type = "string",
                    description = "The task description to pass to the council member. Include all relevant context and user requirements."
                }
            },
            required = new[] { "council_member", "task" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            // This is a sync wrapper - actual delegation happens async in the service
            var councilMember = input.TryGetValue("council_member", out var memberObj) ? memberObj?.ToString()?.ToLowerInvariant() : null;
            var task = input.TryGetValue("task", out var taskObj) ? taskObj?.ToString() : null;

            if (string.IsNullOrEmpty(councilMember))
            {
                return "Error: 'council_member' is required. Choose: sage, seeker, sentinel, or warden.";
            }

            if (string.IsNullOrEmpty(task))
            {
                return "Error: 'task' description is required.";
            }

            if (!IsValidCouncilMember(councilMember))
            {
                return $"Error: Unknown council member '{councilMember}'. Choose: sage, seeker, sentinel, or warden.";
            }

            if (_delegateToSubAgent == null)
            {
                return "Error: Delegation service not available.";
            }

            // Execute async delegation (using async internally for non-blocking I/O)
            try
            {
                var result = _delegateToSubAgent(councilMember, task).GetAwaiter().GetResult();
                return result;
            }
            catch (Exception ex)
            {
                return $"Error delegating to {councilMember}: {ex.Message}";
            }
        }

        private static bool IsValidCouncilMember(string member)
        {
            return member == "sage" || member == "seeker" || member == "sentinel" || member == "warden";
        }
    }
}
