using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;
using System.Text.Json;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing Wyrm pre-analysis recommendations and Wyvern task breakdown analysis.
    /// Shows what the automated analyzers decided about a project's tech stack, constraints,
    /// task structure, and requirements coverage.
    /// </summary>
    public class ViewAnalysisTool : Tool
    {
        private readonly Func<string, string?>? _getProjectFolder;
        private readonly Func<List<(string Id, string Name)>>? _getAllProjects;

        public ViewAnalysisTool(
            Func<string, string?>? getProjectFolder,
            Func<List<(string Id, string Name)>>? getAllProjects)
        {
            _getProjectFolder = getProjectFolder;
            _getAllProjects = getAllProjects;
        }

        public override string Name => "view_analysis";

        public override string Description =>
            "View Wyrm and Wyvern analysis results for a project. " +
            "Shows what the automated analyzers detected: tech stack, constraints, agent type recommendations, " +
            "task breakdown, requirements coverage, and project structure. " +
            "Actions: 'wyrm' (pre-analysis recommendations), 'wyvern' (task breakdown analysis), 'summary' (both in brief).";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'wyrm' (Wyrm recommendations), 'wyvern' (Wyvern analysis), 'summary' (brief overview of both)",
                    @enum = new[] { "wyrm", "wyvern", "summary" }
                },
                project = new
                {
                    type = "string",
                    description = "Project name or ID"
                }
            },
            required = new[] { "action", "project" }
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionObj) ? actionObj?.ToString()?.ToLowerInvariant() : null;
            var project = input.TryGetValue("project", out var projObj) ? projObj?.ToString() : null;

            if (string.IsNullOrEmpty(project))
                return "Error: 'project' parameter is required.";

            var folder = ResolveProjectFolder(project);
            if (folder == null)
                return $"Error: Project '{project}' not found.";

            return action switch
            {
                "wyrm" => ViewWyrmAnalysis(folder, project),
                "wyvern" => ViewWyvernAnalysis(folder, project),
                "summary" => ViewSummary(folder, project),
                _ => "Unknown action. Use 'wyrm', 'wyvern', or 'summary'."
            };
        }

        private string? ResolveProjectFolder(string projectNameOrId)
        {
            if (_getProjectFolder != null)
            {
                var folder = _getProjectFolder(projectNameOrId);
                if (folder != null) return folder;
            }
            return null;
        }

        private string ViewWyrmAnalysis(string folder, string projectName)
        {
            var path = Path.Combine(folder, "wyrm-recommendation.json");
            if (!File.Exists(path))
                return $"No Wyrm pre-analysis found for '{projectName}'. The project may not have been analyzed yet (status must be WyrmAssigned or later).";

            try
            {
                var json = File.ReadAllText(path);
                var rec = JsonSerializer.Deserialize<WyrmRecommendation>(json, _jsonOptions);
                if (rec == null)
                    return "Error: Could not parse Wyrm recommendation file.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"## Wyrm Pre-Analysis: {projectName}\n");
                sb.AppendLine($"**Created**: {rec.CreatedAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"**Complexity**: {rec.Complexity}\n");

                if (!string.IsNullOrEmpty(rec.AnalysisSummary))
                {
                    sb.AppendLine($"### Summary");
                    sb.AppendLine(rec.AnalysisSummary);
                    sb.AppendLine();
                }

                if (rec.RecommendedLanguages.Count > 0)
                {
                    sb.AppendLine($"### Languages");
                    sb.AppendLine(string.Join(", ", rec.RecommendedLanguages.Select(l => $"`{l}`")));
                    sb.AppendLine();
                }

                if (rec.TechnicalStack.Count > 0)
                {
                    sb.AppendLine($"### Technical Stack");
                    foreach (var tech in rec.TechnicalStack)
                        sb.AppendLine($"- {tech}");
                    sb.AppendLine();
                }

                if (rec.RecommendedAgentTypes.Count > 0)
                {
                    sb.AppendLine($"### Recommended Agent Types");
                    sb.AppendLine("| Area | Agent Type |");
                    sb.AppendLine("|------|-----------|");
                    foreach (var kvp in rec.RecommendedAgentTypes)
                        sb.AppendLine($"| {kvp.Key} | `{kvp.Value}` |");
                    sb.AppendLine();
                }

                if (rec.Constraints.Count > 0)
                {
                    sb.AppendLine($"### Constraints");
                    foreach (var c in rec.Constraints)
                        sb.AppendLine($"- ⛔ {c}");
                    sb.AppendLine();
                }

                if (rec.OutOfScope.Count > 0)
                {
                    sb.AppendLine($"### Out of Scope");
                    foreach (var o in rec.OutOfScope)
                        sb.AppendLine($"- 🚫 {o}");
                    sb.AppendLine();
                }

                if (rec.VerificationSteps.Count > 0)
                {
                    sb.AppendLine($"### Verification Steps");
                    foreach (var v in rec.VerificationSteps)
                        sb.AppendLine($"- [{v.Priority}] `{v.Command}` — {v.Description}");
                    sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(rec.Notes))
                {
                    sb.AppendLine($"### Notes");
                    sb.AppendLine(rec.Notes);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading Wyrm analysis: {ex.Message}";
            }
        }

        private string ViewWyvernAnalysis(string folder, string projectName)
        {
            var path = Path.Combine(folder, "analysis.json");
            if (!File.Exists(path))
                return $"No Wyvern analysis found for '{projectName}'. The project may not have been analyzed yet (status must be Analyzed or later).";

            try
            {
                var json = File.ReadAllText(path);
                var analysis = JsonSerializer.Deserialize<WyvernAnalysisView>(json, _jsonOptions);
                if (analysis == null)
                    return "Error: Could not parse Wyvern analysis file.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"## Wyvern Analysis: {projectName}\n");

                if (!string.IsNullOrEmpty(analysis.ProjectName))
                    sb.AppendLine($"**Project**: {analysis.ProjectName}");
                sb.AppendLine($"**Total Tasks**: {analysis.TotalTasks}");
                sb.AppendLine($"**Estimated Complexity**: {analysis.EstimatedComplexity}\n");

                if (analysis.Constraints?.Count > 0)
                {
                    sb.AppendLine("### Constraints");
                    foreach (var c in analysis.Constraints)
                        sb.AppendLine($"- ⛔ {c}");
                    sb.AppendLine();
                }

                if (analysis.OutOfScope?.Count > 0)
                {
                    sb.AppendLine("### Out of Scope");
                    foreach (var o in analysis.OutOfScope)
                        sb.AppendLine($"- 🚫 {o}");
                    sb.AppendLine();
                }

                if (analysis.Structure != null)
                {
                    sb.AppendLine("### Project Structure");

                    if (analysis.Structure.NamingConventions?.Count > 0)
                    {
                        sb.AppendLine("**Naming Conventions:**");
                        foreach (var kvp in analysis.Structure.NamingConventions)
                            sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                        sb.AppendLine();
                    }

                    if (analysis.Structure.DirectoryPurposes?.Count > 0)
                    {
                        sb.AppendLine("**Directories:**");
                        foreach (var kvp in analysis.Structure.DirectoryPurposes)
                            sb.AppendLine($"- `{kvp.Key}`: {kvp.Value}");
                        sb.AppendLine();
                    }
                }

                if (analysis.Areas?.Count > 0)
                {
                    sb.AppendLine("### Task Areas\n");
                    foreach (var area in analysis.Areas)
                    {
                        var taskCount = area.Tasks?.Count ?? 0;
                        sb.AppendLine($"**{area.Name}** ({taskCount} tasks)");
                        if (area.Tasks != null)
                        {
                            foreach (var task in area.Tasks)
                            {
                                var priorityIcon = task.Priority?.ToLowerInvariant() switch
                                {
                                    "critical" => "🔴",
                                    "high" => "🟠",
                                    "normal" => "🟢",
                                    "low" => "🔵",
                                    _ => "⚪"
                                };
                                var deps = task.Dependencies?.Count > 0 ? $" (depends on: {string.Join(", ", task.Dependencies)})" : "";
                                sb.AppendLine($"  {priorityIcon} `{task.Id}`: {task.Name} [{task.AgentType}]{deps}");
                            }
                        }
                        sb.AppendLine();
                    }
                }

                if (analysis.RequirementsCoverage?.Count > 0)
                {
                    sb.AppendLine("### Requirements Coverage\n");
                    sb.AppendLine("| Requirement | Covered By |");
                    sb.AppendLine("|-------------|-----------|");
                    foreach (var kvp in analysis.RequirementsCoverage)
                        sb.AppendLine($"| {kvp.Key} | `{kvp.Value}` |");
                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading Wyvern analysis: {ex.Message}";
            }
        }

        private string ViewSummary(string folder, string projectName)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"## Analysis Summary: {projectName}\n");

            // Wyrm summary
            var wyrmPath = Path.Combine(folder, "wyrm-recommendation.json");
            if (File.Exists(wyrmPath))
            {
                try
                {
                    var json = File.ReadAllText(wyrmPath);
                    var rec = JsonSerializer.Deserialize<WyrmRecommendation>(json, _jsonOptions);
                    if (rec != null)
                    {
                        sb.AppendLine("### Wyrm Pre-Analysis");
                        sb.AppendLine($"- **Summary**: {rec.AnalysisSummary}");
                        sb.AppendLine($"- **Languages**: {string.Join(", ", rec.RecommendedLanguages)}");
                        sb.AppendLine($"- **Complexity**: {rec.Complexity}");
                        sb.AppendLine($"- **Constraints**: {rec.Constraints.Count}");
                        sb.AppendLine($"- **Out of Scope**: {rec.OutOfScope.Count}");
                        sb.AppendLine();
                    }
                }
                catch { sb.AppendLine("### Wyrm Pre-Analysis\n⚠️ Could not parse\n"); }
            }
            else
            {
                sb.AppendLine("### Wyrm Pre-Analysis\n*Not yet available*\n");
            }

            // Wyvern summary
            var wyvernPath = Path.Combine(folder, "analysis.json");
            if (File.Exists(wyvernPath))
            {
                try
                {
                    var json = File.ReadAllText(wyvernPath);
                    var analysis = JsonSerializer.Deserialize<WyvernAnalysisView>(json, _jsonOptions);
                    if (analysis != null)
                    {
                        var areaCount = analysis.Areas?.Count ?? 0;
                        sb.AppendLine("### Wyvern Task Analysis");
                        sb.AppendLine($"- **Total Tasks**: {analysis.TotalTasks}");
                        sb.AppendLine($"- **Areas**: {areaCount}");
                        sb.AppendLine($"- **Complexity**: {analysis.EstimatedComplexity}");
                        sb.AppendLine($"- **Constraints**: {analysis.Constraints?.Count ?? 0}");
                        sb.AppendLine($"- **Requirements Mapped**: {analysis.RequirementsCoverage?.Count ?? 0}");
                        sb.AppendLine();
                    }
                }
                catch { sb.AppendLine("### Wyvern Task Analysis\n⚠️ Could not parse\n"); }
            }
            else
            {
                sb.AppendLine("### Wyvern Task Analysis\n*Not yet available*\n");
            }

            sb.AppendLine("Use action:'wyrm' or action:'wyvern' for full details.");

            return sb.ToString();
        }

        // Lightweight view models for deserialization (avoid tight coupling to orchestrator models)

        private class WyvernAnalysisView
        {
            public string? ProjectName { get; set; }
            public List<string>? Constraints { get; set; }
            public List<string>? OutOfScope { get; set; }
            public WyvernStructureView? Structure { get; set; }
            public List<WyvernAreaView>? Areas { get; set; }
            public Dictionary<string, string>? RequirementsCoverage { get; set; }
            public int TotalTasks { get; set; }
            public string? EstimatedComplexity { get; set; }
        }

        private class WyvernStructureView
        {
            public Dictionary<string, string>? NamingConventions { get; set; }
            public Dictionary<string, string>? DirectoryPurposes { get; set; }
            public Dictionary<string, string>? FileLocationGuidelines { get; set; }
        }

        private class WyvernAreaView
        {
            public string Name { get; set; } = "";
            public List<WyvernTaskView>? Tasks { get; set; }
        }

        private class WyvernTaskView
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string? AgentType { get; set; }
            public string? Priority { get; set; }
            public List<string>? Dependencies { get; set; }
        }
    }
}
