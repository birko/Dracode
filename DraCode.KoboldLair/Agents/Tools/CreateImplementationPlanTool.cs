using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for creating a structured implementation plan for a task.
    /// Used by KoboldPlannerAgent to generate plans before execution.
    /// </summary>
    public class CreateImplementationPlanTool : Tool
    {
        private static KoboldImplementationPlan? _lastPlan;
        private static readonly object _lock = new();

        public override string Name => "create_implementation_plan";

        public override string Description => @"Create a structured implementation plan for a coding task.

The plan should break down the task into concrete, atomic steps that can be executed sequentially.
Each step should be self-contained and testable.

Parameters:
- steps (required): Array of implementation steps, each with:
  - title: Short description (1 line)
  - description: Detailed instructions for this step
  - files_to_create: Array of file paths to create (empty array if none)
  - files_to_modify: Array of file paths to modify (empty array if none)

Returns: Confirmation of the created plan with summary.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                steps = new
                {
                    type = "array",
                    description = "Array of implementation steps",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new
                            {
                                type = "string",
                                description = "Short title for this step (1 line)"
                            },
                            description = new
                            {
                                type = "string",
                                description = "Detailed instructions for implementing this step"
                            },
                            files_to_create = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "File paths that will be created in this step"
                            },
                            files_to_modify = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "File paths that will be modified in this step"
                            }
                        },
                        required = new[] { "title", "description" }
                    }
                }
            },
            required = new[] { "steps" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> arguments)
        {
            try
            {
                if (!arguments.TryGetValue("steps", out var stepsObj) || stepsObj == null)
                {
                    return "Error: 'steps' parameter is required";
                }

                var steps = ParseSteps(stepsObj);
                if (steps.Count == 0)
                {
                    return "Error: At least one step is required in the implementation plan";
                }

                // Create the plan
                var plan = new KoboldImplementationPlan
                {
                    Status = PlanStatus.Ready,
                    Steps = steps
                };

                // Store for retrieval
                lock (_lock)
                {
                    _lastPlan = plan;
                }

                // Build summary
                var totalFiles = steps.Sum(s => s.FilesToCreate.Count + s.FilesToModify.Count);
                var filesToCreate = steps.Sum(s => s.FilesToCreate.Count);
                var filesToModify = steps.Sum(s => s.FilesToModify.Count);

                return $@"Implementation Plan Created

Steps: {steps.Count}
Files to create: {filesToCreate}
Files to modify: {filesToModify}

Step Summary:
{string.Join("\n", steps.Select(s => $"  {s.Index}. {s.Title}"))}

The plan is ready for execution. Each step will be processed in order.";
            }
            catch (Exception ex)
            {
                return $"Error creating implementation plan: {ex.Message}";
            }
        }

        private List<ImplementationStep> ParseSteps(object stepsObj)
        {
            var steps = new List<ImplementationStep>();

            // Handle JSON element from deserialization
            if (stepsObj is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    int index = 1;
                    foreach (var stepElement in jsonElement.EnumerateArray())
                    {
                        var step = new ImplementationStep
                        {
                            Index = index++,
                            Title = GetStringProperty(stepElement, "title") ?? $"Step {index - 1}",
                            Description = GetStringProperty(stepElement, "description") ?? "",
                            FilesToCreate = GetStringArrayProperty(stepElement, "files_to_create"),
                            FilesToModify = GetStringArrayProperty(stepElement, "files_to_modify")
                        };
                        steps.Add(step);
                    }
                }
            }
            // Handle list of dictionaries
            else if (stepsObj is IEnumerable<object> enumerable)
            {
                int index = 1;
                foreach (var item in enumerable)
                {
                    if (item is Dictionary<string, object> dict)
                    {
                        var step = new ImplementationStep
                        {
                            Index = index++,
                            Title = dict.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "",
                            Description = dict.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "",
                            FilesToCreate = ParseStringList(dict, "files_to_create"),
                            FilesToModify = ParseStringList(dict, "files_to_modify")
                        };
                        steps.Add(step);
                    }
                }
            }

            return steps;
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            return null;
        }

        private static List<string> GetStringArrayProperty(JsonElement element, string propertyName)
        {
            var result = new List<string>();
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var str = item.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            result.Add(str);
                        }
                    }
                }
            }
            return result;
        }

        private static List<string> ParseStringList(Dictionary<string, object> dict, string key)
        {
            var result = new List<string>();
            if (dict.TryGetValue(key, out var value))
            {
                if (value is IEnumerable<object> list)
                {
                    result.AddRange(list.Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)));
                }
                else if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                {
                    result.AddRange(GetStringArrayProperty(jsonElement, key));
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the last created plan (for retrieval by the planner agent)
        /// </summary>
        public static KoboldImplementationPlan? GetLastPlan()
        {
            lock (_lock)
            {
                return _lastPlan;
            }
        }

        /// <summary>
        /// Clears the last plan
        /// </summary>
        public static void ClearLastPlan()
        {
            lock (_lock)
            {
                _lastPlan = null;
            }
        }
    }
}
