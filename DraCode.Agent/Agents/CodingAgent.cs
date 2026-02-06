using DraCode.Agent.LLMs.Providers;

namespace DraCode.Agent.Agents
{
    public class CodingAgent : Agent
    {
        public CodingAgent(ILlmProvider llmProvider, AgentOptions? options = null)
            : base(llmProvider, options)
        {
        }

        // Legacy constructor for backward compatibility
        [Obsolete("Use constructor with AgentOptions instead")]
        public CodingAgent(ILlmProvider llmProvider, string workingDirectory, bool verbose = true)
            : base(llmProvider, new AgentOptions { WorkingDirectory = workingDirectory, Verbose = verbose })
        {
        }

        protected override string SystemPrompt
        {
            get
            {
                var depthGuidance = Options.ModelDepth switch
                {
                    <= 3 => @"
Reasoning approach: Quick and efficient
- Make direct, straightforward decisions
- Prioritize speed over exhaustive analysis
- Use common patterns and best practices",
                    >= 7 => @"
Reasoning approach: Deep and thorough
- Think carefully through multiple approaches before acting
- Consider edge cases and potential issues
- Analyze trade-offs and document your reasoning
- Be extra careful with changes that could have side effects",
                    _ => @"
Reasoning approach: Balanced
- Think step-by-step about what you need to do
- Consider important edge cases
- Balance thoroughness with efficiency"
                };

                return $@"You are a helpful coding assistant working in a sandboxed workspace at {WorkingDirectory}.

You have access to tools that let you read, write, and execute code. When given a task:
1. Think step-by-step about what you need to do
2. Use tools to explore the workspace, read files, make changes
3. Test your changes by running code
4. Continue iterating until the task is complete

{depthGuidance}

Important guidelines:
{GetFileOperationGuidelines()}
{GetCommonBestPractices()}

Complete the task efficiently and let me know when you're done.";
            }
        }
    }
}
