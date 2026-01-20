using System.Text.Json.Serialization;

namespace DraCode.Agent
{
    public class Message
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public object? Content { get; set; }
    }
}
