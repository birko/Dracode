using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DraCode.Agent.Helpers;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Discovers external project references from various project formats.
    /// Supports .sln, .slnx, .csproj/.fsproj, .code-workspace, package.json,
    /// go.work, Cargo.toml, tsconfig.json, and pom.xml.
    /// </summary>
    public class ProjectReferenceDiscoverer
    {
        /// <summary>
        /// Main entry point - discovers references from the best-matching project file.
        /// </summary>
        public static DiscoveryResult DiscoverReferences(string projectPath)
        {
            var result = new DiscoveryResult();
            projectPath = Path.GetFullPath(projectPath);

            var projectFile = FindProjectFile(projectPath);
            if (projectFile == null)
                return result;

            result.PrimaryProjectFile = projectFile;
            var ext = Path.GetExtension(projectFile).ToLowerInvariant();
            var fileName = Path.GetFileName(projectFile).ToLowerInvariant();

            try
            {
                List<ProjectReference> refs = (ext, fileName) switch
                {
                    (".sln", _) => ParseDotNetSolution(projectFile, projectPath),
                    (".slnx", _) => ParseDotNetXmlSolution(projectFile, projectPath),
                    (_, "package.json") => ParseNodeWorkspaces(projectFile, projectPath),
                    (_, "go.work") => ParseGoWorkspace(projectFile, projectPath),
                    (_, "cargo.toml") => ParseCargoWorkspace(projectFile, projectPath),
                    (_, "pom.xml") => ParseMavenModules(projectFile, projectPath),
                    (_, "tsconfig.json") => ParseTsConfigReferences(projectFile, projectPath),
                    (".code-workspace", _) => ParseVsCodeWorkspace(projectFile, projectPath),
                    (".csproj" or ".fsproj", _) => ParseCsprojReferences(projectFile, projectPath),
                    _ => new List<ProjectReference>()
                };

                // Determine project type
                result.ProjectType = ext switch
                {
                    ".sln" or ".slnx" or ".csproj" or ".fsproj" => "dotnet",
                    ".code-workspace" => "vscode",
                    _ => fileName switch
                    {
                        "package.json" => "node",
                        "go.work" => "go",
                        "cargo.toml" => "rust",
                        "pom.xml" => "java",
                        "tsconfig.json" => "typescript",
                        _ => "unknown"
                    }
                };

                // For .sln/.slnx, also discover .csproj references for each project
                if (ext is ".sln" or ".slnx")
                {
                    var additionalRefs = new List<ProjectReference>();
                    foreach (var projRef in refs.Where(r => r.AbsolutePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                                            r.AbsolutePath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (File.Exists(projRef.AbsolutePath))
                        {
                            var csprojRefs = ParseCsprojReferences(projRef.AbsolutePath, projectPath);
                            additionalRefs.AddRange(csprojRefs);
                        }
                    }

                    // Add unique additional references
                    var existingPaths = new HashSet<string>(refs.Select(r => r.AbsolutePath), StringComparer.OrdinalIgnoreCase);
                    foreach (var addRef in additionalRefs)
                    {
                        if (!existingPaths.Contains(addRef.AbsolutePath))
                        {
                            refs.Add(addRef);
                            existingPaths.Add(addRef.AbsolutePath);
                        }
                    }
                }

                result.References = refs;

                // Compute external directories
                var externalDirs = refs
                    .Where(r => r.IsExternal)
                    .Select(r => r.DirectoryPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                result.ExternalDirectories = externalDirs;
            }
            catch
            {
                // Best-effort - return what we have
            }

            return result;
        }

        /// <summary>
        /// Finds the primary project/solution file in a directory.
        /// Priority: .sln/.slnx > .code-workspace > package.json > go.work > Cargo.toml > pom.xml > tsconfig.json > .csproj/.fsproj
        /// </summary>
        public static string? FindProjectFile(string directory)
        {
            if (!Directory.Exists(directory))
                return null;

            // .sln / .slnx (highest priority)
            var slnFiles = Directory.GetFiles(directory, "*.sln");
            if (slnFiles.Length > 0) return slnFiles[0];

            var slnxFiles = Directory.GetFiles(directory, "*.slnx");
            if (slnxFiles.Length > 0) return slnxFiles[0];

            // .code-workspace
            var wsFiles = Directory.GetFiles(directory, "*.code-workspace");
            if (wsFiles.Length > 0) return wsFiles[0];

            // package.json (with workspaces)
            var pkgJson = Path.Combine(directory, "package.json");
            if (File.Exists(pkgJson))
            {
                try
                {
                    var content = File.ReadAllText(pkgJson);
                    if (content.Contains("\"workspaces\""))
                        return pkgJson;
                }
                catch { }
            }

            // go.work
            var goWork = Path.Combine(directory, "go.work");
            if (File.Exists(goWork)) return goWork;

            // Cargo.toml (with workspace)
            var cargoToml = Path.Combine(directory, "Cargo.toml");
            if (File.Exists(cargoToml))
            {
                try
                {
                    var content = File.ReadAllText(cargoToml);
                    if (content.Contains("[workspace]"))
                        return cargoToml;
                }
                catch { }
            }

            // pom.xml (with modules)
            var pomXml = Path.Combine(directory, "pom.xml");
            if (File.Exists(pomXml))
            {
                try
                {
                    var content = File.ReadAllText(pomXml);
                    if (content.Contains("<modules>"))
                        return pomXml;
                }
                catch { }
            }

            // tsconfig.json (with references)
            var tsConfig = Path.Combine(directory, "tsconfig.json");
            if (File.Exists(tsConfig))
            {
                try
                {
                    var content = File.ReadAllText(tsConfig);
                    if (content.Contains("\"references\""))
                        return tsConfig;
                }
                catch { }
            }

            // Fall back to .csproj/.fsproj in root
            var csprojFiles = Directory.GetFiles(directory, "*.csproj");
            if (csprojFiles.Length > 0) return csprojFiles[0];

            var fsprojFiles = Directory.GetFiles(directory, "*.fsproj");
            if (fsprojFiles.Length > 0) return fsprojFiles[0];

            return null;
        }

        /// <summary>
        /// Parses .sln files for project references.
        /// </summary>
        private static List<ProjectReference> ParseDotNetSolution(string slnPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var content = File.ReadAllText(slnPath);
                var baseDir = Path.GetDirectoryName(slnPath) ?? rootDir;

                // Regex: Project("{GUID}") = "ProjectName", "RelativePath", "{GUID}"
                var pattern = @"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)""\s*,\s*""([^""]+)""";
                var matches = Regex.Matches(content, pattern);

                foreach (Match match in matches)
                {
                    var name = match.Groups[1].Value;
                    var relativePath = match.Groups[2].Value.Replace('\\', Path.DirectorySeparatorChar);

                    // Skip solution folders (they reference themselves or don't have file extensions)
                    if (!relativePath.Contains('.'))
                        continue;

                    var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                    var dirPath = Path.GetDirectoryName(absolutePath) ?? absolutePath;

                    refs.Add(new ProjectReference
                    {
                        Name = name,
                        AbsolutePath = absolutePath,
                        DirectoryPath = dirPath,
                        IsExternal = !PathHelper.IsUnderDirectory(dirPath, rootDir),
                        SourceFormat = "sln"
                    });
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses .slnx (XML-based solution) files.
        /// </summary>
        private static List<ProjectReference> ParseDotNetXmlSolution(string slnxPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var doc = XDocument.Load(slnxPath);
                var baseDir = Path.GetDirectoryName(slnxPath) ?? rootDir;

                var projects = doc.Descendants("Project")
                    .Where(e => e.Attribute("Path") != null);

                foreach (var proj in projects)
                {
                    var relativePath = proj.Attribute("Path")!.Value.Replace('\\', Path.DirectorySeparatorChar);
                    var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                    var dirPath = Path.GetDirectoryName(absolutePath) ?? absolutePath;
                    var name = Path.GetFileNameWithoutExtension(relativePath);

                    refs.Add(new ProjectReference
                    {
                        Name = name,
                        AbsolutePath = absolutePath,
                        DirectoryPath = dirPath,
                        IsExternal = !PathHelper.IsUnderDirectory(dirPath, rootDir),
                        SourceFormat = "slnx"
                    });
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses .csproj/.fsproj for ProjectReference elements.
        /// </summary>
        private static List<ProjectReference> ParseCsprojReferences(string csprojPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var doc = XDocument.Load(csprojPath);
                var baseDir = Path.GetDirectoryName(csprojPath) ?? rootDir;

                var projectRefs = doc.Descendants()
                    .Where(e => e.Name.LocalName == "ProjectReference" && e.Attribute("Include") != null);

                foreach (var projRef in projectRefs)
                {
                    var relativePath = projRef.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar);
                    var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                    var dirPath = Path.GetDirectoryName(absolutePath) ?? absolutePath;
                    var name = Path.GetFileNameWithoutExtension(relativePath);

                    refs.Add(new ProjectReference
                    {
                        Name = name,
                        AbsolutePath = absolutePath,
                        DirectoryPath = dirPath,
                        IsExternal = !PathHelper.IsUnderDirectory(dirPath, rootDir),
                        SourceFormat = "csproj"
                    });
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses .code-workspace files for folder references.
        /// </summary>
        private static List<ProjectReference> ParseVsCodeWorkspace(string wsPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var content = File.ReadAllText(wsPath);
                var baseDir = Path.GetDirectoryName(wsPath) ?? rootDir;

                using var doc = JsonDocument.Parse(content, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
                if (doc.RootElement.TryGetProperty("folders", out var folders))
                {
                    foreach (var folder in folders.EnumerateArray())
                    {
                        if (folder.TryGetProperty("path", out var pathProp))
                        {
                            var relativePath = pathProp.GetString();
                            if (string.IsNullOrEmpty(relativePath)) continue;

                            var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                            var name = Path.GetFileName(absolutePath);

                            refs.Add(new ProjectReference
                            {
                                Name = name,
                                AbsolutePath = absolutePath,
                                DirectoryPath = absolutePath,
                                IsExternal = !PathHelper.IsUnderDirectory(absolutePath, rootDir),
                                SourceFormat = "code-workspace"
                            });
                        }
                    }
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses package.json workspaces field.
        /// </summary>
        private static List<ProjectReference> ParseNodeWorkspaces(string pkgJsonPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var content = File.ReadAllText(pkgJsonPath);
                var baseDir = Path.GetDirectoryName(pkgJsonPath) ?? rootDir;

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("workspaces", out var workspaces))
                {
                    var patterns = new List<string>();

                    if (workspaces.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ws in workspaces.EnumerateArray())
                        {
                            var pattern = ws.GetString();
                            if (!string.IsNullOrEmpty(pattern))
                                patterns.Add(pattern);
                        }
                    }
                    else if (workspaces.ValueKind == JsonValueKind.Object &&
                             workspaces.TryGetProperty("packages", out var packages))
                    {
                        foreach (var ws in packages.EnumerateArray())
                        {
                            var pattern = ws.GetString();
                            if (!string.IsNullOrEmpty(pattern))
                                patterns.Add(pattern);
                        }
                    }

                    foreach (var pattern in patterns)
                    {
                        // Simple glob resolution: replace * with directory scan
                        var cleanPattern = pattern.TrimEnd('/').TrimEnd('*').TrimEnd('/');
                        var searchDir = Path.GetFullPath(Path.Combine(baseDir, cleanPattern));

                        if (Directory.Exists(searchDir))
                        {
                            // If pattern had wildcard, enumerate subdirectories
                            if (pattern.Contains('*'))
                            {
                                foreach (var subDir in Directory.GetDirectories(searchDir))
                                {
                                    if (File.Exists(Path.Combine(subDir, "package.json")))
                                    {
                                        var name = Path.GetFileName(subDir);
                                        refs.Add(new ProjectReference
                                        {
                                            Name = name,
                                            AbsolutePath = Path.Combine(subDir, "package.json"),
                                            DirectoryPath = subDir,
                                            IsExternal = !PathHelper.IsUnderDirectory(subDir, rootDir),
                                            SourceFormat = "package.json"
                                        });
                                    }
                                }
                            }
                            else
                            {
                                var name = Path.GetFileName(searchDir);
                                refs.Add(new ProjectReference
                                {
                                    Name = name,
                                    AbsolutePath = File.Exists(Path.Combine(searchDir, "package.json"))
                                        ? Path.Combine(searchDir, "package.json")
                                        : searchDir,
                                    DirectoryPath = searchDir,
                                    IsExternal = !PathHelper.IsUnderDirectory(searchDir, rootDir),
                                    SourceFormat = "package.json"
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses go.work files for use directives.
        /// </summary>
        private static List<ProjectReference> ParseGoWorkspace(string goWorkPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var lines = File.ReadAllLines(goWorkPath);
                var baseDir = Path.GetDirectoryName(goWorkPath) ?? rootDir;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("use ") || trimmed.StartsWith("use\t"))
                    {
                        var path = trimmed[4..].Trim().Trim('"');
                        if (string.IsNullOrEmpty(path)) continue;

                        var absolutePath = Path.GetFullPath(Path.Combine(baseDir, path));
                        var name = Path.GetFileName(absolutePath);

                        refs.Add(new ProjectReference
                        {
                            Name = name,
                            AbsolutePath = absolutePath,
                            DirectoryPath = absolutePath,
                            IsExternal = !PathHelper.IsUnderDirectory(absolutePath, rootDir),
                            SourceFormat = "go.work"
                        });
                    }
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses Cargo.toml [workspace] members.
        /// </summary>
        private static List<ProjectReference> ParseCargoWorkspace(string cargoPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var content = File.ReadAllText(cargoPath);
                var baseDir = Path.GetDirectoryName(cargoPath) ?? rootDir;

                // Simple TOML parsing for members = ["path1", "path2"]
                var membersMatch = Regex.Match(content, @"members\s*=\s*\[(.*?)\]", RegexOptions.Singleline);
                if (membersMatch.Success)
                {
                    var membersContent = membersMatch.Groups[1].Value;
                    var pathMatches = Regex.Matches(membersContent, @"""([^""]+)""");

                    foreach (Match pathMatch in pathMatches)
                    {
                        var relativePath = pathMatch.Groups[1].Value;

                        // Handle glob patterns
                        if (relativePath.Contains('*'))
                        {
                            var cleanPath = relativePath.TrimEnd('/').TrimEnd('*').TrimEnd('/');
                            var searchDir = Path.GetFullPath(Path.Combine(baseDir, cleanPath));
                            if (Directory.Exists(searchDir))
                            {
                                foreach (var subDir in Directory.GetDirectories(searchDir))
                                {
                                    if (File.Exists(Path.Combine(subDir, "Cargo.toml")))
                                    {
                                        var name = Path.GetFileName(subDir);
                                        refs.Add(new ProjectReference
                                        {
                                            Name = name,
                                            AbsolutePath = Path.Combine(subDir, "Cargo.toml"),
                                            DirectoryPath = subDir,
                                            IsExternal = !PathHelper.IsUnderDirectory(subDir, rootDir),
                                            SourceFormat = "Cargo.toml"
                                        });
                                    }
                                }
                            }
                        }
                        else
                        {
                            var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                            var name = Path.GetFileName(absolutePath);

                            refs.Add(new ProjectReference
                            {
                                Name = name,
                                AbsolutePath = File.Exists(Path.Combine(absolutePath, "Cargo.toml"))
                                    ? Path.Combine(absolutePath, "Cargo.toml")
                                    : absolutePath,
                                DirectoryPath = absolutePath,
                                IsExternal = !PathHelper.IsUnderDirectory(absolutePath, rootDir),
                                SourceFormat = "Cargo.toml"
                            });
                        }
                    }
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses tsconfig.json references.
        /// </summary>
        private static List<ProjectReference> ParseTsConfigReferences(string tscPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var content = File.ReadAllText(tscPath);
                var baseDir = Path.GetDirectoryName(tscPath) ?? rootDir;

                using var doc = JsonDocument.Parse(content, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
                if (doc.RootElement.TryGetProperty("references", out var references))
                {
                    foreach (var refItem in references.EnumerateArray())
                    {
                        if (refItem.TryGetProperty("path", out var pathProp))
                        {
                            var relativePath = pathProp.GetString();
                            if (string.IsNullOrEmpty(relativePath)) continue;

                            var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                            var name = Path.GetFileName(absolutePath);

                            refs.Add(new ProjectReference
                            {
                                Name = name,
                                AbsolutePath = absolutePath,
                                DirectoryPath = Directory.Exists(absolutePath) ? absolutePath : Path.GetDirectoryName(absolutePath) ?? absolutePath,
                                IsExternal = !PathHelper.IsUnderDirectory(absolutePath, rootDir),
                                SourceFormat = "tsconfig.json"
                            });
                        }
                    }
                }
            }
            catch { }

            return refs;
        }

        /// <summary>
        /// Parses pom.xml module elements.
        /// </summary>
        private static List<ProjectReference> ParseMavenModules(string pomPath, string rootDir)
        {
            var refs = new List<ProjectReference>();
            try
            {
                var doc = XDocument.Load(pomPath);
                var baseDir = Path.GetDirectoryName(pomPath) ?? rootDir;
                var ns = doc.Root?.GetDefaultNamespace();

                var modules = ns != null
                    ? doc.Descendants(ns + "module")
                    : doc.Descendants("module");

                foreach (var module in modules)
                {
                    var relativePath = module.Value.Trim();
                    if (string.IsNullOrEmpty(relativePath)) continue;

                    var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                    var name = Path.GetFileName(absolutePath);

                    refs.Add(new ProjectReference
                    {
                        Name = name,
                        AbsolutePath = absolutePath,
                        DirectoryPath = absolutePath,
                        IsExternal = !PathHelper.IsUnderDirectory(absolutePath, rootDir),
                        SourceFormat = "pom.xml"
                    });
                }
            }
            catch { }

            return refs;
        }
    }

    /// <summary>
    /// Represents a reference to another project.
    /// </summary>
    public class ProjectReference
    {
        public string Name { get; set; } = "";
        public string AbsolutePath { get; set; } = "";
        public string DirectoryPath { get; set; } = "";
        public bool IsExternal { get; set; }
        public string SourceFormat { get; set; } = "";
    }

    /// <summary>
    /// Result of reference discovery.
    /// </summary>
    public class DiscoveryResult
    {
        public List<ProjectReference> References { get; set; } = new();
        public List<string> ExternalDirectories { get; set; } = new();
        public string? PrimaryProjectFile { get; set; }
        public string ProjectType { get; set; } = "";
    }
}
