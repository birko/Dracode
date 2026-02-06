using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Phase 4: Analyzes step dependencies to enable parallel execution.
    /// Identifies which steps can run in parallel based on file dependencies.
    /// </summary>
    public class StepDependencyAnalyzer
    {
        /// <summary>
        /// Analyzes a plan and returns groups of steps that can execute in parallel.
        /// Each group contains steps with no dependencies on each other.
        /// </summary>
        /// <param name="plan">The implementation plan to analyze</param>
        /// <returns>List of step groups where each group can execute in parallel</returns>
        public List<List<ImplementationStep>> AnalyzeParallelGroups(KoboldImplementationPlan plan)
        {
            var groups = new List<List<ImplementationStep>>();
            var remainingSteps = new List<ImplementationStep>(plan.Steps);
            var completedFiles = new HashSet<string>();

            while (remainingSteps.Count > 0)
            {
                var currentGroup = new List<ImplementationStep>();
                var filesTouched = new HashSet<string>();

                foreach (var step in remainingSteps.ToList())
                {
                    // Check if this step depends on any file being created by other steps in the current group
                    var dependsOnGroupFiles = step.FilesToModify.Any(f => filesTouched.Contains(f));
                    var createsConflictingFile = step.FilesToCreate.Any(f => filesTouched.Contains(f));

                    if (!dependsOnGroupFiles && !createsConflictingFile)
                    {
                        // This step can run in parallel with the current group
                        currentGroup.Add(step);
                        remainingSteps.Remove(step);

                        // Track files this step will touch
                        foreach (var file in step.FilesToCreate.Concat(step.FilesToModify))
                        {
                            filesTouched.Add(file);
                        }
                    }
                }

                if (currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                    
                    // Mark files as completed after this group
                    foreach (var file in filesTouched)
                    {
                        completedFiles.Add(file);
                    }
                }
                else if (remainingSteps.Count > 0)
                {
                    // Deadlock detected or circular dependency
                    // Force the first remaining step into its own group
                    var forcedStep = remainingSteps[0];
                    groups.Add(new List<ImplementationStep> { forcedStep });
                    remainingSteps.RemoveAt(0);
                }
            }

            return groups;
        }

        /// <summary>
        /// Checks if two steps have file dependencies that prevent parallel execution
        /// </summary>
        public bool HasDependency(ImplementationStep step1, ImplementationStep step2)
        {
            // Step2 modifies files that step1 creates
            if (step2.FilesToModify.Any(f => step1.FilesToCreate.Contains(f)))
                return true;

            // Step2 creates files that step1 modifies
            if (step2.FilesToCreate.Any(f => step1.FilesToModify.Contains(f)))
                return true;

            // Both steps modify the same file
            if (step1.FilesToModify.Any(f => step2.FilesToModify.Contains(f)))
                return true;

            // Both steps create the same file
            if (step1.FilesToCreate.Any(f => step2.FilesToCreate.Contains(f)))
                return true;

            return false;
        }

        /// <summary>
        /// Suggests an optimal execution order for steps based on dependencies
        /// </summary>
        public List<ImplementationStep> SuggestOptimalOrder(List<ImplementationStep> steps)
        {
            // Build dependency graph
            var dependencies = new Dictionary<ImplementationStep, List<ImplementationStep>>();
            foreach (var step in steps)
            {
                dependencies[step] = new List<ImplementationStep>();
                
                foreach (var otherStep in steps)
                {
                    if (step != otherStep && StepDependsOn(step, otherStep))
                    {
                        dependencies[step].Add(otherStep);
                    }
                }
            }

            // Topological sort
            var sorted = new List<ImplementationStep>();
            var visited = new HashSet<ImplementationStep>();
            var visiting = new HashSet<ImplementationStep>();

            foreach (var step in steps)
            {
                if (!visited.Contains(step))
                {
                    TopologicalSortVisit(step, dependencies, visited, visiting, sorted);
                }
            }

            sorted.Reverse();
            return sorted;
        }

        private bool StepDependsOn(ImplementationStep step, ImplementationStep dependency)
        {
            // Step depends on dependency if it modifies files that dependency creates
            return step.FilesToModify.Any(f => dependency.FilesToCreate.Contains(f));
        }

        private void TopologicalSortVisit(
            ImplementationStep step,
            Dictionary<ImplementationStep, List<ImplementationStep>> dependencies,
            HashSet<ImplementationStep> visited,
            HashSet<ImplementationStep> visiting,
            List<ImplementationStep> sorted)
        {
            if (visiting.Contains(step))
            {
                // Circular dependency detected - skip
                return;
            }

            if (visited.Contains(step))
            {
                return;
            }

            visiting.Add(step);

            foreach (var dependency in dependencies[step])
            {
                TopologicalSortVisit(dependency, dependencies, visited, visiting, sorted);
            }

            visiting.Remove(step);
            visited.Add(step);
            sorted.Add(step);
        }
    }
}
