using DraCode.Agent;
using DraCode.KoboldTown.Factories;
using DraCode.KoboldTown.Wyvern;
using DraCode.KoboldTown.Supervisors;

namespace DraCode.KoboldTown.Examples
{
    /// <summary>
    /// Example demonstrating the Drake supervisor managing Kobolds and tasks
    /// </summary>
    public class DrakeExample
    {
        public static async Task RunExample()
        {
            Console.WriteLine("=== Drake Supervisor Example ===\n");

            // 1. Setup components
            var koboldFactory = new KoboldFactory(
                defaultOptions: new AgentOptions { WorkingDirectory = "./workspace", Verbose = false }
            );
            var taskTracker = new TaskTracker();
            var outputPath = "./wyrm-tasks.md";

            // 2. Create Drake supervisor
            var drake = new Drake(
                koboldFactory,
                taskTracker,
                outputPath,
                defaultProvider: "openai",
                defaultConfig: new Dictionary<string, string>
                {
                    ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
                    ["model"] = "gpt-4o"
                }
            );

            Console.WriteLine("✓ Drake supervisor created\n");

            // 3. Add tasks
            Console.WriteLine("Adding tasks...");
            var task1 = taskTracker.AddTask("Create a C# hello world program");
            var task2 = taskTracker.AddTask("Create a React component for a button");
            var task3 = taskTracker.AddTask("Write CSS for a responsive navbar");
            Console.WriteLine($"  Task 1: {task1.Id.ToString()[..8]} - {task1.Task}");
            Console.WriteLine($"  Task 2: {task2.Id.ToString()[..8]} - {task2.Task}");
            Console.WriteLine($"  Task 3: {task3.Id.ToString()[..8]} - {task3.Task}\n");

            // 4. Execute first task
            Console.WriteLine("Executing Task 1 with C# agent...");
            var (messages1, kobold1) = await drake.ExecuteTaskAsync(
                task1,
                "csharp",
                maxIterations: 5,
                messageCallback: (type, msg) =>
                {
                    var color = type switch
                    {
                        "success" => ConsoleColor.Green,
                        "error" => ConsoleColor.Red,
                        _ => ConsoleColor.Gray
                    };
                    var oldColor = Console.ForegroundColor;
                    Console.ForegroundColor = color;
                    Console.WriteLine($"  {msg}");
                    Console.ForegroundColor = oldColor;
                }
            );
            Console.WriteLine($"  Completed with {messages1.Count} messages\n");

            // 5. Show statistics
            var stats = drake.GetStatistics();
            Console.WriteLine($"Statistics: {stats}\n");

            // 6. Summon Kobold for task 2 (manual control)
            Console.WriteLine("Manually summoning Kobold for Task 2...");
            var kobold2 = drake.SummonKobold(task2, "react", "openai");
            Console.WriteLine($"  Summoned: {kobold2}\n");

            // 7. Start Kobold work (Kobold manages its own state)
            Console.WriteLine("Starting Kobold work (Kobold manages its own state)...");
            var messages2 = await drake.StartKoboldWorkAsync(kobold2.Id, maxIterations: 5);
            Console.WriteLine($"  Kobold status after work: {kobold2.Status}");
            Console.WriteLine($"  Kobold is complete: {kobold2.IsComplete}");
            Console.WriteLine($"  Kobold is success: {kobold2.IsSuccess}");
            Console.WriteLine($"  Task status: {task2.Status}");
            Console.WriteLine($"  Executed with {messages2.Count} messages\n");

            // 8. Monitor tasks (Drake syncs from Kobold states)
            Console.WriteLine("Monitoring tasks (syncing from Kobold states)...");
            drake.MonitorTasks();
            stats = drake.GetStatistics();
            Console.WriteLine($"  {stats}\n");

            // 10. Check markdown output
            Console.WriteLine($"Markdown report saved to: {outputPath}");
            if (File.Exists(outputPath))
            {
                var lines = File.ReadAllLines(outputPath).Take(10);
                Console.WriteLine("  First 10 lines:");
                foreach (var line in lines)
                {
                    Console.WriteLine($"    {line}");
                }
                Console.WriteLine();
            }

            // 11. Unsummon completed Kobolds
            Console.WriteLine("Unsummoning completed Kobolds...");
            int unsummoned = drake.UnsummonCompletedKobolds();
            Console.WriteLine($"  Unsummoned {unsummoned} Kobolds\n");

            stats = drake.GetStatistics();
            Console.WriteLine($"Final Statistics: {stats}\n");

            Console.WriteLine("=== Example Complete ===");
        }

