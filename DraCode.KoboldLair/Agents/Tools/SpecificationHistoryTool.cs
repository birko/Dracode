using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services.EventSourcing;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing specification version history.
    /// When event sourcing is available, shows rich event-level audit trail.
    /// Falls back to in-memory VersionHistory when event store is not configured.
    /// </summary>
    public class SpecificationHistoryTool : Tool
    {
        private readonly Dictionary<string, Specification> _specifications;
        private readonly object _specificationsLock;
        private readonly SpecificationEventService? _eventService;

        public SpecificationHistoryTool(Dictionary<string, Specification> specifications, SpecificationEventService? eventService = null)
        {
            _specifications = specifications;
            _specificationsLock = new object();
            _eventService = eventService;
        }

        public override string Name => "view_specification_history";

        public override string Description =>
            "Views the version history of a project specification, showing when changes were made including feature additions and modifications.";

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

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("name", out var nameObj))
            {
                return "Error: name is required";
            }

            var name = nameObj.ToString() ?? "";

            Specification? spec;
            lock (_specificationsLock)
            {
                if (!_specifications.TryGetValue(name, out spec))
                {
                    return $"Error: Specification '{name}' not found";
                }
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"# Specification History: {name}");
            result.AppendLine();
            result.AppendLine($"**Current Version**: {spec.Version}");
            result.AppendLine($"**Content Hash**: {spec.ContentHash}");
            result.AppendLine($"**Last Updated**: {spec.UpdatedAt:u}");
            result.AppendLine();

            // Try event sourcing first for richer history
            if (_eventService != null)
            {
                try
                {
                    var events = await _eventService.GetSpecificationHistoryAsync(spec.Id);
                    var eventList = events.ToList();

                    if (eventList.Count > 0)
                    {
                        result.AppendLine("## Event Audit Trail");
                        result.AppendLine();
                        result.AppendLine("| # | Event | Timestamp | Details |");
                        result.AppendLine("|---|-------|-----------|---------|");

                        foreach (var evt in eventList)
                        {
                            var details = FormatEventDetails(evt.EventType, evt.EventData);
                            result.AppendLine($"| {evt.Version} | {evt.EventType} | {evt.OccurredAt:u} | {details} |");
                        }

                        result.AppendLine();
                        result.AppendLine($"*{eventList.Count} event(s) recorded*");
                        return result.ToString();
                    }
                }
                catch
                {
                    // Fall through to version history
                }
            }

            // Fall back to in-memory version history
            lock (_specificationsLock)
            {
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
            }

            return result.ToString();
        }

        private static string FormatEventDetails(string eventType, string eventData)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(eventData);
                var root = doc.RootElement;

                return eventType switch
                {
                    "SpecificationCreated" => $"Project: {GetProp(root, "projectId")}",
                    "SpecificationUpdated" => $"v{GetProp(root, "version")} — {GetProp(root, "changeDescription") ?? "content updated"}",
                    "SpecificationApproved" => $"By: {GetProp(root, "approvedBy")}",
                    "FeatureAdded" => $"Feature: {GetProp(root, "name")} ({GetProp(root, "priority")})",
                    "FeatureModified" => $"Feature: {GetProp(root, "name")} modified",
                    "FeatureRemoved" => $"Feature: {GetProp(root, "name")} — {GetProp(root, "reason") ?? "removed"}",
                    "FeatureStatusChanged" => $"Feature: {GetProp(root, "featureId")} {GetProp(root, "oldStatus")} → {GetProp(root, "newStatus")}",
                    _ => eventType
                };
            }
            catch
            {
                return eventType;
            }
        }

        private static string? GetProp(System.Text.Json.JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var prop) ? prop.GetString() : null;
        }
    }
}
