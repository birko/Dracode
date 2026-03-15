using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Models.Agents;

/// <summary>
/// Result from Wyvern's targeted task refinement in response to an escalation.
/// </summary>
public class WyvernRefinementResult
{
    public RefinementAction Action { get; set; }
    public List<TaskRecord>? NewTasks { get; set; }
    public TaskRecord? RevisedTask { get; set; }
    public string Summary { get; set; } = "";
}

public enum RefinementAction { Split, AddDependency, ReviseComplexity, RevisePriority, NoChange }
