namespace DraCode.Agent
{
    public class LlmResponse
    {
        public string? StopReason { get; set; }
        public List<ContentBlock>? Content { get; set; }
    }
}
