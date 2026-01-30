using DraCode.Agent;
using DraCode.KoboldLair.Server.Agents;
using DraCode.KoboldLair.Server.Models.Tasks;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Server.Orchestrators;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TaskStatus = DraCode.KoboldLair.Server.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Services
{
    public class WyrmService
    {
        private readonly TaskTracker _taskTracker;
        private readonly ILogger<WyrmService> _logger;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly WebSocketCommandHandler? _commandHandler;

        public WyrmService(
            ILogger<WyrmService> logger,
            ProviderConfigurationService providerConfigService,
            WebSocketCommandHandler? commandHandler = null)
        {
            _logger = logger;
            _taskTracker = new TaskTracker();
            _providerConfigService = providerConfigService;
            _commandHandler = commandHandler;
        }

        public TaskTracker TaskTracker => _taskTracker;

        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Wyrm WebSocket close received");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogDebug("Wyrm received message ({Count} bytes): {Message}", result.Count, message);

                    await ProcessMessageAsync(webSocket, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket error");
                if (webSocket.State == WebSocketState.Open)
                {
                    await SendErrorAsync(webSocket, ex.Message);
                }
            }
        }

        private async Task ProcessMessageAsync(WebSocket webSocket, string message)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var request = JsonSerializer.Deserialize<WebSocketRequest>(message, options);
                if (request == null)
                {
                    await SendErrorAsync(webSocket, "Invalid request format");
                    return;
                }

                // Support both 'action' and 'command' fields
                var actionOrCommand = (request.Action ?? request.Command)?.ToLowerInvariant();

                switch (actionOrCommand)
                {
                    case "ping":
                        await SendMessageAsync(webSocket, new { type = "pong" });
                        break;

                    case "submit_task":
                        await HandleSubmitTaskAsync(webSocket, request);
                        break;

                    case "get_tasks":
                        await HandleGetTasksAsync(webSocket);
                        break;

                    case "get_task":
                        await HandleGetTaskAsync(webSocket, request);
                        break;

                    case "get_markdown":
                        await HandleGetMarkdownAsync(webSocket);
                        break;

                    default:
                        // Try to handle as API command if command handler is available
                        if (_commandHandler != null && actionOrCommand != null)
                        {
                            await _commandHandler.HandleCommandAsync(webSocket, message);
                        }
                        else
                        {
                            await SendErrorAsync(webSocket, $"Unknown action: {actionOrCommand}");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await SendErrorAsync(webSocket, ex.Message);
            }
        }

        private async Task HandleSubmitTaskAsync(WebSocket webSocket, WebSocketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Task))
            {
                await SendErrorAsync(webSocket, "Task description is required");
                return;
            }

            var taskRecord = _taskTracker.AddTask(request.Task);

            await SendMessageAsync(webSocket, new
            {
                type = "task_created",
                taskId = taskRecord.Id,
                task = taskRecord.Task,
                status = taskRecord.Status.ToString().ToLower()
            });

            // Run Wyrm in background
            _ = Task.Run(async () => await RunWyrmAsync(webSocket, taskRecord));
        }

        private async Task RunWyrmAsync(WebSocket webSocket, TaskRecord taskRecord)
        {
            try
            {
                var options = new AgentOptions
                {
                    WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "workspace"),
                    Verbose = true,
                    MaxIterations = 10
                };

                // Message callback to send real-time updates and log to console
                Action<string, string> messageCallback = async (type, content) =>
                {
                    // Log all agent messages to console for debugging
                    _logger.LogInformation("[Wyrm Agent] [{Type}] {Content}", type, content);

                    if (webSocket.State == WebSocketState.Open)
                    {
                        await SendMessageAsync(webSocket, new
                        {
                            type = "agent_message",
                            taskId = taskRecord.Id,
                            messageType = type,
                            content
                        });
                    }
                };

                // Get provider settings from configuration
                var (provider, config, _) = _providerConfigService.GetProviderSettingsForAgent("wyvern", options.WorkingDirectory);

                // Get recommendation first
                var result = await WyrmRunner.GetRecommendationAsync(
                    provider,
                    taskRecord.Task,
                    options,
                    config,
                    messageCallback
                );

                var agentType = result.selectedAgentType;
                var reasoning = result.reasoning;

                if (agentType != null)
                {
                    _taskTracker.UpdateTask(taskRecord, TaskStatus.NotInitialized, agentType);
                    await SendStatusUpdateAsync(webSocket, taskRecord);

                    // For demo purposes, mark as done after a short delay
                    // In production, you would actually run the agent
                    await Task.Delay(2000);

                    _taskTracker.UpdateTask(taskRecord, TaskStatus.Working);
                    await SendStatusUpdateAsync(webSocket, taskRecord);

                    await Task.Delay(3000);

                    _taskTracker.UpdateTask(taskRecord, TaskStatus.Done);
                    await SendStatusUpdateAsync(webSocket, taskRecord);
                }
                else
                {
                    _taskTracker.SetError(taskRecord, "Failed to select agent");
                    await SendStatusUpdateAsync(webSocket, taskRecord);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running Wyrm for task {TaskId}", taskRecord.Id);
                _taskTracker.SetError(taskRecord, ex.Message);
                await SendStatusUpdateAsync(webSocket, taskRecord);
            }
        }

        private async Task HandleGetTasksAsync(WebSocket webSocket)
        {
            var tasks = _taskTracker.GetAllTasks();
            await SendMessageAsync(webSocket, new
            {
                type = "tasks_list",
                tasks = tasks.Select(t => new
                {
                    id = t.Id,
                    task = t.Task,
                    assignedAgent = t.AssignedAgent,
                    status = t.Status.ToString().ToLower(),
                    createdAt = t.CreatedAt,
                    updatedAt = t.UpdatedAt,
                    errorMessage = t.ErrorMessage
                })
            });
        }

        private async Task HandleGetTaskAsync(WebSocket webSocket, WebSocketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TaskId))
            {
                await SendErrorAsync(webSocket, "Task ID is required");
                return;
            }

            var task = _taskTracker.GetTaskById(request.TaskId);
            if (task == null)
            {
                await SendErrorAsync(webSocket, $"Task not found: {request.TaskId}");
                return;
            }

            await SendMessageAsync(webSocket, new
            {
                type = "task_detail",
                id = task.Id,
                task = task.Task,
                assignedAgent = task.AssignedAgent,
                status = task.Status.ToString().ToLower(),
                createdAt = task.CreatedAt,
                updatedAt = task.UpdatedAt,
                errorMessage = task.ErrorMessage
            });
        }

        private async Task HandleGetMarkdownAsync(WebSocket webSocket)
        {
            var markdown = _taskTracker.GenerateMarkdown("KoboldLair Wyrm Tasks");
            await SendMessageAsync(webSocket, new
            {
                type = "markdown_report",
                markdown
            });
        }

        private async Task SendStatusUpdateAsync(WebSocket webSocket, TaskRecord task)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await SendMessageAsync(webSocket, new
                {
                    type = "status_update",
                    taskId = task.Id,
                    status = task.Status.ToString().ToLower(),
                    assignedAgent = task.AssignedAgent,
                    errorMessage = task.ErrorMessage
                });
            }
        }

        private async Task SendMessageAsync(WebSocket webSocket, object data)
        {
            if (webSocket.State != WebSocketState.Open) return;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(data, options);
            _logger.LogDebug("Wyrm sending message: {Message}", json);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendErrorAsync(WebSocket webSocket, string error)
        {
            await SendMessageAsync(webSocket, new { type = "error", error });
        }
    }
}
