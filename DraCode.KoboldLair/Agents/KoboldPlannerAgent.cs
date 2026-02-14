using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// Specialized agent that creates implementation plans for Kobold tasks.
    /// The planner analyzes a task and creates a structured plan with concrete steps
    /// that the Kobold can follow during execution.
    /// </summary>
    public class KoboldPlannerAgent : AgentBase
    {
        public KoboldPlannerAgent(ILlmProvider llmProvider, AgentOptions? options = null)
            : base(llmProvider, options)
        {
        }

        protected override string SystemPrompt => $@"You are an implementation planner for coding tasks. Your job is to analyze a task and create a detailed implementation plan.

Working directory: {WorkingDirectory}

Your role:
1. Understand the task requirements
2. Break the task into concrete, atomic steps
3. Identify which files need to be created or modified
4. Order steps by dependencies (what needs to be done first)
5. Create a plan using the create_implementation_plan tool
6. Follow the project's file structure and organization guidelines

Planning guidelines:

**Step Design:**
- Each step should be self-contained and atomic
- A step should ideally do ONE thing (create one file, implement one function, etc.)
- Steps should be testable - you can verify completion
- Keep steps small enough to be completed in a single iteration
- Avoid steps that are too vague (""implement the feature"")

**File Organization:**
- Identify all files that need to be created
- Identify existing files that need modification
- Consider the project structure and conventions
- Follow naming conventions (PascalCase, camelCase, kebab-case) specified for the project
- Place files in appropriate directories based on file location guidelines
- Don't forget test files if applicable

**Default Web Project Structure (use if no specific guidelines provided):**
- HTML entry point: root folder (index.html only)
- JavaScript/TypeScript: ALWAYS in js/ or src/ folder (e.g., js/app.js, js/utils.js) - NEVER in root
- CSS/Stylesheets: ALWAYS in css/ folder (e.g., css/styles.css) - NEVER in root
- Images/Assets: assets/ or assets/images/ folder
- Components: components/ folder for reusable UI pieces
- CRITICAL: .js, .ts, .css files must NEVER be placed in the root folder

**Dependencies:**
- Order steps so dependencies come first
- If step B depends on step A, A must come before B
- Consider compile-time and runtime dependencies

**Naming:**
- Use clear, descriptive step titles
- Titles should be action-oriented (""Create"", ""Implement"", ""Add"", ""Configure"")

**Example good steps:**
1. ""Create User model class"" - Create Models/User.cs with properties
2. ""Implement UserRepository interface"" - Create IUserRepository.cs
3. ""Implement UserRepository"" - Create UserRepository.cs implementing the interface
4. ""Add dependency injection registration"" - Modify Startup.cs to register services
5. ""Create UserController"" - Create Controllers/UserController.cs with CRUD endpoints

**Example bad steps:**
- ""Do the backend"" (too vague)
- ""Implement everything"" (not atomic)
- ""Create all the files"" (should be separate steps)

After analyzing the task, use the create_implementation_plan tool to output your plan.";

        protected override List<Tool> CreateTools()
        {
            // Only include the plan creation tool - no file operations
            return new List<Tool>
            {
                new CreateImplementationPlanTool()
            };
        }

        /// <summary>
        /// Creates an implementation plan for the given task
        /// </summary>
        /// <param name="taskDescription">The task to plan</param>
        /// <param name="specificationContext">Optional project specification for context</param>
        /// <param name="projectStructure">Optional project structure with file organization guidelines</param>
        /// <param name="workspaceFiles">List of files already in workspace</param>
        /// <param name="filesInUse">Files currently being worked on by other agents</param>
        /// <param name="fileMetadata">Metadata about files including their purposes</param>
        /// <param name="relatedPlans">Related completed plans that touched similar files</param>
        /// <param name="similarTaskInsights">Insights from similar task executions</param>
        /// <param name="bestPractices">Best practices learned for this agent type</param>
        /// <param name="maxIterations">Maximum iterations for plan generation</param>
        /// <returns>The generated implementation plan</returns>
        public async Task<KoboldImplementationPlan> CreatePlanAsync(
            string taskDescription,
            string? specificationContext = null,
            ProjectStructure? projectStructure = null,
            List<string>? workspaceFiles = null,
            HashSet<string>? filesInUse = null,
            Dictionary<string, string>? fileMetadata = null,
            List<KoboldImplementationPlan>? relatedPlans = null,
            List<PlanningInsight>? similarTaskInsights = null,
            Dictionary<string, string>? bestPractices = null,
            int maxIterations = 5)
        {
            // Clear any previous plan
            CreateImplementationPlanTool.ClearLastPlan();

            // Build the prompt
            var prompt = BuildPlanningPrompt(taskDescription, specificationContext, projectStructure, workspaceFiles, filesInUse, fileMetadata, relatedPlans, similarTaskInsights, bestPractices);

            // Run the agent to generate the plan
            await RunAsync(prompt, maxIterations);

            // Retrieve the generated plan
            var plan = CreateImplementationPlanTool.GetLastPlan();

            if (plan == null)
            {
                // Create a fallback single-step plan if no plan was generated
                plan = new KoboldImplementationPlan
                {
                    TaskDescription = taskDescription,
                    Status = PlanStatus.Ready,
                    Steps = new List<ImplementationStep>
                    {
                        new ImplementationStep
                        {
                            Index = 1,
                            Title = "Execute task",
                            Description = taskDescription
                        }
                    }
                };
            }
            else
            {
                plan.TaskDescription = taskDescription;
            }

            plan.Status = PlanStatus.Ready;
            plan.AddLogEntry("Plan created by KoboldPlannerAgent");

            return plan;
        }

        private string BuildPlanningPrompt(
            string taskDescription,
            string? specificationContext,
            ProjectStructure? projectStructure,
            List<string>? workspaceFiles,
            HashSet<string>? filesInUse,
            Dictionary<string, string>? fileMetadata,
            List<KoboldImplementationPlan>? relatedPlans,
            List<PlanningInsight>? similarTaskInsights,
            Dictionary<string, string>? bestPractices)
        {
            var prompt = new System.Text.StringBuilder();
            prompt.AppendLine("Please create an implementation plan for the following task.");
            prompt.AppendLine();

            // Add learning context from past executions (GAP 1 fix)
            if (bestPractices != null && bestPractices.Any())
            {
                prompt.AppendLine("## Learned Best Practices");
                prompt.AppendLine();
                prompt.AppendLine("Based on similar completed tasks, here are learned patterns:");
                prompt.AppendLine();
                foreach (var practice in bestPractices)
                {
                    prompt.AppendLine($"- **{practice.Key}**: {practice.Value}");
                }
                prompt.AppendLine();
                prompt.AppendLine("---");
                prompt.AppendLine();
            }

            // Add insights from similar task executions
            if (similarTaskInsights != null && similarTaskInsights.Any())
            {
                prompt.AppendLine("## Similar Task Insights");
                prompt.AppendLine();
                prompt.AppendLine("Previous similar tasks completed successfully:");
                prompt.AppendLine();
                foreach (var insight in similarTaskInsights.Take(3))
                {
                    var avgIterationsPerStep = insight.StepCount > 0 ? insight.TotalIterations / (double)insight.StepCount : 0;
                    prompt.AppendLine($"- **Task {insight.TaskId[..Math.Min(8, insight.TaskId.Length)]}**: {insight.StepCount} steps, {insight.TotalIterations} iterations ({avgIterationsPerStep:F1} per step), {insight.DurationSeconds:F0}s duration");
                    prompt.AppendLine($"  - Created {insight.FilesCreated} files, modified {insight.FilesModified} files");
                }
                prompt.AppendLine();
                prompt.AppendLine("Use these insights to estimate appropriate step granularity.");
                prompt.AppendLine();
                prompt.AppendLine("---");
                prompt.AppendLine();
            }

            // Add related plans that touched similar files
            if (relatedPlans != null && relatedPlans.Any())
            {
                prompt.AppendLine("## Related Completed Plans");
                prompt.AppendLine();
                prompt.AppendLine("These completed plans worked with related files - learn from their structure:");
                prompt.AppendLine();
                foreach (var plan in relatedPlans.Take(3))
                {
                    prompt.AppendLine($"### Plan: {plan.TaskDescription?.Substring(0, Math.Min(50, plan.TaskDescription?.Length ?? 0)) ?? "Unknown"}...");
                    prompt.AppendLine($"Steps: {plan.Steps.Count} | Status: {plan.Status}");

                    // Show step titles for reference
                    var stepTitles = plan.Steps.Select(s => s.Title).Take(5);
                    prompt.AppendLine("Step overview:");
                    foreach (var title in stepTitles)
                    {
                        prompt.AppendLine($"  - {title}");
                    }
                    if (plan.Steps.Count > 5)
                    {
                        prompt.AppendLine($"  - ... and {plan.Steps.Count - 5} more steps");
                    }
                    prompt.AppendLine();
                }
                prompt.AppendLine("Consider similar step organization if appropriate.");
                prompt.AppendLine();
                prompt.AppendLine("---");
                prompt.AppendLine();
            }

            // Add workspace state - CRITICAL for correct planning
            if (workspaceFiles != null && workspaceFiles.Any())
            {
                prompt.AppendLine("## Workspace State");
                prompt.AppendLine();
                prompt.AppendLine("The following files already exist in the workspace:");
                prompt.AppendLine();

                // Categorize files by extension for better readability
                var filesByCategory = workspaceFiles
                    .GroupBy(f => GetFileCategory(f))
                    .OrderBy(g => g.Key);

                foreach (var category in filesByCategory)
                {
                    prompt.AppendLine($"**{category.Key}:**");
                    foreach (var file in category.OrderBy(f => f))
                    {
                        // Mark files currently in use
                        var inUseMarker = filesInUse != null && filesInUse.Contains(file) ? " ⚠️ (in use)" : "";
                        
                        // Add file purpose if available
                        var purpose = "";
                        if (fileMetadata != null && fileMetadata.TryGetValue(file, out var filePurpose))
                        {
                            purpose = $" - {filePurpose}";
                        }
                        
                        prompt.AppendLine($"- {file}{inUseMarker}{purpose}");
                    }
                    prompt.AppendLine();
                }

                prompt.AppendLine("**CRITICAL**: When creating your plan:");
                prompt.AppendLine("- Files listed above already EXIST - mark them in `filesToModify`, NOT `filesToCreate`");
                prompt.AppendLine("- Only put files that DON'T exist in `filesToCreate`");
                prompt.AppendLine("- For steps that modify existing files, include a note: 'Read file first, then edit'");
                prompt.AppendLine("- Files marked ⚠️ are being worked on by other agents - avoid if possible");
                prompt.AppendLine("- Consider each file's purpose when deciding which to modify");
                prompt.AppendLine();
                prompt.AppendLine("---");
                prompt.AppendLine();
            }
            else
            {
                prompt.AppendLine("## Workspace State");
                prompt.AppendLine();
                prompt.AppendLine("The workspace is currently empty. All files you specify will need to be created.");
                prompt.AppendLine();
                prompt.AppendLine("---");
                prompt.AppendLine();
            }

            // Add project structure guidance if available
            var hasStructureGuidelines = projectStructure != null &&
                (projectStructure.DirectoryPurposes.Any() || projectStructure.FileLocationGuidelines.Any());

            if (projectStructure != null && hasStructureGuidelines)
            {
                prompt.AppendLine();
                prompt.AppendLine("## Project Structure Guidelines");
                prompt.AppendLine();

                if (projectStructure.NamingConventions.Any())
                {
                    prompt.AppendLine("**Naming Conventions:**");
                    foreach (var convention in projectStructure.NamingConventions)
                    {
                        prompt.AppendLine($"- {convention.Key}: {convention.Value}");
                    }
                    prompt.AppendLine();
                }

                if (projectStructure.DirectoryPurposes.Any())
                {
                    prompt.AppendLine("**Directory Organization:**");
                    foreach (var dir in projectStructure.DirectoryPurposes)
                    {
                        prompt.AppendLine($"- `{dir.Key}`: {dir.Value}");
                    }
                    prompt.AppendLine();
                }

                if (projectStructure.FileLocationGuidelines.Any())
                {
                    prompt.AppendLine("**File Placement Guidelines:**");
                    foreach (var guideline in projectStructure.FileLocationGuidelines)
                    {
                        prompt.AppendLine($"- {guideline.Key} files → `{guideline.Value}`");
                    }
                    prompt.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(projectStructure.ArchitectureNotes))
                {
                    prompt.AppendLine("**Architecture Notes:**");
                    prompt.AppendLine(projectStructure.ArchitectureNotes);
                    prompt.AppendLine();
                }

                prompt.AppendLine("IMPORTANT: Follow these guidelines when creating your implementation plan. Place files in the correct directories according to the guidelines.");
                prompt.AppendLine();
            }
            else
            {
                // Fallback: Provide default structure guidance when Wyvern didn't provide specific guidelines
                prompt.AppendLine();
                prompt.AppendLine("## Default File Organization Guidelines");
                prompt.AppendLine();
                prompt.AppendLine("No specific project structure was provided. Use these standard conventions:");
                prompt.AppendLine();
                prompt.AppendLine("**Web Projects (HTML/JS/CSS):**");
                prompt.AppendLine("- `index.html` → root folder (ONLY HTML entry point goes in root)");
                prompt.AppendLine("- JavaScript/TypeScript files → `js/` or `src/` folder (e.g., `js/app.js`) - NEVER in root");
                prompt.AppendLine("- CSS/Stylesheets → `css/` folder (e.g., `css/styles.css`) - NEVER in root");
                prompt.AppendLine("- Images/Assets → `assets/` or `assets/images/` folder");
                prompt.AppendLine("- Components → `components/` folder for reusable UI pieces");
                prompt.AppendLine();
                prompt.AppendLine("**Backend Projects:**");
                prompt.AppendLine("- Source code → `src/` folder");
                prompt.AppendLine("- Tests → `tests/` folder");
                prompt.AppendLine("- Configuration → `config/` folder");
                prompt.AppendLine();
                prompt.AppendLine("**CRITICAL**: .js, .ts, and .css files must NEVER be placed in the root folder. Only config files (package.json, tsconfig.json) and HTML entry points (index.html) belong in root.");
                prompt.AppendLine();
            }

            // Add specification context if available
            if (!string.IsNullOrEmpty(specificationContext))
            {
                prompt.AppendLine("## Project Specification");
                prompt.AppendLine(specificationContext);
                prompt.AppendLine();
                prompt.AppendLine("---");
                prompt.AppendLine();
            }

            // Add the task
            prompt.AppendLine("## Task");
            prompt.AppendLine(taskDescription);
            prompt.AppendLine();

            if (!string.IsNullOrEmpty(specificationContext) || projectStructure != null)
            {
                prompt.AppendLine("Analyze this task in the context of the project and structure guidelines above, then create a detailed implementation plan using the create_implementation_plan tool.");
            }
            else
            {
                prompt.AppendLine("Analyze this task and create a detailed implementation plan using the create_implementation_plan tool.");
            }

            return prompt.ToString();
        }

        /// <summary>
        /// Categorizes a file by its type for better display
        /// </summary>
        private string GetFileCategory(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".cs" => "C# Source Files",
                ".csproj" => "Project Files",
                ".sln" or ".slnx" => "Solution Files",
                ".json" => "Configuration Files",
                ".js" or ".ts" => "JavaScript/TypeScript",
                ".jsx" or ".tsx" => "React Components",
                ".css" or ".scss" or ".sass" => "Stylesheets",
                ".html" or ".htm" => "HTML Files",
                ".md" => "Documentation",
                ".xml" => "XML Files",
                ".yml" or ".yaml" => "YAML Configuration",
                ".txt" => "Text Files",
                ".py" => "Python Files",
                ".java" => "Java Files",
                ".cpp" or ".hpp" or ".h" or ".c" => "C/C++ Files",
                ".go" => "Go Files",
                ".rs" => "Rust Files",
                _ => "Other Files"
            };
        }
    }
}
