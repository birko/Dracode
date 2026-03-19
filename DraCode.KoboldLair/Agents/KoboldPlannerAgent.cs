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

**Expected Content (for validation):**
- Include `expected_content` for each step: key identifiers that MUST appear in the output files
- These verify the step was actually completed, not just that files were touched
- Include: function/method names, class names, key HTML elements, CSS selectors, import statements
- Example: Step ""Add GetUserById method"" → expected_content: [""GetUserById"", ""async Task<User>""]
- Example: Step ""Create navigation bar"" → expected_content: [""<nav"", ""navbar""]
- Keep entries short (identifiers, not full code lines)
- 2-5 entries per step is ideal

**Cross-Module Integration:**
- If this task IMPORTS or USES modules created by other tasks, check the Module APIs section
- Your plan steps MUST reference the ACTUAL function signatures, constructor parameters, and exported types
- Include a final integration verification step: ""Verify all imports match actual module exports""
- Never assume an API — if module APIs are provided, use them exactly

**Self-Reflection & Escalation Awareness:**
- The Kobold executing your plan has a `reflect` tool that it calls every 3 iterations to report progress, confidence, and blockers
- Design steps so progress is **measurable** — each step should have a clear deliverable (a file created, a function implemented) that the Kobold can assess as X% done
- Avoid steps where progress is hard to gauge (e.g. ""research best approach"" — instead, make it ""create X using Y approach"")
- If a step fails, the Kobold may escalate with types: task_infeasible, missing_dependency, needs_split, wrong_approach, wrong_agent_type
- Steps that are too large or vague increase the risk of escalation — keep steps atomic and concrete

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
        /// GAP FIX 1: Creates an implementation plan for the given task with full specification context
        /// </summary>
        /// <param name="projectId">Project identifier for this plan</param>
        /// <param name="taskId">Task identifier for this plan</param>
        /// <param name="taskDescription">The task to plan</param>
        /// <param name="specification">Optional full specification for version tracking</param>
        /// <param name="featureId">Optional feature ID this task implements</param>
        /// <param name="featureName">Optional feature name this task implements</param>
        /// <param name="featureDescription">Optional feature description</param>
        /// <param name="projectStructure">Optional project structure with file organization guidelines</param>
        /// <param name="workspaceFiles">List of files already in workspace</param>
        /// <param name="filesInUse">Files currently being worked on by other agents</param>
        /// <param name="fileMetadata">Metadata about files including their purposes</param>
        /// <param name="relatedPlans">Related completed plans that touched similar files</param>
        /// <param name="similarTaskInsights">Insights from similar task executions</param>
        /// <param name="bestPractices">Best practices learned for this agent type</param>
        /// <param name="moduleApis">Extracted public API signatures from existing workspace modules</param>
        /// <param name="projectConstraints">Project constraints from Wyrm and Wyvern that must not be violated</param>
        /// <param name="maxIterations">Maximum iterations for plan generation</param>
        /// <returns>The generated implementation plan</returns>
        public async Task<KoboldImplementationPlan> CreatePlanAsync(
            string projectId,
            string taskId,
            string taskDescription,
            Specification? specification = null,
            string? featureId = null,
            string? featureName = null,
            string? featureDescription = null,
            ProjectStructure? projectStructure = null,
            List<string>? workspaceFiles = null,
            HashSet<string>? filesInUse = null,
            Dictionary<string, string>? fileMetadata = null,
            List<KoboldImplementationPlan>? relatedPlans = null,
            List<PlanningInsight>? similarTaskInsights = null,
            Dictionary<string, string>? bestPractices = null,
            Dictionary<string, List<string>>? moduleApis = null,
            List<string>? projectConstraints = null,
            string? taskName = null,
            string? taskPriority = null,
            string? taskComplexity = null,
            List<string>? taskDependencies = null,
            int maxIterations = 5)
        {
            // Clear any previous plan
            CreateImplementationPlanTool.ClearLastPlan();

            // Build specification context for the prompt
            var specificationContext = BuildSpecificationContext(specification, featureId, featureName, featureDescription);

            // Build the prompt
            var prompt = BuildPlanningPrompt(taskDescription, specificationContext, projectStructure, workspaceFiles, filesInUse, fileMetadata, relatedPlans, similarTaskInsights, bestPractices, moduleApis, projectConstraints, taskName, taskPriority, taskComplexity, taskDependencies);

            // Run the agent to generate the plan
            await RunAsync(prompt, maxIterations);

            // Retrieve the generated plan
            var plan = CreateImplementationPlanTool.GetLastPlan();

            if (plan == null)
            {
                // Create a fallback single-step plan if no plan was generated
                plan = new KoboldImplementationPlan
                {
                    ProjectId = projectId,
                    TaskId = taskId,
                    TaskDescription = taskDescription,
                    FeatureId = featureId,
                    FeatureName = featureName,
                    FeatureDescription = featureDescription,
                    SpecificationVersion = specification?.Version ?? 1,
                    SpecificationContentHash = specification?.ContentHash ?? string.Empty,
                    SpecificationContext = specificationContext,
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
                // GAP FIX 1: Embed specification context in the plan
                plan.ProjectId = projectId;
                plan.TaskId = taskId;
                plan.TaskDescription = taskDescription;
                plan.FeatureId = featureId;
                plan.FeatureName = featureName;
                plan.FeatureDescription = featureDescription;
                plan.SpecificationVersion = specification?.Version ?? 1;
                plan.SpecificationContentHash = specification?.ContentHash ?? string.Empty;
                plan.SpecificationContext = specificationContext;

                // Phase 4: Intelligently reorder steps based on dependencies
                var analyzer = new Services.StepDependencyAnalyzer();

                // First, check for dependency violations
                var violations = plan.ValidateStepOrdering(analyzer);
                if (violations.Count > 0)
                {
                    // Log violations to the plan
                    plan.AddLogEntry($"Found {violations.Count} dependency violation(s):");
                    foreach (var violation in violations)
                    {
                        plan.AddLogEntry($"  - {violation}");
                    }

                    // Reorder the steps to fix violations
                    bool wasReordered = plan.ReorderSteps(analyzer, null);
                    if (wasReordered)
                    {
                        plan.AddLogEntry("Steps were intelligently reordered based on file dependencies");
                    }
                }
                else
                {
                    plan.AddLogEntry("Plan step ordering validated - no dependency violations found");
                }
            }

            plan.Status = PlanStatus.Ready;
            plan.AddLogEntry("Plan created by KoboldPlannerAgent");

            return plan;
        }

        /// <summary>
        /// GAP FIX 1: Builds a concise specification context for the planner
        /// Extracts only the relevant parts of the specification for this task
        /// </summary>
        private string? BuildSpecificationContext(
            Specification? specification,
            string? featureId,
            string? featureName,
            string? featureDescription)
        {
            if (specification == null) return null;

            var context = new System.Text.StringBuilder();

            // Add project overview
            if (!string.IsNullOrEmpty(specification.Name))
            {
                context.AppendLine($"**Project:** {specification.Name}");
                context.AppendLine($"**Specification Version:** {specification.Version}");
                context.AppendLine();
            }

            // Add feature context if available
            if (!string.IsNullOrEmpty(featureId) || !string.IsNullOrEmpty(featureName))
            {
                context.AppendLine("**Feature Context:**");
                context.AppendLine();

                if (!string.IsNullOrEmpty(featureName))
                {
                    context.AppendLine($"**Feature:** {featureName}");
                }

                if (!string.IsNullOrEmpty(featureDescription))
                {
                    context.AppendLine(featureDescription);
                }

                // Find and include the full feature details from specification
                if (specification.Features.Any())
                {
                    var feature = specification.Features.FirstOrDefault(f =>
                        f.Id == featureId ||
                        f.Name.Equals(featureName, StringComparison.OrdinalIgnoreCase));

                    if (feature != null)
                    {
                        if (!string.IsNullOrEmpty(feature.Description) &&
                            feature.Description != featureDescription)
                        {
                            context.AppendLine($"**Details:** {feature.Description}");
                        }

                        // GAP FIX 3: Include acceptance criteria if available
                        if (feature.AcceptanceCriteria != null && feature.AcceptanceCriteria.Any())
                        {
                            context.AppendLine("**Acceptance Criteria:**");
                            foreach (var criteria in feature.AcceptanceCriteria)
                            {
                                context.AppendLine($"- {criteria}");
                            }
                        }
                    }
                }

                context.AppendLine();
            }

            return context.Length > 0 ? context.ToString() : null;
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
            Dictionary<string, string>? bestPractices,
            Dictionary<string, List<string>>? moduleApis = null,
            List<string>? projectConstraints = null,
            string? taskName = null,
            string? taskPriority = null,
            string? taskComplexity = null,
            List<string>? taskDependencies = null)
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

            // Add module API signatures for cross-module awareness
            if (moduleApis != null && moduleApis.Any())
            {
                prompt.AppendLine("## Existing Module APIs");
                prompt.AppendLine();
                prompt.AppendLine("These are the PUBLIC APIs of existing modules in the workspace.");
                prompt.AppendLine("When your task imports or uses these modules, you MUST use their ACTUAL signatures below.");
                prompt.AppendLine("Do NOT assume or guess function signatures — use exactly what is listed here.");
                prompt.AppendLine();

                foreach (var module in moduleApis.OrderBy(m => m.Key))
                {
                    prompt.AppendLine($"### `{module.Key}`");
                    prompt.AppendLine("```");
                    foreach (var sig in module.Value)
                    {
                        prompt.AppendLine(sig);
                    }
                    prompt.AppendLine("```");
                    prompt.AppendLine();
                }

                prompt.AppendLine("**CRITICAL**: Your plan steps MUST reference these actual APIs, not assumed ones.");
                prompt.AppendLine("Include an integration verification step if this task imports other modules.");
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

            // Add project constraints prominently before specification
            if (projectConstraints != null && projectConstraints.Any())
            {
                prompt.AppendLine("## ⛔ PROJECT CONSTRAINTS (MUST NOT VIOLATE)");
                prompt.AppendLine();
                prompt.AppendLine("Your implementation plan MUST respect these constraints. Do NOT create steps that violate them:");
                prompt.AppendLine();
                foreach (var constraint in projectConstraints)
                {
                    prompt.AppendLine($"- {constraint}");
                }
                prompt.AppendLine();
                prompt.AppendLine("---");
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

            // Add task metadata (Gaps 1-3 fix: name, priority, complexity, structured dependencies)
            var hasTaskMetadata = !string.IsNullOrEmpty(taskName) || !string.IsNullOrEmpty(taskPriority) ||
                                  !string.IsNullOrEmpty(taskComplexity) || (taskDependencies != null && taskDependencies.Any());
            if (hasTaskMetadata)
            {
                prompt.AppendLine("## Task Metadata");
                prompt.AppendLine();

                if (!string.IsNullOrEmpty(taskName))
                    prompt.AppendLine($"**Task Name:** {taskName}");

                if (!string.IsNullOrEmpty(taskPriority))
                {
                    prompt.AppendLine($"**Priority:** {taskPriority}");
                    prompt.AppendLine(taskPriority.ToLower() switch
                    {
                        "critical" => "This is a **critical** task — keep steps minimal and focused on getting it working correctly.",
                        "low" => "This is a **low-priority** task — a simpler plan with fewer steps is preferred.",
                        _ => ""
                    });
                }

                if (!string.IsNullOrEmpty(taskComplexity))
                {
                    prompt.AppendLine($"**Complexity:** {taskComplexity}");
                    prompt.AppendLine(taskComplexity.ToLower() switch
                    {
                        "low" => "Low complexity — aim for 2-4 concise steps.",
                        "high" => "High complexity — plan for thorough steps with validation, but keep each step atomic.",
                        _ => "Medium complexity — aim for a balanced number of steps."
                    });
                }

                if (taskDependencies != null && taskDependencies.Any())
                {
                    prompt.AppendLine();
                    prompt.AppendLine($"**Dependencies (must be completed first):** {string.Join(", ", taskDependencies)}");
                    prompt.AppendLine("Ensure your plan accounts for outputs from these dependency tasks being available.");
                }

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

        /// <summary>
        /// Revises an existing plan after an escalation, preserving completed steps
        /// and generating new remaining steps based on reflection feedback.
        /// </summary>
        public async Task<KoboldImplementationPlan> RevisePlanAsync(
            KoboldImplementationPlan existingPlan,
            List<ReflectionEntry> reflections,
            EscalationAlert escalation,
            string? additionalContext = null)
        {
            CreateImplementationPlanTool.ClearLastPlan();

            var prompt = new System.Text.StringBuilder();
            prompt.AppendLine("# Plan Revision Required");
            prompt.AppendLine();
            prompt.AppendLine($"## Escalation: {escalation.Type}");
            prompt.AppendLine($"Summary: {escalation.Summary}");
            prompt.AppendLine();

            // Show completed steps (preserved)
            var completedSteps = existingPlan.Steps.Where(s => s.Status == StepStatus.Completed).ToList();
            if (completedSteps.Any())
            {
                prompt.AppendLine("## Completed Steps (DO NOT change these)");
                foreach (var step in completedSteps)
                {
                    prompt.AppendLine($"- Step {step.Index}: {step.Title} - {step.Description}");
                    if (step.FilesToCreate.Any())
                        prompt.AppendLine($"  Created: {string.Join(", ", step.FilesToCreate)}");
                    if (step.FilesToModify.Any())
                        prompt.AppendLine($"  Modified: {string.Join(", ", step.FilesToModify)}");
                }
                prompt.AppendLine();
            }

            // Show failed/pending steps (revisable)
            var revisableSteps = existingPlan.Steps
                .Where(s => s.Status != StepStatus.Completed && s.Status != StepStatus.Skipped)
                .ToList();
            if (revisableSteps.Any())
            {
                prompt.AppendLine("## Steps That Need Revision");
                foreach (var step in revisableSteps)
                {
                    prompt.AppendLine($"- Step {step.Index} [{step.Status}]: {step.Title} - {step.Description}");
                }
                prompt.AppendLine();
            }

            // Show reflection history
            if (reflections.Any())
            {
                prompt.AppendLine("## Reflection History (what went wrong)");
                foreach (var r in reflections.TakeLast(5))
                {
                    prompt.AppendLine($"- Iteration {r.Iteration}: progress={r.ProgressPercent}%, confidence={r.ConfidencePercent}%, decision={r.Decision}");
                    if (!string.IsNullOrEmpty(r.Blockers))
                        prompt.AppendLine($"  Blockers: {r.Blockers}");
                    if (!string.IsNullOrEmpty(r.Adjustment))
                        prompt.AppendLine($"  Adjustment: {r.Adjustment}");
                }
                prompt.AppendLine();
            }

            prompt.AppendLine("## Task");
            prompt.AppendLine(existingPlan.TaskDescription);
            prompt.AppendLine();

            if (!string.IsNullOrEmpty(additionalContext))
            {
                prompt.AppendLine("## Additional Context");
                prompt.AppendLine(additionalContext);
                prompt.AppendLine();
            }

            prompt.AppendLine("## Instructions");
            prompt.AppendLine("Create a REVISED plan that:");
            prompt.AppendLine("1. Keeps all completed work intact");
            prompt.AppendLine("2. Addresses the escalation issue with a different approach");
            prompt.AppendLine("3. Has concrete, atomic steps for the remaining work");
            prompt.AppendLine();
            prompt.AppendLine("Use the create_implementation_plan tool to submit your revised plan.");

            await RunAsync(prompt.ToString(), Options.MaxIterations > 0 ? Options.MaxIterations : 5);

            var revisedPlan = CreateImplementationPlanTool.GetLastPlan();
            if (revisedPlan == null)
            {
                return existingPlan; // Return original if revision fails
            }

            // Merge: completed steps from old plan + new remaining steps from revised plan
            var mergedSteps = new List<ImplementationStep>();
            mergedSteps.AddRange(completedSteps);

            var nextIndex = completedSteps.Count + 1;
            foreach (var step in revisedPlan.Steps)
            {
                step.Index = nextIndex++;
                step.Status = StepStatus.Pending;
                mergedSteps.Add(step);
            }

            // Update the existing plan in-place
            existingPlan.Steps = mergedSteps;
            existingPlan.CurrentStepIndex = completedSteps.Count;
            existingPlan.Status = PlanStatus.InProgress;
            existingPlan.ErrorMessage = null;
            existingPlan.AddLogEntry($"Plan revised after escalation: {escalation.Type} - {escalation.Summary}");
            existingPlan.UpdatedAt = DateTime.UtcNow;

            return existingPlan;
        }
    }
}
