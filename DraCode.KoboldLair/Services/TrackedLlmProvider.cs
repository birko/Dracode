using DraCode.Agent;
using DraCode.Agent.LLMs;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Decorator that wraps an ILlmProvider with rate limiting and cost tracking.
    /// Intercepts all LLM calls to enforce limits and record usage.
    /// </summary>
    public class TrackedLlmProvider : ILlmProvider
    {
        private readonly ILlmProvider _inner;
        private readonly ProviderRateLimiter? _rateLimiter;
        private readonly CostTrackingService? _costTracker;
        private readonly ILogger? _logger;

        /// <summary>
        /// Tracking context — set by the caller (factory) to associate usage with project/task/agent.
        /// </summary>
        public string? ProjectId { get; set; }
        public string? TaskId { get; set; }
        public string? AgentType { get; set; }
        public string? CallerContext { get; set; }

        public string Name => _inner.Name;

        public Action<string, string>? MessageCallback
        {
            get => _inner.MessageCallback;
            set => _inner.MessageCallback = value;
        }

        public TrackedLlmProvider(
            ILlmProvider inner,
            ProviderRateLimiter? rateLimiter = null,
            CostTrackingService? costTracker = null,
            ILogger? logger = null)
        {
            _inner = inner;
            _rateLimiter = rateLimiter;
            _costTracker = costTracker;
            _logger = logger;
        }

        public async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            // Check rate limit before calling
            if (_rateLimiter != null && !_rateLimiter.CanMakeRequest(Name))
            {
                var retryAfter = _rateLimiter.GetRetryAfter(Name);
                if (retryAfter.HasValue && retryAfter.Value.TotalSeconds > 0)
                {
                    _logger?.LogWarning("Rate limited for {Provider}, waiting {Seconds:F1}s", Name, retryAfter.Value.TotalSeconds);
                    await Task.Delay(retryAfter.Value);
                }
                else
                {
                    // Brief wait as fallback
                    await Task.Delay(1000);
                }
            }

            // Check budget
            if (_costTracker != null)
            {
                var budgetStatus = await _costTracker.CheckBudgetAsync(ProjectId);
                if (!budgetStatus.IsWithinBudget)
                {
                    var msg = $"Budget exceeded ({budgetStatus.BudgetType}): ${budgetStatus.CurrentSpend:F2} / ${budgetStatus.BudgetLimit:F2}";
                    _logger?.LogWarning(msg);
                    return LlmResponse.Error(msg);
                }
            }

            var response = await _inner.SendMessageAsync(messages, tools, systemPrompt);

            // Record usage after successful call
            await RecordUsageFromResponse(response.Usage);

            return response;
        }

        public async Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            // Check rate limit
            if (_rateLimiter != null && !_rateLimiter.CanMakeRequest(Name))
            {
                var retryAfter = _rateLimiter.GetRetryAfter(Name);
                if (retryAfter.HasValue && retryAfter.Value.TotalSeconds > 0)
                {
                    _logger?.LogWarning("Rate limited for {Provider} (streaming), waiting {Seconds:F1}s", Name, retryAfter.Value.TotalSeconds);
                    await Task.Delay(retryAfter.Value);
                }
                else
                {
                    await Task.Delay(1000);
                }
            }

            // Check budget
            if (_costTracker != null)
            {
                var budgetStatus = await _costTracker.CheckBudgetAsync(ProjectId);
                if (!budgetStatus.IsWithinBudget)
                {
                    var msg = $"Budget exceeded ({budgetStatus.BudgetType}): ${budgetStatus.CurrentSpend:F2} / ${budgetStatus.BudgetLimit:F2}";
                    return new LlmStreamingResponse
                    {
                        GetStreamAsync = () => Task.FromResult<IAsyncEnumerable<string>>(EmptyStream()),
                        Error = msg,
                        FinalResponse = LlmResponse.Error(msg)
                    };
                }
            }

            var streamingResponse = await _inner.SendMessageStreamingAsync(messages, tools, systemPrompt);

            // Wrap the stream to capture usage after completion
            var originalGetStream = streamingResponse.GetStreamAsync;
            streamingResponse.GetStreamAsync = async () =>
            {
                var stream = await originalGetStream();
                return WrapStreamForUsageTracking(stream, streamingResponse);
            };

            return streamingResponse;
        }

        private async IAsyncEnumerable<string> WrapStreamForUsageTracking(
            IAsyncEnumerable<string> innerStream,
            LlmStreamingResponse streamingResponse)
        {
            await foreach (var chunk in innerStream)
            {
                yield return chunk;
            }

            // After stream completes, record usage from final response
            var usage = streamingResponse.Usage ?? streamingResponse.FinalResponse?.Usage;
            await RecordUsageFromResponse(usage);
        }

        private async Task RecordUsageFromResponse(TokenUsage? usage)
        {
            if (usage == null || (usage.PromptTokens == 0 && usage.CompletionTokens == 0))
                return;

            // Record with rate limiter
            _rateLimiter?.RecordRequest(Name, usage.TotalTokens);

            // Record with cost tracker
            if (_costTracker != null)
            {
                try
                {
                    await _costTracker.RecordUsageAsync(new UsageRecord(
                        Provider: Name,
                        Model: "", // Model name not available at provider level — populated by factory if known
                        PromptTokens: usage.PromptTokens,
                        CompletionTokens: usage.CompletionTokens,
                        ProjectId: ProjectId,
                        TaskId: TaskId,
                        AgentType: AgentType,
                        CallerContext: CallerContext));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to record usage for {Provider}", Name);
                }
            }
        }

        private static async IAsyncEnumerable<string> EmptyStream()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
