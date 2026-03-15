using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing and managing project notifications.
    /// Notifications are generated when features complete, projects finish, or escalations occur.
    /// </summary>
    public class NotificationsTool : Tool
    {
        private readonly Func<string, List<NotificationInfo>>? _getPendingNotifications;
        private readonly Action<string, IEnumerable<string>?>? _markAsRead;
        private readonly Func<List<(string ProjectName, int Count)>>? _getAllPendingCounts;

        public NotificationsTool(
            Func<string, List<NotificationInfo>>? getPendingNotifications,
            Action<string, IEnumerable<string>?>? markAsRead,
            Func<List<(string ProjectName, int Count)>>? getAllPendingCounts)
        {
            _getPendingNotifications = getPendingNotifications;
            _markAsRead = markAsRead;
            _getAllPendingCounts = getAllPendingCounts;
        }

        public override string Name => "view_notifications";

        public override string Description =>
            "View and manage project notifications. " +
            "Notifications are generated when features complete, projects finish, or issues arise. " +
            "Actions: 'list' (all projects with pending counts), 'project' (notifications for one project), " +
            "'dismiss' (mark specific notifications as read), 'dismiss_all' (clear all for a project).";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'list' (summary of all projects), 'project' (one project's notifications), 'dismiss' (mark as read), 'dismiss_all' (clear all for project)",
                    @enum = new[] { "list", "project", "dismiss", "dismiss_all" }
                },
                project = new
                {
                    type = "string",
                    description = "Project name (required for project, dismiss, dismiss_all)"
                },
                notification_ids = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Notification IDs to dismiss (required for 'dismiss' action)"
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionObj) ? actionObj?.ToString()?.ToLowerInvariant() : null;
            var project = input.TryGetValue("project", out var projObj) ? projObj?.ToString() : null;

            return action switch
            {
                "list" => ListAllPending(),
                "project" => ListProjectNotifications(project),
                "dismiss" => DismissNotifications(project, input),
                "dismiss_all" => DismissAll(project),
                _ => "Unknown action. Use 'list', 'project', 'dismiss', or 'dismiss_all'."
            };
        }

        private string ListAllPending()
        {
            if (_getAllPendingCounts == null)
                return "Notification service not available.";

            try
            {
                var counts = _getAllPendingCounts();
                if (counts.Count == 0)
                    return "No pending notifications across any project.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("## Pending Notifications\n");
                sb.AppendLine("| Project | Pending |");
                sb.AppendLine("|---------|---------|");

                var total = 0;
                foreach (var (projectName, count) in counts)
                {
                    sb.AppendLine($"| {projectName} | {count} |");
                    total += count;
                }

                sb.AppendLine();
                sb.AppendLine($"**Total**: {total} pending notification(s)");
                sb.AppendLine();
                sb.AppendLine("Use action:'project' with a project name to see details.");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error listing notifications: {ex.Message}";
            }
        }

        private string ListProjectNotifications(string? project)
        {
            if (string.IsNullOrEmpty(project))
                return "Error: 'project' parameter is required.";

            if (_getPendingNotifications == null)
                return "Notification service not available.";

            try
            {
                var notifications = _getPendingNotifications(project);
                if (notifications.Count == 0)
                    return $"No pending notifications for project '{project}'.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"## Notifications for {project} ({notifications.Count})\n");

                foreach (var n in notifications.OrderByDescending(n => n.CreatedAt))
                {
                    var icon = n.Type switch
                    {
                        "feature_branch_ready" => "✅",
                        "project_complete" => "🎉",
                        "escalation" => "⚠️",
                        "error" => "❌",
                        _ => "📋"
                    };

                    sb.AppendLine($"### {icon} {n.Type} — {n.CreatedAt:yyyy-MM-dd HH:mm}");
                    sb.AppendLine(n.Message);

                    if (n.Metadata.Count > 0)
                    {
                        foreach (var kvp in n.Metadata)
                        {
                            sb.AppendLine($"  - **{kvp.Key}**: {kvp.Value}");
                        }
                    }

                    sb.AppendLine($"  *ID: {n.Id}*");
                    sb.AppendLine();
                }

                sb.AppendLine("Use action:'dismiss_all' to clear these, or action:'dismiss' with notification_ids to dismiss specific ones.");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting notifications: {ex.Message}";
            }
        }

        private string DismissNotifications(string? project, Dictionary<string, object> input)
        {
            if (string.IsNullOrEmpty(project))
                return "Error: 'project' parameter is required.";

            if (_markAsRead == null)
                return "Notification service not available.";

            var ids = new List<string>();
            if (input.TryGetValue("notification_ids", out var idsObj))
            {
                if (idsObj is System.Text.Json.JsonElement jsonEl && jsonEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in jsonEl.EnumerateArray())
                    {
                        var id = item.GetString();
                        if (!string.IsNullOrEmpty(id))
                            ids.Add(id);
                    }
                }
                else if (idsObj is IEnumerable<object> list)
                {
                    ids.AddRange(list.Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)));
                }
            }

            if (ids.Count == 0)
                return "Error: 'notification_ids' parameter is required for 'dismiss' action.";

            try
            {
                _markAsRead(project, ids);
                return $"Dismissed {ids.Count} notification(s) for project '{project}'.";
            }
            catch (Exception ex)
            {
                return $"Error dismissing notifications: {ex.Message}";
            }
        }

        private string DismissAll(string? project)
        {
            if (string.IsNullOrEmpty(project))
                return "Error: 'project' parameter is required.";

            if (_markAsRead == null)
                return "Notification service not available.";

            try
            {
                _markAsRead(project, null);
                return $"All notifications dismissed for project '{project}'.";
            }
            catch (Exception ex)
            {
                return $"Error dismissing notifications: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Simplified notification info for the tool (avoids dependency on Server project's ProjectNotification)
    /// </summary>
    public class NotificationInfo
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
