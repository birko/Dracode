using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// The project notifications a decision raises (TASK-020, TASK-160): a parked task raises an answerable
    /// <c>decision_required</c> card; its answer or timeout retires the card (marked read) and raises
    /// <c>decision_resolved</c>, which every session watching the project gets live so its card goes too. An answer is the
    /// person's own doing, so its <c>decision_resolved</c> is kept as read: the bell drops the card rather than trade it
    /// for news of what they just did. A timeout stays unread: nobody answered, and the bell says so.
    /// </summary>
    public static class DecisionNotifications
    {
        public static void Required(ProjectNotificationService notifications, string projectName, PendingDecision decision) =>
            notifications.Notify(projectName, PendingDecisionNotification.RequiredType,
                PendingDecisionNotification.QuestionText(decision), PendingDecisionNotification.ToMetadata(decision));

        public static void Resolved(ProjectNotificationService notifications, string projectName, PendingDecision decision)
        {
            var cards = notifications.GetPendingNotifications(projectName)
                .Where(n => n.Type == PendingDecisionNotification.RequiredType
                    && n.Metadata.TryGetValue(PendingDecisionNotification.DecisionId, out var id) && id == decision.Id)
                .Select(n => n.Id)
                .ToList();
            if (cards.Count > 0)
                notifications.MarkAsRead(projectName, cards);
            var resolved = notifications.Notify(projectName, PendingDecisionNotification.ResolvedType,
                $"Decision {decision.ResolutionKind?.ToString().ToLowerInvariant()}: {PendingDecisionNotification.QuestionText(decision)}",
                PendingDecisionNotification.ToResolvedMetadata(decision));
            if (decision.ResolutionKind == PendingDecisionResolution.Answered)
                notifications.MarkAsRead(projectName, new[] { resolved.Id });
        }
    }
}
