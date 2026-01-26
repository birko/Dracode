using DraCode.Agent;
using DraCode.KoboldTown.Factories;
using DraCode.KoboldTown.Wyvern;
using DraCode.KoboldTown.Supervisors;

namespace DraCode.KoboldTown.Examples
{
    /// <summary>
    /// Example demonstrating the DrakeFactory usage
    /// </summary>
    public class DrakeFactoryExample
    {
        public static async Task RunExample()
        {
            Console.WriteLine("=== DrakeFactory Example ===\n");

            // 1. Setup factories
            var koboldFactory = new KoboldFactory(
                defaultOptions: new AgentOptions { WorkingDirectory = "./workspace", Verbose = false }
            );

            var drakeFactory = new DrakeFactory(
                koboldFactory,
                defaultProvider: "openai",
                defaultConfig: new Dictionary<string, string>
                {
                    ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
                    ["model"] = "gpt-4o"
                }
            );

            Console.WriteLine("✓ Factories created\n");

            // 2. Create Drakes for different wyvern outputs
            Console.WriteLine("Creating Drakes for different task files...");
            
            var drake1 = drakeFactory.CreateDrake("./tasks/Wyvern-output-1.md", "drake-1");
            Console.WriteLine($"  ✓ Drake 1 monitoring: ./tasks/Wyvern-output-1.md");

            var drake2 = drakeFactory.CreateDrake("./tasks/Wyvern-output-2.md", "drake-2");
            Console.WriteLine($"  ✓ Drake 2 monitoring: ./tasks/Wyvern-output-2.md\n");

            // 3. Show Drake statistics
            Console.WriteLine($"Total Drakes: {drakeFactory.TotalDrakes}\n");

            // 4. Add tasks to first Drake
            Console.WriteLine("Adding tasks to Drake 1...");
            var tracker1 = drake1.GetTaskTracker();
            var task1 = tracker1.AddTask("Create C# hello world");
            var task2 = tracker1.AddTask("Write unit tests");
            Console.WriteLine($"  Added task: {task1.Task}");
            Console.WriteLine($"  Added task: {task2.Task}\n");

            // 5. Execute task through Drake
            Console.WriteLine("Executing task through Drake 1...");
            var (messages, kobold) = await drake1.ExecuteTaskAsync(
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

            Console.WriteLine($"\n✓ Task completed with {messages.Count} messages\n");

            // 6. Get Drake statistics
            var stats1 = drake1.GetStatistics();
            Console.WriteLine($"Drake 1 Stats: {stats1}\n");

            // 7. Monitor all Drakes
            Console.WriteLine("Monitoring all Drakes...");
            foreach (var drake in drakeFactory.GetAllDrakes())
            {
                drake.MonitorTasks();
                var stats = drake.GetStatistics();
                Console.WriteLine($"  Drake Stats: {stats}");
            }
            Console.WriteLine();

            // 8. Cleanup
            Console.WriteLine("Cleaning up...");
            var unsummoned = drake1.UnsummonCompletedKobolds();
            Console.WriteLine($"  Unsummoned {unsummoned} Kobolds from Drake 1\n");

            Console.WriteLine("=== Example Complete ===");
        }

        /// <summary>
        /// Example demonstrating background monitoring simulation
        /// </summary>
        public static async Task BackgroundMonitoringExample()
        {
            Console.WriteLine("\n=== Background Monitoring Simulation ===\n");

            var koboldFactory = new KoboldFactory();
            var drakeFactory = new DrakeFactory(koboldFactory);

            // Create Drake
            var drake = drakeFactory.CreateDrake("./tasks/monitored-tasks.md", "monitored-drake");
            
            // Add tasks
            var tracker = drake.GetTaskTracker();
            var task1 = tracker.AddTask("Long running task 1");
            var task2 = tracker.AddTask("Long running task 2");

            Console.WriteLine("Starting tasks...\n");

            // Start tasks in background
            var task1Execution = Task.Run(async () =>
            {
                await drake.ExecuteTaskAsync(task1, "csharp", maxIterations: 10);
            });

            var task2Execution = Task.Run(async () =>
            {
                await drake.ExecuteTaskAsync(task2, "react", maxIterations: 10);
            });

            // Simulate monitoring every 5 seconds
            Console.WriteLine("Monitoring every 5 seconds (like background service)...\n");
            
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(5000);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Monitoring cycle {i + 1}");
                
                drake.MonitorTasks();
                var stats = drake.GetStatistics();
                
                Console.WriteLine($"  {stats}");
                
                if (stats.WorkingKobolds == 0 && stats.DoneKobolds > 0)
                {
                    var unsummoned = drake.UnsummonCompletedKobolds();
                    Console.WriteLine($"  Unsummoned {unsummoned} completed Kobolds");
                }

                drake.UpdateTasksFile();
                Console.WriteLine($"  Updated: ./tasks/monitored-tasks.md");
                Console.WriteLine();

                // Check if all done
                if (stats.WorkingTasks == 0 && stats.DoneTasks == 2)
                {
                    Console.WriteLine("✅ All tasks completed!");
                    break;
                }
            }

            // Wait for tasks to complete
            await Task.WhenAll(task1Execution, task2Execution);

            Console.WriteLine("\n=== Monitoring Complete ===");
        }

        /// <summary>
        /// Example demonstrating multiple Drakes monitoring different paths
        /// </summary>
        public static async Task MultiDrakeExample()
        {
            Console.WriteLine("\n=== Multiple Drakes Example ===\n");

            var koboldFactory = new KoboldFactory();
            var drakeFactory = new DrakeFactory(koboldFactory);

            // Create Drakes for different teams/projects
            var frontendDrake = drakeFactory.CreateDrake("./tasks/frontend-tasks.md", "frontend-drake");
            var backendDrake = drakeFactory.CreateDrake("./tasks/backend-tasks.md", "backend-drake");
            var devopsDrake = drakeFactory.CreateDrake("./tasks/devops-tasks.md", "devops-drake");

            Console.WriteLine($"Created {drakeFactory.TotalDrakes} Drakes:\n");

            // Add tasks to each Drake
            var frontendTracker = frontendDrake.GetTaskTracker();
            frontendTracker.AddTask("Create React component");
            frontendTracker.AddTask("Style with CSS");

            var backendTracker = backendDrake.GetTaskTracker();
            backendTracker.AddTask("Create API endpoint");
            backendTracker.AddTask("Write database migration");

            var devopsTracker = devopsDrake.GetTaskTracker();
            devopsTracker.AddTask("Configure deployment pipeline");

            // Show all Drakes
            foreach (var drake in drakeFactory.GetAllDrakes())
            {
                var stats = drake.GetStatistics();
                Console.WriteLine($"  Drake: {stats.TotalTasks} tasks");
            }

            Console.WriteLine("\n✅ Multiple Drakes ready for monitoring");
        }
    }

    /// <summary>
    /// Extension method to get task tracker from Drake (helper for examples)
    /// </summary>
    public static class DrakeExtensions
    {
        public static TaskTracker GetTaskTracker(this Drake drake)
        {
            // This would need to be exposed on Drake class
            // For now, this is just for demonstration
            return new TaskTracker();
        }
    }
}
