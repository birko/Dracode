using Birko.AI.Orchestration.Models;

namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// Chooses how an escalation is resolved: automatically, or by a person (FEATURE-027 D5). The policy that decides
    /// is TASK-023's; until then <see cref="AutoResolveEscalationPolicy"/> keeps every escalation on its automatic route.
    /// </summary>
    public interface IEscalationDispositionPolicy
    {
        EscalationRoute Choose(EscalationAlert alert, TaskRecord? task);
    }

    /// <param name="Disposition">Automatic, or parked for a person.</param>
    /// <param name="Timeout">How long a parked escalation waits before its automatic route runs; null waits until answered.</param>
    public sealed record EscalationRoute(EscalationDisposition Disposition, TimeSpan? Timeout = null);

    public sealed class AutoResolveEscalationPolicy : IEscalationDispositionPolicy
    {
        public static readonly AutoResolveEscalationPolicy Instance = new();

        public EscalationRoute Choose(EscalationAlert alert, TaskRecord? task) => new(EscalationDisposition.AutoResolve);
    }
}
