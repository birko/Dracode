namespace DraCode.Agent.LLMs.Providers
{
    /// <summary>
    /// Provider for vLLM server with OpenAI-compatible API.
    /// Default endpoint: http://localhost:8000
    /// </summary>
    public class VllmProvider : OpenAiCompatibleProviderBase
    {
        protected override string ProviderName => "vLLM";

        public VllmProvider(string model = "default", string baseUrl = "http://localhost:8000", string? apiKey = null)
            : base(model, baseUrl, apiKey)
        {
        }
    }
}
