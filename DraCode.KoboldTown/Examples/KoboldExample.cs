using DraCode.Agent;
using DraCode.KoboldTown.Factories;
using DraCode.KoboldTown.Models;

namespace DraCode.KoboldTown.Examples
{
    /// <summary>
    /// Example demonstrating the Kobold worker system usage
    /// </summary>
    public class KoboldExample
    {
        public static async Task RunExample()
        {
            Console.WriteLine("=== Kobold Worker System Example ===\n");

            // 1. Create KoboldFactory
            var factory = new KoboldFactory(
                defaultOptions: new AgentOptions
                {
                    WorkingDirectory = "./workspace",
                    Verbose = true
                },
                defaultConfig: new Dictionary<string, string>
                {
                    ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
                    ["model"] = "gpt-4o"
                }
            );

            Console.WriteLine("✓ KoboldFactory created\n");

            // 2. Create different types of Kobolds
            Console.WriteLine("Creating Kobolds...");
            var csharpKobold = factory.CreateKobold("openai", "csharp");
            Console.WriteLine($"  - {csharpKobold}");

            var reactKobold = factory.CreateKobold("openai", "react");
            Console.WriteLine($"  - {reactKobold}");

            var cssKobold = factory.CreateKobold("openai", "css");
            Console.WriteLine($"  - {cssKobold}\n");

            // 3. Show initial statistics
            var stats = factory.GetStatistics();
            Console.WriteLine($"Initial Statistics: {stats}");
            Console.WriteLine($"  By Type: {string.Join(", ", stats.ByAgentType.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}\n");

            // 4. Simulate task assignment
            var taskId = Guid.NewGuid();
            var taskDescription = "Create a simple C# hello world program";
            Console.WriteLine($"Assigning task {taskId.ToString()[..8]}... to C# Kobold");
            csharpKobold.AssignTask(taskId, taskDescription);
            Console.WriteLine($"  Status: {csharpKobold.Status}");
            Console.WriteLine($"  Assigned At: {csharpKobold.AssignedAt}");
            Console.WriteLine($"  Task: {csharpKobold.TaskDescription}\n");

            // 5. Start working (executes automatically)
            Console.WriteLine("Starting work (Kobold will execute the task)...");
            var messages = await csharpKobold.StartWorkingAsync(maxIterations: 5);
            Console.WriteLine($"  Status: {csharpKobold.Status}");
            Console.WriteLine($"  Started At: {csharpKobold.StartedAt}");
            Console.WriteLine($"  Completed with {messages.Count} messages\n");

            // 6. Query working Kobolds
            var workingKobolds = factory.GetWorkingKobolds();
            Console.WriteLine($"Working Kobolds: {workingKobolds.Count}");
            foreach (var kobold in workingKobolds)
            {
                Console.WriteLine($"  - {kobold}");
            }
            Console.WriteLine();

            // 7. Task automatically transitions to Done after StartWorkingAsync completes
            Console.WriteLine("Task completed (Kobold automatically transitioned to Done)...");
            Console.WriteLine($"  Status: {csharpKobold.Status}");
            Console.WriteLine($"  Is Complete: {csharpKobold.IsComplete}");
            Console.WriteLine($"  Is Success: {csharpKobold.IsSuccess}");
            Console.WriteLine($"  Completed At: {csharpKobold.CompletedAt}");
            
            var duration = csharpKobold.CompletedAt - csharpKobold.StartedAt;
            Console.WriteLine($"  Duration: {duration?.TotalSeconds:F2}s\n");

            // 8. Show final statistics
            stats = factory.GetStatistics();
            Console.WriteLine($"Final Statistics: {stats}\n");

            // 9. Demonstrate Kobold lookup
            Console.WriteLine("Looking up Kobolds...");
            var foundKobold = factory.GetKoboldByTaskId(taskId);
            Console.WriteLine($"  By Task ID: {foundKobold}\n");

            var csharpKobolds = factory.GetKoboldsByType("csharp");
            Console.WriteLine($"  C# Kobolds: {csharpKobolds.Count}");

            // 10. Cleanup
            Console.WriteLine("\nCleaning up done Kobolds...");
            int removed = factory.CleanupDoneKobolds();
            Console.WriteLine($"  Removed: {removed} Kobolds");

            stats = factory.GetStatistics();
            Console.WriteLine($"  After cleanup: {stats}\n");

            // 11. Demonstrate Kobold reuse
            Console.WriteLine("Demonstrating Kobold reset and reuse...");
            var reusableKobold = factory.GetKobold(reactKobold.Id);
            if (reusableKobold != null)
            {
                Console.WriteLine($"  Before reset: {reusableKobold}");
                
                // Assign and complete a task
                var newTaskId = Guid.NewGuid();
                reusableKobold.AssignTask(newTaskId, "Create a React button component");
                await reusableKobold.StartWorkingAsync(5);
                // StartWorkingAsync automatically transitions to Done
                
                Console.WriteLine($"  After task: {reusableKobold}");
                
                // Reset for reuse
                reusableKobold.Reset();
                Console.WriteLine($"  After reset: {reusableKobold}");
            }

            Console.WriteLine("\n=== Example Complete ===");
        }

        /// <summary>
        /// Example demonstrating error handling
        /// </summary>
        public static async Task ErrorHandlingExample()
        {
            Console.WriteLine("\n=== Error Handling Example ===\n");

            var factory = new KoboldFactory();
            var kobold = factory.CreateKobold("openai", "csharp");

            try
            {
                // Try to start working without assigning task
                await kobold.StartWorkingAsync();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"✗ Expected error: {ex.Message}");
            }

            try
            {
                // Assign task twice
                var taskId = Guid.NewGuid();
                kobold.AssignTask(taskId, "Test task");
                kobold.AssignTask(Guid.NewGuid(), "Another task");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"✗ Expected error: {ex.Message}");
            }

            Console.WriteLine("\n✓ Error handling works correctly");
        }

        /// <summary>
        /// Example demonstrating pool pattern
        /// </summary>
        public static async Task PoolPatternExample()
        {
            Console.WriteLine("\n=== Pool Pattern Example ===\n");

            var factory = new KoboldFactory();

            // Create a pool of Kobolds
            Console.WriteLine("Creating Kobold pool (5 workers)...");
            for (int i = 0; i < 5; i++)
            {
                factory.CreateKobold("openai", "csharp");
            }

            var stats = factory.GetStatistics();
            Console.WriteLine($"Pool ready: {stats}\n");

            // Simulate multiple tasks
            var tasks = Enumerable.Range(1, 3).Select(i => (Id: Guid.NewGuid(), Index: i)).ToList();
            
            Console.WriteLine($"Processing {tasks.Count} tasks...");
            foreach (var task in tasks)
            {
                // Get available Kobold
                var kobold = factory.GetUnassignedKobolds().FirstOrDefault();
                
                if (kobold != null)
                {
                    Console.WriteLine($"  Task {task.Id.ToString()[..8]} → Kobold {kobold.Id.ToString()[..8]}");
                    kobold.AssignTask(task.Id, $"Process task {task.Index}");
                    
                    // Simulate async work
                    _ = Task.Run(async () =>
                    {
                        await kobold.StartWorkingAsync(5);
                        // StartWorkingAsync automatically transitions to Done
                        Console.WriteLine($"    ✓ Kobold {kobold.Id.ToString()[..8]} completed (Status: {kobold.Status})");
                    });
                }
            }

            // Wait for completion
            await Task.Delay(5000);

            stats = factory.GetStatistics();
            Console.WriteLine($"\nFinal state: {stats}");
        }
    }
}
