using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// What a decision leaves in the project's notifications (TASK-160): its card is retired once resolved, and the bell's
/// unread count goes down for an answer — the person's own doing — but shows a timeout, which nobody answered.
/// </summary>
public class DecisionNotificationsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kl-decision-notifications", Guid.NewGuid().ToString("N"));
    private readonly ProjectNotificationService _notifications;
    private readonly List<ProjectNotification> _pushed = new();

    public DecisionNotificationsTests()
    {
        Directory.CreateDirectory(_dir);
        _notifications = new ProjectNotificationService(NullLogger<ProjectNotificationService>.Instance, _dir);
        _notifications.OnNotification += (_, n) => _pushed.Add(n);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static PendingDecision Decision() => new()
    {
        ProjectId = "p-1", TaskId = "t-1", Source = PendingDecisionSource.AskHuman, Question = "Which currency?"
    };

    [Fact]
    public void An_answered_decision_leaves_nothing_unread_but_its_resolution_is_still_pushed()
    {
        var decision = Decision();
        DecisionNotifications.Required(_notifications, "shop", decision);
        _notifications.GetPendingNotifications("shop").Should().ContainSingle(n => n.Type == PendingDecisionNotification.RequiredType);

        decision.ResolutionKind = PendingDecisionResolution.Answered;
        DecisionNotifications.Resolved(_notifications, "shop", decision);

        _notifications.GetPendingNotifications("shop").Should().BeEmpty("the person answered it; there is nothing new to tell them");
        _notifications.GetNotifications("shop").Should().HaveCount(2).And.OnlyContain(n => n.ReadAt != null);
        _pushed.Select(n => n.Type).Should().Equal(PendingDecisionNotification.RequiredType, PendingDecisionNotification.ResolvedType);
    }

    [Fact]
    public void A_timed_out_decision_retires_its_card_and_says_so_unread()
    {
        var decision = Decision();
        DecisionNotifications.Required(_notifications, "shop", decision);

        decision.ResolutionKind = PendingDecisionResolution.TimedOut;
        DecisionNotifications.Resolved(_notifications, "shop", decision);

        var unread = _notifications.GetPendingNotifications("shop");
        unread.Should().ContainSingle();
        unread[0].Type.Should().Be(PendingDecisionNotification.ResolvedType);
        unread[0].Message.Should().StartWith("Decision timedout: Which currency?");
    }
}
