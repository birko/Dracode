using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public class AzureOpenAiProvider : LlmProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _deployment;

        public override string Name => "Azure OpenAI";

        public AzureOpenAiProvider(string endpoint, string apiKey, string deployment = "gpt-4")
        {
            _endpoint = endpoint.TrimEnd('/');
            _apiKey = apiKey;
            _deployment = deployment;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured()) return NotConfigured();

            try
            {
                var payload = new
                {
                    messages = BuildOpenAiStyleMessages(messages, systemPrompt),
                    tools = BuildOpenAiStyleTools(tools)
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version=2024-02-15-preview";
                var response = await _httpClient.PostAsync(url, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    SendMessage("error", $"Azure OpenAI API Error: {response.StatusCode}");
                    SendMessage("error", $"Response: {responseJson}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                return ParseOpenAiStyleResponse(responseJson, MessageCallback);
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling Azure OpenAI API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        protected override bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_deployment);

        
    }
}
