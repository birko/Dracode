using Birko.AI;
using Birko.AI.Providers;
using Birko.AI.Models;
using Birko.AI.Resilience.Configuration;
using Birko.AI.Resilience.Services;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents.SubAgents;
using DraCode.KoboldLair.Agents.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// Warden owns <c>view_cost_report</c>, so asking Dragon for costs reaches the usage data (TASK-092 / FIELD-009).
/// </summary>
public class WardenCostReportToolTests
{
    private sealed class NoReplyProvider : ILlmProvider
    {
        public string Name => "none";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public void Warden_given_a_cost_report_tool_offers_view_cost_report()
    {
        var costs = new CostTrackingService(new CostTrackingConfiguration(), NullLogger<CostTrackingService>.Instance);

        var warden = new WardenAgent(new NoReplyProvider(), new AgentOptions { Verbose = false },
            viewCostReportTool: new ViewCostReportTool(costs));

        warden.Tools.Select(t => t.Name).Should().Contain("view_cost_report");
    }
}
