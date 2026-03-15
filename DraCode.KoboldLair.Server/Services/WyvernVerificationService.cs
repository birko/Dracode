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
                    var check = await ExecuteVerificationCheckAsync(project, step, stoppingToken);
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

        private async Task<VerificationCheck> ExecuteVerificationCheckAsync(
            Project project,
            VerificationStepDefinition step,
            CancellationToken stoppingToken)
        {
            var check = new VerificationCheck
            {
                CheckType = step.CheckType,
                Command = step.Command,
                Priority = Enum.TryParse<VerificationCheckPriority>(step.Priority, out var priority)
                    ? priority
                    : VerificationCheckPriority.Medium,
                ExecutedAt = DateTime.UtcNow
            };

            var workingDir = string.IsNullOrEmpty(step.WorkingDirectory)
                ? project.Paths.Output
                : Path.Combine(project.Paths.Output, step.WorkingDirectory);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var timeout = step.TimeoutSeconds > 0 ? step.TimeoutSeconds : _defaultTimeout;
                var (exitCode, output) = await ExecuteCommandAsync(step.Command, workingDir, timeout, stoppingToken);

                stopwatch.Stop();
                check.ExitCode = exitCode;
                check.Output = output;
                check.DurationSeconds = stopwatch.Elapsed.TotalSeconds;

                // Evaluate success criteria
                check.Passed = EvaluateSuccessCriteria(step.SuccessCriteria, exitCode, output);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                check.ExitCode = -1;
                check.Output = $"Error executing check: {ex.Message}";
                check.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
                check.Passed = false;
            }

            return check;
        }

        private async Task<(int exitCode, string output)> ExecuteCommandAsync(
            string command,
            string workingDir,
            int timeoutSeconds,
            CancellationToken stoppingToken)
        {
            string fileName;
            string arguments;
            if (OperatingSystem.IsWindows())
            {
                fileName = "powershell.exe";
                arguments = $"-NoProfile -Command \"{command}\"";
            }
            else
            {
                fileName = "/bin/bash";
                arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new System.Text.StringBuilder();
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    outputBuilder.AppendLine($"\n[TIMEOUT: Process killed after {timeoutSeconds}s]");
                }
            }

            return (process.ExitCode, outputBuilder.ToString());
        }

        private bool EvaluateSuccessCriteria(string criteria, int exitCode, string output)
        {
            if (criteria == "exit_code_0")
                return exitCode == 0;

            if (criteria.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
            {
                var expectedText = criteria.Substring("contains:".Length);
                return output.Contains(expectedText, StringComparison.OrdinalIgnoreCase);
            }

            if (criteria.StartsWith("not_contains:", StringComparison.OrdinalIgnoreCase))
            {
                var unexpectedText = criteria.Substring("not_contains:".Length);
                return !output.Contains(unexpectedText, StringComparison.OrdinalIgnoreCase);
            }

            // Default: exit code 0
            return exitCode == 0;
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
            var workspaceFiles = Directory.Exists(project.Paths.Output)
                ? Directory.GetFiles(project.Paths.Output, "*", SearchOption.TopDirectoryOnly)
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
            // Create or update verification-fixes task file
            var tasksDir = Path.Combine(project.Paths.Output, "tasks");
            Directory.CreateDirectory(tasksDir);
            var taskFilePath = Path.Combine(tasksDir, "verification-fixes-tasks.md");

            var taskLines = new List<string>();
            var existingCheckTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add header if file doesn't exist
            if (!File.Exists(taskFilePath))
            {
                taskLines.Add("# Verification Fixes");
                taskLines.Add("");
                taskLines.Add("Tasks to fix issues found during project verification.");
                taskLines.Add("");
                taskLines.Add("| ID | Task | Agent | Status |");
                taskLines.Add("|----|------|-------|--------|");
            }
            else
            {
                // Read existing content and track existing check types to prevent duplicates
                var existingLines = await File.ReadAllLinesAsync(taskFilePath, stoppingToken);
                taskLines.AddRange(existingLines);
                foreach (var line in existingLines)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"verify-(\w+)-");
                    if (match.Success)
                        existingCheckTypes.Add(match.Groups[1].Value);
                }
            }

            // Add new tasks, skipping duplicates by check type
            foreach (var check in failedChecks)
            {
                if (existingCheckTypes.Contains(check.CheckType))
                {
                    _logger.LogDebug("Skipping duplicate fix task for {CheckType} in {Project}", check.CheckType, project.Name);
                    continue;
                }

                var taskId = $"verify-{check.CheckType}-{Guid.NewGuid().ToString()[..8]}";
                var priority = check.Priority switch
                {
                    VerificationCheckPriority.Critical => "Critical",
                    VerificationCheckPriority.High => "High",
                    VerificationCheckPriority.Medium => "Normal",
                    _ => "Low"
                };

                var taskDescription = $"[id:{taskId}] [priority:{priority}] Fix {check.CheckType} failure: {check.Command}. Error: {(check.Output.Length > 200 ? check.Output.Substring(0, 200) + "..." : check.Output)}";

                taskLines.Add($"| {taskId} | {taskDescription} | unassigned | unassigned |");
                existingCheckTypes.Add(check.CheckType);
                _logger.LogInformation("Created fix task {TaskId} for {CheckType} failure in {Project}", taskId, check.CheckType, project.Name);
            }

            // Write updated file
            await File.WriteAllLinesAsync(taskFilePath, taskLines, stoppingToken);

            // Register task file with project if not already registered
            if (!project.Paths.TaskFiles.ContainsKey("verification-fixes"))
            {
                project.Paths.TaskFiles["verification-fixes"] = taskFilePath;
                _projectService.UpdateProject(project);
            }
        }
    }
}
