using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Model for running agent information per project
    /// </summary>
    public class RunningAgentInfo
    {
        public string ProjectId { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string ProjectStatus { get; set; } = "";
        public int ActiveDrakes { get; set; }
        public int ActiveKobolds { get; set; }
        public int WorkingKobolds { get; set; }
        public int AssignedKobolds { get; set; }
        public List<KoboldInfo> Kobolds { get; set; } = new();
        public List<DrakeInfo> Drakes { get; set; } = new();
    }

    public class KoboldInfo
    {
        public string Id { get; set; } = "";
        public string AgentType { get; set; } = "";
        public string Status { get; set; } = "";
        public string? TaskDescription { get; set; }
        public DateTime? StartedAt { get; set; }
        public TimeSpan? WorkingDuration { get; set; }
        public bool IsStuck { get; set; }
    }

    public class DrakeInfo
    {
        public string Name { get; set; } = "";
        public string TaskFile { get; set; } = "";
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int WorkingTasks { get; set; }
        public int PendingTasks { get; set; }
    }

    /// <summary>
    /// Tool for viewing running agents (Drakes, Kobolds) per project
    /// </summary>
    public class AgentStatusTool : Tool
    {
        private readonly Func<List<RunningAgentInfo>>? _getRunningAgents;
        private readonly Func<string, RunningAgentInfo?>? _getRunningAgentsForProject;
        private readonly Func<KoboldStatistics>? _getGlobalKoboldStats;

        public AgentStatusTool(
            Func<List<RunningAgentInfo>>? getRunningAgents = null,
            Func<string, RunningAgentInfo?>? getRunningAgentsForProject = null,
            Func<KoboldStatistics>? getGlobalKoboldStats = null)
        {
            _getRunningAgents = getRunningAgents;
            _getRunningAgentsForProject = getRunningAgentsForProject;
            _getGlobalKoboldStats = getGlobalKoboldStats;
        }

        public override string Name => "agent_status";

        public override string Description =>
            "View running agents (Drakes, Kobolds) per project. Shows active workers, their status, tasks, and working duration. " +
            "Use action 'list' to see all projects with running agents, 'project' to see details for a specific project, " +
            "or 'summary' for a global overview of all agent activity.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'list' (all projects with agents), 'project' (specific project), 'summary' (global stats)",
                    @enum = new[] { "list", "project", "summary" }
                },
                project = new
                {
                    type = "string",
                    description = "Project ID or name (required for 'project' action)"
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionVal)
                ? actionVal?.ToString()?.ToLowerInvariant()
                : "list";

            return action switch
            {
                "list" => ExecuteList(),
                "project" => ExecuteProject(input),
                "summary" => ExecuteSummary(),
                _ => $"Unknown action: {action}. Use 'list', 'project', or 'summary'."
            };
        }

        private string ExecuteList()
        {
            if (_getRunningAgents == null)
            {
                return "Agent status information is not available.";
            }

            try
            {
                var projects = _getRunningAgents();

                if (projects.Count == 0)
                {
                    return "No running agents found. All projects are idle.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"# Running Agents ({projects.Count} project(s) with activity)\n");

                foreach (var project in projects.OrderByDescending(p => p.ActiveKobolds + p.ActiveDrakes))
                {
                    var statusIcon = project.ProjectStatus switch
                    {
                        "InProgress" => "🔨",
                        "Analyzed" => "✅",
                        "WyvernAssigned" => "📋",
                        _ => "📦"
                    };

                    result.AppendLine($"## {statusIcon} {project.ProjectName}");
                    result.AppendLine($"   Status: {project.ProjectStatus}");
                    result.AppendLine($"   Active Drakes: {project.ActiveDrakes}");
                    result.AppendLine($"   Active Kobolds: {project.ActiveKobolds} (Working: {project.WorkingKobolds}, Assigned: {project.AssignedKobolds})");

                    if (project.Drakes.Count > 0)
                    {
                        result.AppendLine("   Drakes:");
                        foreach (var drake in project.Drakes)
                        {
                            result.AppendLine($"     - {drake.Name}: {drake.CompletedTasks}/{drake.TotalTasks} tasks done, {drake.WorkingTasks} working, {drake.PendingTasks} pending");
                        }
                    }

                    if (project.Kobolds.Count > 0)
                    {
                        result.AppendLine("   Kobolds:");
                        foreach (var kobold in project.Kobolds.Take(5)) // Limit to first 5 for readability
                        {
                            var durationStr = kobold.WorkingDuration.HasValue
                                ? $" ({kobold.WorkingDuration.Value.TotalMinutes:F1}m)"
                                : "";
                            var stuckStr = kobold.IsStuck ? " ⚠️ STUCK" : "";
                            var taskStr = !string.IsNullOrEmpty(kobold.TaskDescription)
                                ? $" - {Truncate(kobold.TaskDescription, 50)}"
                                : "";
                            result.AppendLine($"     - [{kobold.AgentType}] {kobold.Status}{durationStr}{stuckStr}{taskStr}");
                        }
                        if (project.Kobolds.Count > 5)
                        {
                            result.AppendLine($"     ... and {project.Kobolds.Count - 5} more Kobolds");
                        }
                    }

                    result.AppendLine();
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting running agents: {ex.Message}";
            }
        }

        private string ExecuteProject(Dictionary<string, object> input)
        {
            if (_getRunningAgentsForProject == null)
            {
                return "Agent status information is not available.";
            }

            if (!input.TryGetValue("project", out var projectVal) || string.IsNullOrEmpty(projectVal?.ToString()))
            {
                return "Error: 'project' parameter is required for the 'project' action. Provide a project ID or name.";
            }

            var projectIdOrName = projectVal.ToString()!;

            try
            {
                var project = _getRunningAgentsForProject(projectIdOrName);

                if (project == null)
                {
                    return $"Project '{projectIdOrName}' not found or has no running agents.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"# Agent Status: {project.ProjectName}");
                result.AppendLine();
                result.AppendLine($"**Project ID:** {project.ProjectId}");
                result.AppendLine($"**Status:** {project.ProjectStatus}");
                result.AppendLine();
                result.AppendLine("## Summary");
                result.AppendLine($"- Active Drakes: {project.ActiveDrakes}");
                result.AppendLine($"- Active Kobolds: {project.ActiveKobolds}");
                result.AppendLine($"  - Working: {project.WorkingKobolds}");
                result.AppendLine($"  - Assigned (waiting): {project.AssignedKobolds}");
                result.AppendLine();

                if (project.Drakes.Count > 0)
                {
                    result.AppendLine("## Drakes (Supervisors)");
                    foreach (var drake in project.Drakes)
                    {
                        var progress = drake.TotalTasks > 0
                            ? $"{(drake.CompletedTasks * 100.0 / drake.TotalTasks):F0}%"
                            : "N/A";
                        result.AppendLine($"### {drake.Name}");
                        result.AppendLine($"- Task File: {drake.TaskFile}");
                        result.AppendLine($"- Progress: {progress} ({drake.CompletedTasks}/{drake.TotalTasks} tasks)");
                        result.AppendLine($"- Working: {drake.WorkingTasks} | Pending: {drake.PendingTasks}");
                        result.AppendLine();
                    }
                }

                if (project.Kobolds.Count > 0)
                {
                    result.AppendLine("## Kobolds (Workers)");
                    result.AppendLine();
                    result.AppendLine("| ID | Type | Status | Duration | Task |");
                    result.AppendLine("|-----|------|--------|----------|------|");

                    foreach (var kobold in project.Kobolds)
                    {
                        var durationStr = kobold.WorkingDuration.HasValue
                            ? $"{kobold.WorkingDuration.Value.TotalMinutes:F1}m"
                            : "-";
                        var statusStr = kobold.IsStuck ? $"⚠️ {kobold.Status}" : kobold.Status;
                        var taskStr = Truncate(kobold.TaskDescription ?? "-", 40);
                        result.AppendLine($"| {kobold.Id[..8]} | {kobold.AgentType} | {statusStr} | {durationStr} | {taskStr} |");
                    }
                    result.AppendLine();
                }
                else if (project.ActiveKobolds == 0)
                {
                    result.AppendLine("## Kobolds (Workers)");
                    result.AppendLine("No Kobolds currently active for this project.");
                    result.AppendLine();
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting project agent status: {ex.Message}";
            }
        }

        private string ExecuteSummary()
        {
            if (_getRunningAgents == null || _getGlobalKoboldStats == null)
            {
                return "Agent status information is not available.";
            }

            try
            {
                var projects = _getRunningAgents();
                var koboldStats = _getGlobalKoboldStats();

                var result = new System.Text.StringBuilder();
                result.AppendLine("# Global Agent Status Summary\n");

                // Kobold statistics
                result.AppendLine("## Kobold Statistics");
                result.AppendLine($"- **Total Kobolds:** {koboldStats.Total}");
                result.AppendLine($"- Unassigned: {koboldStats.Unassigned}");
                result.AppendLine($"- Assigned: {koboldStats.Assigned}");
                result.AppendLine($"- Working: {koboldStats.Working}");
                result.AppendLine($"- Done: {koboldStats.Done}");
                result.AppendLine();

                if (koboldStats.ByAgentType.Count > 0)
                {
                    result.AppendLine("### By Agent Type");
                    foreach (var kvp in koboldStats.ByAgentType.OrderByDescending(k => k.Value))
                    {
                        result.AppendLine($"- {kvp.Key}: {kvp.Value}");
                    }
                    result.AppendLine();
                }

                // Project summary
                var totalDrakes = projects.Sum(p => p.ActiveDrakes);
                var totalActiveKobolds = projects.Sum(p => p.ActiveKobolds);
                var projectsWithActivity = projects.Count(p => p.ActiveDrakes > 0 || p.ActiveKobolds > 0);

                result.AppendLine("## Project Overview");
                result.AppendLine($"- **Projects with activity:** {projectsWithActivity}");
                result.AppendLine($"- **Total active Drakes:** {totalDrakes}");
                result.AppendLine($"- **Total active Kobolds:** {totalActiveKobolds}");
                result.AppendLine();

                if (projectsWithActivity > 0)
                {
                    result.AppendLine("### Active Projects");
                    foreach (var project in projects.Where(p => p.ActiveDrakes > 0 || p.ActiveKobolds > 0).OrderByDescending(p => p.WorkingKobolds))
                    {
                        result.AppendLine($"- **{project.ProjectName}**: {project.ActiveDrakes} Drake(s), {project.ActiveKobolds} Kobold(s) ({project.WorkingKobolds} working)");
                    }
                }
                else
                {
                    result.AppendLine("*No projects currently have running agents.*");
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting agent summary: {ex.Message}";
            }
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text[..(maxLength - 3)] + "...";
        }
    }
}