        /// <summary>
        /// Example demonstrating batch task processing
        /// </summary>
        public static async Task BatchProcessingExample()
        {
            Console.WriteLine("\n=== Drake Batch Processing Example ===\n");

            var koboldFactory = new KoboldFactory();
            var taskTracker = new TaskTracker();
            var outputPath = "./drake-batch-tasks.md";

            var drake = new Drake(
                koboldFactory,
                taskTracker,
                outputPath,
                defaultProvider: "openai"
            );

            // Add multiple tasks
            var tasks = new[]
            {
                taskTracker.AddTask("Task 1: Implement binary search"),
                taskTracker.AddTask("Task 2: Create linked list"),
                taskTracker.AddTask("Task 3: Write sorting algorithm")
            };

            Console.WriteLine($"Processing {tasks.Length} tasks in parallel...\n");

            // Process tasks in parallel
            var executionTasks = tasks.Select(task =>
                drake.ExecuteTaskAsync(
                    task,
                    "csharp",
                    maxIterations: 5,
                    messageCallback: (type, msg) =>
                    {
                        Console.WriteLine($"[{task.Id.ToString()[..8]}] {msg}");
                    }
                )
            );

            await Task.WhenAll(executionTasks);

            Console.WriteLine("\n✓ All tasks completed\n");

            var stats = drake.GetStatistics();
            Console.WriteLine($"Final Statistics: {stats}");

            // Cleanup
            int cleaned = drake.UnsummonCompletedKobolds();
            Console.WriteLine($"Cleaned up {cleaned} Kobolds");
        }

        /// <summary>
        /// Example demonstrating task monitoring
        /// </summary>
        public static async Task MonitoringExample()
        {
            Console.WriteLine("\n=== Drake Monitoring Example ===\n");

            var koboldFactory = new KoboldFactory();
            var taskTracker = new TaskTracker();
            var outputPath = "./drake-monitor-tasks.md";

            var drake = new Drake(koboldFactory, taskTracker, outputPath);

            // Add tasks
            var task1 = taskTracker.AddTask("Long running task 1");
            var task2 = taskTracker.AddTask("Long running task 2");

            // Start tasks asynchronously
            var task1Execution = Task.Run(async () =>
            {
                await drake.ExecuteTaskAsync(task1, "csharp", maxIterations: 10);
            });

            var task2Execution = Task.Run(async () =>
            {
                await drake.ExecuteTaskAsync(task2, "react", maxIterations: 10);
            });

            // Monitor while tasks are running
            Console.WriteLine("Monitoring task progress...\n");
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(1000);
                
                drake.MonitorTasks();
                var stats = drake.GetStatistics();
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {stats}");
                
                // Check individual Kobolds
                var kobold1 = drake.GetKoboldForTask(task1.Id);
                var kobold2 = drake.GetKoboldForTask(task2.Id);
                
                if (kobold1 != null)
                    Console.WriteLine($"  Task 1 Kobold: {kobold1.Status}");
                if (kobold2 != null)
                    Console.WriteLine($"  Task 2 Kobold: {kobold2.Status}");
                
                Console.WriteLine();
            }

            // Wait for completion
            await Task.WhenAll(task1Execution, task2Execution);

            Console.WriteLine("✓ Monitoring complete");
        }
    }
}
