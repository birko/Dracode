using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using DraCode.KoboldLair.Factories;
using DraCode.Agent;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services
{
    public class WyrmProcessingService : BackgroundService
    {
        private readonly ILogger<WyrmProcessingService> _logger;
        private readonly ProjectService _projectService;
        private readonly WyrmFactory _wyrmFactory;
        private readonly SharedPlanningContextService? _sharedPlanningContext;
        private readonly TimeSpan _checkInterval;
        private bool _isRunning;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 5;

        public WyrmProcessingService(
            ILogger<WyrmProcessingService> logger,
            ProjectService projectService,
            WyrmFactory wyrmFactory,
            SharedPlanningContextService? sharedPlanningContext = null,
            int checkIntervalSeconds = 60)
        {
            _logger = logger;
            _projectService = projectService;
            _wyrmFactory = wyrmFactory;
            _sharedPlanningContext = sharedPlanningContext;
            _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
            _isRunning = false;
            _projectThrottle = new SemaphoreSlim(MaxConcurrentProjects, MaxConcurrentProjects);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Wyrm Processing Service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                    bool canRun;
                    lock (_lock) { canRun = !_isRunning; if (canRun) _isRunning = true; }
                    if (!canRun) continue;
                    await ProcessProjectsAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Error in Wyrm processing"); }
                finally { lock (_lock) { _isRunning = false; } }
            }
        }

        private async Task ProcessProjectsAsync(CancellationToken stoppingToken)
        {
            var projects = _projectService.GetProjectsByStatus(ProjectStatus.New);
            if (!projects.Any()) return;
            await Task.WhenAll(projects.Select(p => RunWyrmAsync(p, stoppingToken)));
        }

        private async Task RunWyrmAsync(Project project, CancellationToken stoppingToken)
        {
            await _projectThrottle.WaitAsync(stoppingToken);
            try
            {
                _logger.LogInformation("Starting Wyrm analysis: {Name}", project.Name);
                var wyrm = _wyrmFactory.CreateWyrm(project);
                var spec = File.Exists(project.Paths.Specification)
                    ? await File.ReadAllTextAsync(project.Paths.Specification, stoppingToken)
                    : "";

                // GAP 2 FIX: Build cross-project insights context
                var crossProjectContext = await BuildCrossProjectContextAsync(project.Id);

                var prompt = $@"Analyze the following project specification and provide initial recommendations as JSON:

{spec}
{crossProjectContext}
Provide JSON with: RecommendedLanguages[], RecommendedAgentTypes{{}}, TechnicalStack[], SuggestedAreas[], Complexity, AnalysisSummary";
                
                var messages = await wyrm.RunAsync(prompt);
                var lastMessage = messages.LastOrDefault();
                
                // Try to parse Wyrm's response
                WyrmRecommendation? rec = null;
                try
                {
                    // Extract text from content object
                    var response = ExtractTextFromContent(lastMessage?.Content);
                    
                    // Extract JSON from response (handles markdown code blocks)
                    var jsonText = ExtractJson(response);
                    rec = JsonSerializer.Deserialize<WyrmRecommendation>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (rec != null)
                    {
                        rec.ProjectId = project.Id;
                        rec.ProjectName = project.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Wyrm response for {Name}, using fallback", project.Name);
                }
                
                // Fallback to default values if parsing failed
                rec ??= new WyrmRecommendation { 
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    RecommendedLanguages = new List<string> { "general" }, 
                    RecommendedAgentTypes = new Dictionary<string, string> { { "general", "coding" } }, 
                    TechnicalStack = new List<string>(), 
                    SuggestedAreas = new List<string> { "general" }, 
                    Complexity = "Medium" 
                };
                
                var path = Path.Combine(project.Paths.Output, "wyrm-recommendation.json");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, new JsonSerializerOptions { 
                    WriteIndented = true, 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                }), stoppingToken);
                
                _projectService.UpdateProjectStatus(project.Id, ProjectStatus.WyrmAssigned);
                _logger.LogInformation("Wyrm analysis complete: {Name}", project.Name);
            }
            catch (Exception ex) { _logger.LogError(ex, "Wyrm analysis failed: {Name}", project.Name); }
            finally { _projectThrottle.Release(); }
        }

        private static string ExtractTextFromContent(object? content)
        {
            if (content == null) return "";
            if (content is string text) return text;
            
            // Handle ContentBlock
            if (content is Agent.ContentBlock block)
                return block.Text ?? "";
            
            // Handle List<ContentBlock>
            if (content is IEnumerable<Agent.ContentBlock> contentBlocks)
            {
                return string.Join("\n", contentBlocks
                    .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                    .Select(b => b.Text));
            }
            
            // Handle JsonElement (for serialized content)
            if (content is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var texts = new List<string>();
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeEl) &&
                            typeEl.GetString() == "text" &&
                            element.TryGetProperty("text", out var textEl))
                        {
                            var t = textEl.GetString();
                            if (!string.IsNullOrEmpty(t))
                                texts.Add(t);
                        }
                    }
                    return string.Join("\n", texts);
                }
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return jsonElement.GetString() ?? "";
                }
            }
            
            return content.ToString() ?? "";
        }

        /// <summary>
        /// Builds cross-project learning context for Wyrm analysis (GAP 2 FIX)
        /// </summary>
        private async Task<string> BuildCrossProjectContextAsync(string projectId)
        {
            if (_sharedPlanningContext == null)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();

            try
            {
                // Get insights from other successful projects
                var codingInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "coding", 5);
                var csharpInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "csharp", 3);
                var jsInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "javascript", 3);
                var pythonInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "python", 3);

                var allInsights = codingInsights
                    .Concat(csharpInsights)
                    .Concat(jsInsights)
                    .Concat(pythonInsights)
                    .GroupBy(i => i.AgentType)
                    .ToList();

                if (!allInsights.Any())
                {
                    return string.Empty;
                }

                sb.AppendLine();
                sb.AppendLine("## Cross-Project Learning Context");
                sb.AppendLine();
                sb.AppendLine("Based on previous successful projects, here are patterns to consider:");
                sb.AppendLine();

                foreach (var group in allInsights)
                {
                    var agentType = group.Key;
                    var insights = group.ToList();
                    var avgSteps = insights.Average(i => i.StepCount);
                    var avgDuration = insights.Average(i => i.DurationSeconds);
                    var successRate = insights.Count(i => i.Success) / (double)insights.Count * 100;

                    sb.AppendLine($"**{agentType} tasks**: {insights.Count} completed, avg {avgSteps:F1} steps, {avgDuration:F0}s duration, {successRate:F0}% success rate");
                }

                sb.AppendLine();
                sb.AppendLine("Consider these patterns when recommending agent types and estimating complexity.");
                sb.AppendLine();

                _logger.LogDebug("Added cross-project context with {Count} insight groups for project {Name}",
                    allInsights.Count, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build cross-project context, proceeding without it");
            }

            return sb.ToString();
        }

        private static string ExtractJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Wyrm returned empty response.");

            // If it already starts with '{', extract balanced JSON
            var trimmed = content.Trim();
            if (trimmed.StartsWith('{'))
                return ExtractBalancedJson(trimmed, 0);

            // Try to extract from markdown code block (```json ... ``` or ``` ... ```)
            var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(
                content,
                @"```(?:json)?\s*\n?([\s\S]*?)\n?```",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (codeBlockMatch.Success)
            {
                var blockContent = codeBlockMatch.Groups[1].Value.Trim();
                if (blockContent.StartsWith('{'))
                    return ExtractBalancedJson(blockContent, 0);
            }

            // Try to find JSON object anywhere in the text using bracket matching
            var startIdx = content.IndexOf('{');
            if (startIdx >= 0)
                return ExtractBalancedJson(content, startIdx);

            throw new InvalidOperationException("Could not extract JSON from Wyrm response.");
        }

        /// <summary>
        /// Extracts a balanced JSON object by counting braces.
        /// Handles nested objects and arrays correctly.
        /// </summary>
        private static string ExtractBalancedJson(string content, int startIndex)
        {
            if (startIndex >= content.Length || content[startIndex] != '{')
                throw new InvalidOperationException("Could not extract JSON from Wyrm response.");

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = startIndex; i < content.Length; i++)
            {
                char c = content[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return content.Substring(startIndex, i - startIndex + 1);
                }
            }

            throw new InvalidOperationException("Could not extract JSON from Wyrm response - unbalanced braces.");
        }
    }
}
