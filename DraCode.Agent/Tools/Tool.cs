namespace DraCode.Agent.Tools
{
    public abstract class Tool
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract object? InputSchema { get; }

        public Action<string, string>? MessageCallback { get; set; }
        public AgentOptions? Options { get; set; }

        public abstract string Execute(string workingDirectory, Dictionary<string, object> input);
        
        protected void SendMessage(string type, string content)
        {
            MessageCallback?.Invoke(type, content);
        }
    }
}
