using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
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
        /// <param name="maxIterations">Maximum iterations for plan generation</param>
        /// <returns>The generated implementation plan</returns>
        public async Task<KoboldImplementationPlan> CreatePlanAsync(
            string taskDescription,
            string? specificationContext = null,
            ProjectStructure? projectStructure = null,
            int maxIterations = 5)
        {
            // Clear any previous plan
            CreateImplementationPlanTool.ClearLastPlan();

            // Build the prompt
            var prompt = BuildPlanningPrompt(taskDescription, specificationContext, projectStructure);

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

        private string BuildPlanningPrompt(string taskDescription, string? specificationContext, ProjectStructure? projectStructure)
        {
            var prompt = new System.Text.StringBuilder();
            prompt.AppendLine("Please create an implementation plan for the following task.");

            // Add project structure guidance if available
            if (projectStructure != null)
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
    }
}
