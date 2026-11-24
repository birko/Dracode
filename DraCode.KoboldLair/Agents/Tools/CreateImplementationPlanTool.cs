using System.Text.Json;
using Birko.AI.Orchestration.Models;
using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for creating a structured implementation plan for a task.
    /// Used by KoboldPlannerAgent to generate plans before execution. A submitted plan goes to the planner run that
    /// opened the current <see cref="Capture"/> (TASK-195): one planner serves a Drake's Kobolds in parallel, so the
    /// result is held per run, never on the tool or in process-wide state.
    /// </summary>
    public class CreateImplementationPlanTool : Tool
    {
        /// <summary>The plan one planner run received; null until the model submits one.</summary>
        public sealed class PlanCapture : IDisposable
        {
            private readonly PlanCapture? _outer;

            internal PlanCapture(PlanCapture? outer) => _outer = outer;

            public KoboldImplementationPlan? Plan { get; internal set; }

            public void Dispose() => _current.Value = _outer;
        }

        private static readonly AsyncLocal<PlanCapture?> _current = new();

        /// <summary>
        /// Starts collecting the plan for the calling run. It flows with the run's awaits into the tool call, and a run
        /// started at the same time gets its own.
        /// </summary>
        public static PlanCapture Capture() => _current.Value = new PlanCapture(_current.Value);

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
                            },
                            expected_content = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "Key identifiers/strings that must appear in the output files after this step (e.g. function names, class names, CSS selectors)"
                            }
                        },
                        required = new[] { "title", "description" }
                    }
                }
            },
            required = new[] { "steps" }
        };

        public override Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!arguments.TryGetValue("steps", out var stepsObj) || stepsObj == null)
                {
                    return Task.FromResult("Error: 'steps' parameter is required");
                }

                var steps = ParseSteps(stepsObj);
                if (steps.Count == 0)
                {
                    return Task.FromResult("Error: At least one step is required in the implementation plan");
                }

                // Create the plan
                var plan = new KoboldImplementationPlan
                {
                    Status = PlanStatus.Ready,
                    Steps = steps
                };

                if (_current.Value is { } capture)
                    capture.Plan = plan;

                // Build summary
                var totalFiles = steps.Sum(s => s.FilesToCreate.Count + s.FilesToModify.Count);
                var filesToCreate = steps.Sum(s => s.FilesToCreate.Count);
                var filesToModify = steps.Sum(s => s.FilesToModify.Count);

                return Task.FromResult($@"Implementation Plan Created

Steps: {steps.Count}
Files to create: {filesToCreate}
Files to modify: {filesToModify}

Step Summary:
{string.Join("\n", steps.Select(s => $"  {s.Index}. {s.Title}"))}

The plan is ready for execution. Each step will be processed in order.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error creating implementation plan: {ex.Message}");
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
                            FilesToModify = GetStringArrayProperty(stepElement, "files_to_modify"),
                            ExpectedContent = GetStringArrayProperty(stepElement, "expected_content")
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
                            FilesToModify = ParseStringList(dict, "files_to_modify"),
                            ExpectedContent = ParseStringList(dict, "expected_content")
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
    }
}
