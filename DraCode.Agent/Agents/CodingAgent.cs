using DraCode.Agent.LLMs.Providers;

namespace DraCode.Agent.Agents
{
    public class CodingAgent : Agent
    {
        public CodingAgent(ILlmProvider llmProvider, string workingDirectory, bool verbose = true)
            : base(llmProvider, workingDirectory, verbose)
        {
        }

        protected override string SystemPrompt => $@"You are a helpful coding assistant working in a sandboxed workspace at {WorkingDirectory}.

You have access to tools that let you read, write, and execute code. When given a task:
1. Think step-by-step about what you need to do
2. Use tools to explore the workspace, read files, make changes
3. Test your changes by running code
4. Continue iterating until the task is complete

Important guidelines:
- Always explore the workspace first with list_files before making assumptions
- Read existing files before modifying them
- Test your code after making changes
- If something fails, analyze the error and try a different approach
- Be methodical and thorough

Complete the task efficiently and let me know when you're done.";
    }
}
