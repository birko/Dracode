using DraCode.Agent.Tools;

namespace DraCode.Agent.LLMs.Providers
{
    public interface ILlmProvider
    {
        Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt);
        string Name { get; }
    }
}
