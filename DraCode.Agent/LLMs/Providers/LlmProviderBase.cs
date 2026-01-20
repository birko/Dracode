using DraCode.Agent.Tools;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public abstract class LlmProviderBase : ILlmProvider
    {
        public abstract string Name { get; }

        public abstract Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt);

        protected static List<object> BuildOpenAiStyleMessages(IEnumerable<Message> messages, string systemPrompt)
        {
            var list = new List<object> { new { role = "system", content = systemPrompt } };
            list.AddRange(messages.Select(m => new { role = m.Role, content = m.Content }));
            return list;
        }

        protected static object BuildOpenAiStyleTools(IEnumerable<Tool> tools) => tools.Select(t => new
        {
            type = "function",
            function = new { name = t.Name, description = t.Description, parameters = t.InputSchema }
        }).ToList();

        protected static LlmResponse ParseOpenAiStyleResponse(string responseJson)
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseJson);
            var choice = result.GetProperty("choices")[0];
            var message = choice.GetProperty("message");

            var llmResponse = new LlmResponse { Content = [] };

            if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
            {
                llmResponse.StopReason = "tool_use";
                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var argumentsJson = function.GetProperty("arguments").GetString();
                    var args = argumentsJson is not null ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson) : [];
                    llmResponse.Content.Add(new ContentBlock
                    {
                        Type = "tool_use",
                        Id = toolCall.GetProperty("id").GetString(),
                        Name = function.GetProperty("name").GetString(),
                        Input = args
                    });
                }
            }
            else
            {
                llmResponse.StopReason = "end_turn";
                if (message.TryGetProperty("content", out var textContent))
                {
                    llmResponse.Content.Add(new ContentBlock { Type = "text", Text = textContent.GetString() });
                }
            }

            return llmResponse;
        }

        protected static LlmResponse NotConfigured() => new() { StopReason = "NotConfigured", Content = [] };
    }
}
