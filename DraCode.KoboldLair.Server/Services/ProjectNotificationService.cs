using System.Collections.Concurrent;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Notification service for project events with persistence.
    /// Notifications are stored to disk so they survive server restarts
    /// and are available when the user reconnects.
    /// </summary>
    public class ProjectNotificationService
    {
        private readonly ILogger<ProjectNotificationService> _logger;
        private readonly string _projectsPath;
        private readonly ConcurrentDictionary<string, List<ProjectNotification>> _pendingNotifications = new();

        /// <summary>
        /// Raised when a new notification is added (for real-time push to connected clients).
        /// Parameters: projectName, notification
        /// </summary>
        public event Action<string, ProjectNotification>? OnNotification;

        public ProjectNotificationService(ILogger<ProjectNotificationService> logger, string projectsPath)
        {
            _logger = logger;
            _projectsPath = projectsPath;
        }

        /// <summary>
        /// Adds a notification for a project. Persists to disk and fires event.
        /// </summary>
        public void Notify(string projectName, string type, string message, Dictionary<string, string>? metadata = null)
        {
            var notification = new ProjectNotification
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Message = message,
                Metadata = metadata ?? new(),
                CreatedAt = DateTime.UtcNow
            };

            // Add to in-memory pending list
            var list = _pendingNotifications.GetOrAdd(projectName, _ => new List<ProjectNotification>());
            lock (list)
            {
                list.Add(notification);
            }

            // Persist to disk
            SaveNotifications(projectName, list);

            // Fire event for real-time push
            try
            {
                OnNotification?.Invoke(projectName, notification);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error firing notification event for project {Project}", projectName);
            }

            _logger.LogInformation("Notification [{Type}] for project {Project}: {Message}", type, projectName, message);
        }

        /// <summary>
        /// Gets all pending (unread) notifications for a project.
        /// </summary>
        public List<ProjectNotification> GetPendingNotifications(string projectName)
        {
            // Try in-memory first
            if (_pendingNotifications.TryGetValue(projectName, out var list))
            {
                lock (list)
                {
                    return new List<ProjectNotification>(list);
                }
            }

            // Load from disk
            var loaded = LoadNotifications(projectName);
            if (loaded.Count > 0)
            {
                _pendingNotifications[projectName] = loaded;
            }
            return loaded;
        }

        /// <summary>
        /// Marks notifications as read (removes them).
        /// </summary>
        public void MarkAsRead(string projectName, IEnumerable<string>? notificationIds = null)
        {
            if (_pendingNotifications.TryGetValue(projectName, out var list))
            {
                lock (list)
                {
                    if (notificationIds == null)
                    {
                        list.Clear();
                    }
                    else
                    {
                        var ids = new HashSet<string>(notificationIds);
                        list.RemoveAll(n => ids.Contains(n.Id));
                    }
                }
                SaveNotifications(projectName, list);
            }
        }

        /// <summary>
        /// Helper to create a feature-branch-ready notification.
        /// </summary>
        public void NotifyFeatureBranchReady(string projectName, string featureName, string branchName)
        {
            Notify(projectName, "feature_branch_ready",
                $"Feature \"{featureName}\" is complete! Branch `{branchName}` is ready for merge into main.",
                new Dictionary<string, string>
                {
                    ["featureName"] = featureName,
                    ["branchName"] = branchName
                });
        }

        /// <summary>
        /// Helper to create a project-complete notification.
        /// </summary>
        public void NotifyProjectComplete(string projectName)
        {
            Notify(projectName, "project_complete",
                $"All features for project \"{projectName}\" are complete! Review branches and merge when ready.");
        }

        /// <summary>
        /// Persists all in-memory notifications to disk. Called during graceful shutdown.
        /// </summary>
        public void PersistAll()
        {
            foreach (var (projectName, list) in _pendingNotifications)
            {
                if (list.Count > 0)
                {
                    SaveNotifications(projectName, list);
                }
            }
        }

        private string GetNotificationsPath(string projectName)
        {
            var sanitized = projectName.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("\\", "-");
            return Path.Combine(_projectsPath, sanitized, "notifications.json");
        }

        private void SaveNotifications(string projectName, List<ProjectNotification> notifications)
        {
            try
            {
                var path = GetNotificationsPath(projectName);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                List<ProjectNotification> copy;
                lock (notifications)
                {
                    copy = new List<ProjectNotification>(notifications);
                }

                var json = JsonSerializer.Serialize(copy, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save notifications for project {Project}", projectName);
            }
        }

        private List<ProjectNotification> LoadNotifications(string projectName)
        {
            try
            {
                var path = GetNotificationsPath(projectName);
                if (!File.Exists(path))
                    return new List<ProjectNotification>();

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ProjectNotification>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ProjectNotification>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load notifications for project {Project}", projectName);
                return new List<ProjectNotification>();
            }
        }
    }

    public class ProjectNotification
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
