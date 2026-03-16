using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for Dragon to delegate tasks to specialized sub-agents (the Dragon Council).
    /// </summary>
    public class DelegateToCouncilTool : Tool
    {
        private readonly Func<string, string, Task<string>>? _delegateToSubAgent;
        private Action<string, string>? _statusCallback;

        /// <summary>
        /// Creates a new DelegateToCouncilTool
        /// </summary>
        /// <param name="delegateToSubAgent">Function to delegate (subAgentName, task) => result</param>
        public DelegateToCouncilTool(Func<string, string, Task<string>>? delegateToSubAgent)
        {
            _delegateToSubAgent = delegateToSubAgent;
        }

        /// <summary>
        /// Sets a callback for status updates during delegation
        /// </summary>
        public void SetStatusCallback(Action<string, string>? callback)
        {
            _statusCallback = callback;
        }

        /// <summary>
        /// Sends a status update if callback is set
        /// </summary>
        private void SendStatus(string councilMember, string statusType)
        {
            _statusCallback?.Invoke(councilMember, statusType);
        }

        public override string Name => "delegate_to_council";

        public override string Description =>
            "Delegate a task to a specialized member of the Dragon Council. " +
            "Use this to route tasks to the appropriate specialist: " +
            "Sage (specifications/features/delete features), Seeker (scan projects), " +
            "Sentinel (git status/init/diff/commit/merge), Warden (agent config/task details/progress/workspace/project deletion).";

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

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            var councilMember = input.TryGetValue("council_member", out var memberObj) ? memberObj?.ToString()?.ToLowerInvariant() : null;
            var task = input.TryGetValue("task", out var taskObj) ? taskObj?.ToString() : null;

            // Debug: Log all received keys
            var receivedKeys = string.Join(", ", input.Keys);
            var taskObjType = taskObj?.GetType().Name ?? "null";
            var taskObjValue = taskObj?.ToString()?.Substring(0, Math.Min(100, taskObj.ToString()?.Length ?? 0)) ?? "null";

            if (string.IsNullOrEmpty(councilMember))
            {
                return $"Error: 'council_member' is required. Choose: sage, seeker, sentinel, or warden.\n[DEBUG] Received keys: {receivedKeys}";
            }

            if (string.IsNullOrEmpty(task))
            {
                return $"Error: 'task' description is required.\n[DEBUG] Received keys: {receivedKeys}\n[DEBUG] taskObj type: {taskObjType}\n[DEBUG] taskObj value: {taskObjValue}";
            }

            if (!IsValidCouncilMember(councilMember))
            {
                return $"Error: Unknown council member '{councilMember}'. Choose: sage, seeker, sentinel, or warden.";
            }

            if (_delegateToSubAgent == null)
            {
                return "Error: Delegation service not available.";
            }

            // Send status update before delegating
            SendStatus(councilMember!, "delegating");

            try
            {
                var result = await _delegateToSubAgent(councilMember, task);

                // Send status update after completion
                SendStatus(councilMember!, "complete");

                return result;
            }
            catch (Exception ex)
            {
                SendStatus(councilMember!, "error");
                return $"Error delegating to {councilMember}: {ex.Message}";
            }
        }

        private static bool IsValidCouncilMember(string member)
        {
            return member == "sage" || member == "seeker" || member == "sentinel" || member == "warden";
        }
    }
}
