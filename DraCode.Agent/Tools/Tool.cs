namespace DraCode.Agent.Tools
{
    public abstract class Tool
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract object? InputSchema { get; }

        public abstract string Execute(string workingDirectory, Dictionary<string, object> input);
    }
}
