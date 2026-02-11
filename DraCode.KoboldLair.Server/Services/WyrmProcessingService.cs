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
        private readonly TimeSpan _checkInterval;
        private bool _isRunning;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 5;

        public WyrmProcessingService(
            ILogger<WyrmProcessingService> logger,
            ProjectService projectService,
            WyrmFactory wyrmFactory,
            int checkIntervalSeconds = 60)
        {
            _logger = logger;
            _projectService = projectService;
            _wyrmFactory = wyrmFactory;
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
                
                var prompt = $@"Analyze the following project specification and provide initial recommendations as JSON:
{spec}

Provide JSON with: RecommendedLanguages[], RecommendedAgentTypes{{}}, TechnicalStack[], SuggestedAreas[], Complexity";
                
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

        private static string ExtractJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Wyrm returned empty response.");

            // If it already starts with '{', assume it's valid JSON
            var trimmed = content.Trim();
            if (trimmed.StartsWith('{'))
                return trimmed;

            // Try to extract from markdown code block (```json ... ``` or ``` ... ```)
            var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(
                content,
                @"```(?:json)?\s*\n?(\{[\s\S]*?\})\s*\n?```",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (jsonBlockMatch.Success)
                return jsonBlockMatch.Groups[1].Value.Trim();

            // Try to find JSON object anywhere in the text
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                content,
                @"\{[\s\S]*?\}",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (jsonMatch.Success)
                return jsonMatch.Value.Trim();

            throw new InvalidOperationException("Could not extract JSON from Wyrm response.");
        }
    }
}
