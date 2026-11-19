using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using System.Text.Json;
using System.Diagnostics;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors projects awaiting verification and runs validation checks.
    /// Automatically creates fix tasks if verification fails.
    /// </summary>
    public class WyvernVerificationService : PeriodicBackgroundService
    {
        private readonly ILogger<WyvernVerificationService> _logger;
        private readonly ProjectService _projectService;
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 3;
        private readonly int _defaultTimeout;
        private readonly bool _autoCreateFixTasks;
        private readonly bool _requireAllChecksPassing;

        protected override ILogger Logger => _logger;

        public WyvernVerificationService(
            ILogger<WyvernVerificationService> logger,
            ProjectService projectService,
            IConfiguration configuration,
            int checkIntervalSeconds = 30)
            : base(TimeSpan.FromSeconds(checkIntervalSeconds))
        {
            _logger = logger;
            _projectService = projectService;
            _projectThrottle = new SemaphoreSlim(MaxConcurrentProjects, MaxConcurrentProjects);

            var verificationConfig = configuration.GetSection("KoboldLair:Verification");
            _defaultTimeout = verificationConfig.GetValue<int>("TimeoutSeconds", 600);
            _autoCreateFixTasks = verificationConfig.GetValue<bool>("AutoCreateFixTasks", true);
            _requireAllChecksPassing = verificationConfig.GetValue<bool>("RequireAllChecksPassing", false);
        }

        protected override async Task ExecuteCycleAsync(CancellationToken stoppingToken)
        {
            var projects = _projectService.GetProjectsByStatus(ProjectStatus.AwaitingVerification)
                .Where(p => p.ExecutionState == ProjectExecutionState.Running)
                .ToList();
            if (!projects.Any()) return;
            await Task.WhenAll(projects.Select(p => RunVerificationAsync(p, stoppingToken)));
        }

        private async Task RunVerificationAsync(Project project, CancellationToken stoppingToken)
        {
            await _projectThrottle.WaitAsync(stoppingToken);
            try
            {
                _logger.LogInformation("Starting verification: {Name}", project.Name);
                
                // Update status
                project.VerificationStatus = VerificationStatus.InProgress;
                project.VerificationStartedAt = DateTime.UtcNow;
                _projectService.UpdateProject(project);

                // Load Wyrm recommendations
                var wyrmRecommendationPath = Path.Combine(project.Paths.Output, "wyrm-recommendation.json");
                WyrmRecommendation? wyrmRec = null;
                if (File.Exists(wyrmRecommendationPath))
                {
                    var json = await File.ReadAllTextAsync(wyrmRecommendationPath, stoppingToken);
                    wyrmRec = JsonSerializer.Deserialize<WyrmRecommendation>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }

                // Get verification steps (from Wyrm or auto-detect)
                var verificationSteps = wyrmRec?.VerificationSteps ?? await AutoDetectVerificationStepsAsync(project, wyrmRec);

                if (!verificationSteps.Any())
                {
                    _logger.LogWarning("No verification steps found for {Name}, skipping verification", project.Name);
                    project.VerificationStatus = VerificationStatus.Skipped;
                    project.VerificationCompletedAt = DateTime.UtcNow;
                    project.VerificationReport = "No verification steps configured.";
                    _projectService.UpdateProjectStatus(project.Id, ProjectStatus.Completed);
                    _logger.LogInformation("Project {Name} completed (verification skipped)", project.Name);
                    return;
                }

                // Execute verification checks
                var checks = new List<VerificationCheck>();
                var reportLines = new List<string>
                {
                    "# Verification Report",
                    $"**Project**: {project.Name}",
                    $"**Started**: {project.VerificationStartedAt:yyyy-MM-dd HH:mm:ss} UTC",
                    "",
                    "## Checks Executed",
                    ""
                };

                foreach (var step in verificationSteps)
                {
                    var check = await VerificationCheckRunner.RunAsync(project, step, _defaultTimeout, stoppingToken);
                    checks.Add(check);

                    reportLines.Add($"### {step.CheckType} - {(check.Passed ? "✅ PASSED" : "❌ FAILED")}");
                    reportLines.Add($"**Command**: `{step.Command}`");
                    reportLines.Add($"**Duration**: {check.DurationSeconds:F2}s");
                    reportLines.Add($"**Exit Code**: {check.ExitCode}");
                    if (!string.IsNullOrEmpty(check.Output))
                    {
                        reportLines.Add("**Output**:");
                        reportLines.Add("```");
                        reportLines.Add(check.Output.Length > 2000 ? check.Output.Substring(0, 2000) + "..." : check.Output);
                        reportLines.Add("```");
                    }
                    reportLines.Add("");
                }

                // Determine overall status
                var criticalFailed = checks.Where(c => c.Priority == VerificationCheckPriority.Critical && !c.Passed).ToList();
                var highFailed = checks.Where(c => c.Priority == VerificationCheckPriority.High && !c.Passed).ToList();
                var anyFailed = checks.Any(c => !c.Passed);

                bool verificationPassed = _requireAllChecksPassing ? !anyFailed : criticalFailed.Count == 0;

                project.VerificationStatus = verificationPassed ? VerificationStatus.Passed : VerificationStatus.Failed;
                project.VerificationCompletedAt = DateTime.UtcNow;
                project.VerificationChecks = checks;

                reportLines.Add("## Summary");
                reportLines.Add($"**Status**: {(verificationPassed ? "✅ PASSED" : "❌ FAILED")}");
                reportLines.Add($"**Total Checks**: {checks.Count}");
                reportLines.Add($"**Passed**: {checks.Count(c => c.Passed)}");
                reportLines.Add($"**Failed**: {checks.Count(c => !c.Passed)}");
                reportLines.Add($"**Critical Failures**: {criticalFailed.Count}");
                reportLines.Add($"**High Priority Failures**: {highFailed.Count}");

                project.VerificationReport = string.Join("\n", reportLines);
                _projectService.UpdateProject(project);

                // Create fix tasks if failures detected and auto-create enabled
                if (!verificationPassed && _autoCreateFixTasks)
                {
                    await CreateFixTasksAsync(project, checks.Where(c => !c.Passed).ToList(), stoppingToken);
                    _projectService.UpdateProjectStatus(project.Id, ProjectStatus.InProgress);
                    _logger.LogInformation("Verification failed for {Name}, created fix tasks", project.Name);
                }
                else
                {
                    _projectService.UpdateProjectStatus(project.Id, ProjectStatus.Completed);
                    _logger.LogInformation("Project {Name} completed (verification {Status})", project.Name, project.VerificationStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification failed: {Name}", project.Name);
                project.VerificationStatus = VerificationStatus.Failed;
                project.VerificationCompletedAt = DateTime.UtcNow;
                project.VerificationReport = $"Verification error: {ex.Message}";
                _projectService.UpdateProject(project);
            }
            finally
            {
                _projectThrottle.Release();
            }
        }

        private static readonly TechStackDetector[] TechStackDetectors =
        [
            new(
                TechStackKeywords: ["NET", "csharp"],
                Languages: ["csharp"],
                FilePatterns: [".csproj", ".sln", ".slnx"],
                Steps:
                [
                    ("build", "dotnet build", "Critical", ".NET build verification"),
                    ("test", "dotnet test", "High", ".NET unit tests")
                ]),
            new(
                TechStackKeywords: ["Node", "npm"],
                Languages: ["javascript", "typescript"],
                FilePatterns: ["package.json"],
                Steps:
                [
                    ("build", "npm run build", "Critical", "Node.js build verification"),
                    ("test", "npm test", "High", "Node.js unit tests")
                ]),
            new(
                TechStackKeywords: [],
                Languages: ["python"],
                FilePatterns: ["requirements.txt", "setup.py", "pyproject.toml"],
                Steps:
                [
                    ("test", "pytest", "High", "Python unit tests")
                ]),
            new(
                TechStackKeywords: ["Go", "golang"],
                Languages: ["go"],
                FilePatterns: ["go.mod", "go.sum"],
                Steps:
                [
                    ("build", "go build ./...", "Critical", "Go build verification"),
                    ("test", "go test ./...", "High", "Go unit tests")
                ]),
            new(
                TechStackKeywords: ["Rust", "cargo"],
                Languages: ["rust"],
                FilePatterns: ["Cargo.toml", "Cargo.lock"],
                Steps:
                [
                    ("build", "cargo build", "Critical", "Rust build verification"),
                    ("test", "cargo test", "High", "Rust unit tests")
                ]),
            new(
                TechStackKeywords: ["Java", "Maven", "Gradle"],
                Languages: ["java"],
                FilePatterns: ["pom.xml", "build.gradle", "build.gradle.kts"],
                Steps:
                [
                    ("build", "mvn compile -q", "Critical", "Java build verification"),
                    ("test", "mvn test -q", "High", "Java unit tests")
                ]),
            new(
                TechStackKeywords: ["C++", "CMake", "cpp"],
                Languages: ["cpp", "c++"],
                FilePatterns: ["CMakeLists.txt", "Makefile"],
                Steps:
                [
                    ("build", "cmake --build . --config Release", "Critical", "C++ build verification")
                ])
        ];

        private Task<List<VerificationStepDefinition>> AutoDetectVerificationStepsAsync(
            Project project,
            WyrmRecommendation? wyrmRec)
        {
            var steps = new List<VerificationStepDefinition>();
            var workspace = ProjectWorkspace.For(project);
            var workspaceFiles = Directory.Exists(workspace)
                ? Directory.GetFiles(workspace, "*", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            var techStack = wyrmRec?.TechnicalStack ?? new List<string>();
            var languages = wyrmRec?.RecommendedLanguages ?? new List<string>();

            foreach (var detector in TechStackDetectors)
            {
                bool matched =
                    detector.TechStackKeywords.Any(kw => techStack.Any(t => t.Contains(kw, StringComparison.OrdinalIgnoreCase))) ||
                    detector.Languages.Any(l => languages.Contains(l)) ||
                    detector.FilePatterns.Any(fp => workspaceFiles.Any(f => f.EndsWith(fp)));

                if (!matched) continue;

                foreach (var (checkType, command, priority, description) in detector.Steps)
                {
                    steps.Add(new VerificationStepDefinition
                    {
                        CheckType = checkType,
                        Command = command,
                        Priority = priority,
                        Description = description,
                        TimeoutSeconds = 600
                    });
                }
            }

            return Task.FromResult(steps);
        }

        private sealed record TechStackDetector(
            string[] TechStackKeywords,
            string[] Languages,
            string[] FilePatterns,
            (string CheckType, string Command, string Priority, string Description)[] Steps);

        private async Task CreateFixTasksAsync(Project project, List<VerificationCheck> failedChecks, CancellationToken stoppingToken)
        {
            var taskFilePath = Path.Combine(project.Paths.Output, "tasks", VerificationFixTasks.FileName);
            await VerificationFixTasks.AddAsync(taskFilePath, failedChecks, _logger);

            if (!project.Paths.TaskFiles.ContainsKey(VerificationFixTasks.AreaName))
            {
                project.Paths.TaskFiles[VerificationFixTasks.AreaName] = taskFilePath;
                _projectService.UpdateProject(project);
            }
        }
    }
}
