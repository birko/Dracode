using System.Text;
using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for adding existing projects from disk.
    /// Scans a directory, analyzes its structure, and optionally registers it as a project.
    /// </summary>
    public class AddExistingProjectTool : Tool
    {
        private readonly Func<string, string, string?>? _registerExistingProject;

        /// <summary>
        /// File extensions mapped to their technology/language
        /// </summary>
        private static readonly Dictionary<string, string> TechnologyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // .NET
            { ".cs", "C#" },
            { ".csproj", "C# Project" },
            { ".sln", ".NET Solution" },
            { ".slnx", ".NET Solution" },
            { ".fs", "F#" },
            { ".vb", "VB.NET" },

            // Web Frontend
            { ".ts", "TypeScript" },
            { ".tsx", "TypeScript/React" },
            { ".js", "JavaScript" },
            { ".jsx", "JavaScript/React" },
            { ".vue", "Vue.js" },
            { ".svelte", "Svelte" },
            { ".html", "HTML" },
            { ".css", "CSS" },
            { ".scss", "SCSS" },
            { ".sass", "Sass" },
            { ".less", "Less" },

            // Backend
            { ".py", "Python" },
            { ".rb", "Ruby" },
            { ".php", "PHP" },
            { ".java", "Java" },
            { ".kt", "Kotlin" },
            { ".go", "Go" },
            { ".rs", "Rust" },
            { ".cpp", "C++" },
            { ".c", "C" },
            { ".h", "C/C++ Header" },

            // Config & Data
            { ".json", "JSON" },
            { ".yaml", "YAML" },
            { ".yml", "YAML" },
            { ".xml", "XML" },
            { ".toml", "TOML" },
            { ".ini", "INI" },
            { ".env", "Environment" },

            // Documentation
            { ".md", "Markdown" },
            { ".rst", "reStructuredText" },
            { ".txt", "Text" },

            // Database
            { ".sql", "SQL" },
            { ".prisma", "Prisma" },

            // DevOps
            { ".dockerfile", "Docker" },
            { ".tf", "Terraform" },
            { ".bicep", "Bicep" },
        };

        /// <summary>
        /// Directories to skip during scanning
        /// </summary>
        private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".git", "bin", "obj", ".vs", ".idea", ".vscode",
            "dist", "build", "out", "target", "__pycache__", ".cache",
            "packages", "vendor", ".next", ".nuxt", "coverage", ".nyc_output"
        };

        public AddExistingProjectTool(Func<string, string, string?>? registerExistingProject = null)
        {
            _registerExistingProject = registerExistingProject;
        }

        public override string Name => "add_existing_project";

        public override string Description =>
            "Scans an existing project directory on the user's machine to analyze its structure, technologies, and files. " +
            "Use 'scan' action to analyze a directory, then 'register' action to add it as a project for future specifications.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'scan' to analyze a directory, 'register' to add it as a project",
                    @enum = new[] { "scan", "register" }
                },
                path = new
                {
                    type = "string",
                    description = "Absolute path to the existing project directory"
                },
                name = new
                {
                    type = "string",
                    description = "Project name (required for register action, optional for scan - defaults to folder name)"
                }
            },
            required = new[] { "action", "path" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("action", out var actionObj))
            {
                return "Error: action is required";
            }

            if (!input.TryGetValue("path", out var pathObj))
            {
                return "Error: path is required";
            }

            var action = actionObj.ToString()?.ToLower();
            var path = pathObj.ToString() ?? "";

            // Normalize the path
            path = Path.GetFullPath(path);

            switch (action)
            {
                case "scan":
                    return ScanDirectory(path, input);
                case "register":
                    return RegisterProject(path, input);
                default:
                    return $"Error: Unknown action '{action}'";
            }
        }

        private string ScanDirectory(string path, Dictionary<string, object> input)
        {
            if (!Directory.Exists(path))
            {
                return $"Error: Directory does not exist: {path}";
            }

            var result = new StringBuilder();
            var projectName = input.TryGetValue("name", out var nameObj)
                ? nameObj.ToString()
                : Path.GetFileName(path);

            result.AppendLine($"# Project Scan: {projectName}");
            result.AppendLine($"**Location:** `{path}`");
            result.AppendLine();

            // Analyze the project
            var analysis = AnalyzeDirectory(path);

            // Summary
            result.AppendLine("## Summary");
            result.AppendLine($"- **Total Files:** {analysis.TotalFiles}");
            result.AppendLine($"- **Total Directories:** {analysis.TotalDirectories}");
            result.AppendLine($"- **Total Size:** {FormatFileSize(analysis.TotalSize)}");
            result.AppendLine();

            // Technologies detected
            if (analysis.Technologies.Count > 0)
            {
                result.AppendLine("## Technologies Detected");
                foreach (var tech in analysis.Technologies.OrderByDescending(t => t.Value.FileCount))
                {
                    result.AppendLine($"- **{tech.Key}**: {tech.Value.FileCount} files ({FormatFileSize(tech.Value.TotalSize)})");
                }
                result.AppendLine();
            }

            // Project type indicators
            if (analysis.ProjectIndicators.Count > 0)
            {
                result.AppendLine("## Project Type Indicators");
                foreach (var indicator in analysis.ProjectIndicators)
                {
                    result.AppendLine($"- {indicator}");
                }
                result.AppendLine();
            }

            // Key files found
            if (analysis.KeyFiles.Count > 0)
            {
                result.AppendLine("## Key Files Found");
                foreach (var file in analysis.KeyFiles.Take(20))
                {
                    result.AppendLine($"- `{file}`");
                }
                if (analysis.KeyFiles.Count > 20)
                {
                    result.AppendLine($"- ... and {analysis.KeyFiles.Count - 20} more");
                }
                result.AppendLine();
            }

            // Directory structure (top level)
            result.AppendLine("## Top-Level Structure");
            var topDirs = Directory.GetDirectories(path)
                .Select(d => Path.GetFileName(d))
                .Where(d => !SkipDirectories.Contains(d))
                .Take(15)
                .ToList();

            var topFiles = Directory.GetFiles(path)
                .Select(f => Path.GetFileName(f))
                .Take(10)
                .ToList();

            foreach (var dir in topDirs)
            {
                result.AppendLine($"- 📁 {dir}/");
            }
            foreach (var file in topFiles)
            {
                result.AppendLine($"- 📄 {file}");
            }
            result.AppendLine();

            result.AppendLine("---");
            result.AppendLine("To add this project, use the 'register' action with this path.");

            return result.ToString();
        }

        private string RegisterProject(string path, Dictionary<string, object> input)
        {
            if (!Directory.Exists(path))
            {
                return $"Error: Directory does not exist: {path}";
            }

            if (_registerExistingProject == null)
            {
                return "Error: Project registration is not available. ProjectService not configured.";
            }

            var projectName = input.TryGetValue("name", out var nameObj)
                ? nameObj.ToString() ?? Path.GetFileName(path)
                : Path.GetFileName(path);

            try
            {
                var projectId = _registerExistingProject(projectName, path);

                if (projectId == null)
                {
                    return $"Error: Failed to register project '{projectName}'";
                }

                SendMessage("success", $"Project registered: {projectName}");

                return $@"✅ **Project '{projectName}' registered successfully!**

**Project ID:** {projectId}
**Source Path:** `{path}`
**Status:** Prototype (awaiting specification)

The project has been added to KoboldLair. You can now:
1. Create a specification for this project using `manage_specification` with action:'create'
2. The specification should analyze and document the existing codebase
3. Add features for planned improvements or new functionality

Would you like me to help create a specification based on this existing codebase?";
            }
            catch (Exception ex)
            {
                return $"Error registering project: {ex.Message}";
            }
        }

        private ProjectAnalysis AnalyzeDirectory(string path)
        {
            var analysis = new ProjectAnalysis();
            AnalyzeDirectoryRecursive(path, analysis, 0);
            DetectProjectIndicators(path, analysis);
            return analysis;
        }

        private void AnalyzeDirectoryRecursive(string path, ProjectAnalysis analysis, int depth)
        {
            // Limit recursion depth
            if (depth > 10) return;

            try
            {
                // Count this directory
                analysis.TotalDirectories++;

                // Process files in this directory
                foreach (var file in Directory.GetFiles(path))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var ext = fileInfo.Extension.ToLowerInvariant();

                        analysis.TotalFiles++;
                        analysis.TotalSize += fileInfo.Length;

                        // Categorize by technology
                        if (TechnologyMap.TryGetValue(ext, out var technology))
                        {
                            if (!analysis.Technologies.ContainsKey(technology))
                            {
                                analysis.Technologies[technology] = new TechnologyStats();
                            }
                            analysis.Technologies[technology].FileCount++;
                            analysis.Technologies[technology].TotalSize += fileInfo.Length;
                        }

                        // Track key files
                        var fileName = fileInfo.Name.ToLowerInvariant();
                        if (IsKeyFile(fileName, ext))
                        {
                            analysis.KeyFiles.Add(Path.GetRelativePath(path, file));
                        }
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }

                // Recurse into subdirectories
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var dirName = Path.GetFileName(dir);
                    if (!SkipDirectories.Contains(dirName))
                    {
                        AnalyzeDirectoryRecursive(dir, analysis, depth + 1);
                    }
                }
            }
            catch
            {
                // Skip directories we can't access
            }
        }

        private bool IsKeyFile(string fileName, string ext)
        {
            // Configuration files
            if (fileName is "package.json" or "tsconfig.json" or "webpack.config.js" or
                "vite.config.ts" or "vite.config.js" or ".env" or ".env.example" or
                "docker-compose.yml" or "dockerfile" or "makefile" or "cmakelists.txt")
                return true;

            // .NET files
            if (ext is ".csproj" or ".sln" or ".slnx" or ".fsproj")
                return true;

            // Build/Config at root
            if (fileName is "readme.md" or "readme.txt" or "license" or "license.md" or
                "changelog.md" or ".gitignore" or ".editorconfig")
                return true;

            // Python
            if (fileName is "requirements.txt" or "setup.py" or "pyproject.toml" or "pipfile")
                return true;

            // Ruby
            if (fileName is "gemfile" or "rakefile")
                return true;

            // Java/Gradle
            if (fileName is "pom.xml" or "build.gradle" or "build.gradle.kts")
                return true;

            // Rust
            if (fileName is "cargo.toml")
                return true;

            // Go
            if (fileName is "go.mod" or "go.sum")
                return true;

            return false;
        }

        private void DetectProjectIndicators(string path, ProjectAnalysis analysis)
        {
            // Check for specific project types
            if (File.Exists(Path.Combine(path, "package.json")))
            {
                analysis.ProjectIndicators.Add("Node.js/npm project (package.json found)");

                // Try to read package.json for more info
                try
                {
                    var packageJson = File.ReadAllText(Path.Combine(path, "package.json"));
                    if (packageJson.Contains("\"react\""))
                        analysis.ProjectIndicators.Add("React framework detected");
                    if (packageJson.Contains("\"vue\""))
                        analysis.ProjectIndicators.Add("Vue.js framework detected");
                    if (packageJson.Contains("\"angular\""))
                        analysis.ProjectIndicators.Add("Angular framework detected");
                    if (packageJson.Contains("\"next\""))
                        analysis.ProjectIndicators.Add("Next.js framework detected");
                    if (packageJson.Contains("\"express\""))
                        analysis.ProjectIndicators.Add("Express.js backend detected");
                }
                catch { }
            }

            if (Directory.GetFiles(path, "*.sln").Length > 0 ||
                Directory.GetFiles(path, "*.slnx").Length > 0)
            {
                analysis.ProjectIndicators.Add(".NET Solution found");
            }

            if (Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories).Length > 0)
            {
                analysis.ProjectIndicators.Add("C# project(s) detected");
            }

            if (File.Exists(Path.Combine(path, "requirements.txt")) ||
                File.Exists(Path.Combine(path, "pyproject.toml")))
            {
                analysis.ProjectIndicators.Add("Python project detected");
            }

            if (File.Exists(Path.Combine(path, "Cargo.toml")))
            {
                analysis.ProjectIndicators.Add("Rust project detected");
            }

            if (File.Exists(Path.Combine(path, "go.mod")))
            {
                analysis.ProjectIndicators.Add("Go module detected");
            }

            if (File.Exists(Path.Combine(path, "pom.xml")))
            {
                analysis.ProjectIndicators.Add("Maven/Java project detected");
            }

            if (File.Exists(Path.Combine(path, "build.gradle")) ||
                File.Exists(Path.Combine(path, "build.gradle.kts")))
            {
                analysis.ProjectIndicators.Add("Gradle project detected");
            }

            if (File.Exists(Path.Combine(path, "docker-compose.yml")) ||
                File.Exists(Path.Combine(path, "docker-compose.yaml")))
            {
                analysis.ProjectIndicators.Add("Docker Compose configuration found");
            }

            if (File.Exists(Path.Combine(path, "Dockerfile")))
            {
                analysis.ProjectIndicators.Add("Dockerfile found");
            }

            // Check for CI/CD
            if (Directory.Exists(Path.Combine(path, ".github", "workflows")))
            {
                analysis.ProjectIndicators.Add("GitHub Actions CI/CD configured");
            }

            if (File.Exists(Path.Combine(path, ".gitlab-ci.yml")))
            {
                analysis.ProjectIndicators.Add("GitLab CI/CD configured");
            }

            if (File.Exists(Path.Combine(path, "azure-pipelines.yml")))
            {
                analysis.ProjectIndicators.Add("Azure Pipelines configured");
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private class ProjectAnalysis
        {
            public int TotalFiles { get; set; }
            public int TotalDirectories { get; set; }
            public long TotalSize { get; set; }
            public Dictionary<string, TechnologyStats> Technologies { get; } = new();
            public List<string> ProjectIndicators { get; } = new();
            public List<string> KeyFiles { get; } = new();
        }

        private class TechnologyStats
        {
            public int FileCount { get; set; }
            public long TotalSize { get; set; }
        }
    }
}
