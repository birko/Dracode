using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Tests for EntityMapper bidirectional mapping between domain models and DB entities.
/// </summary>
public class EntityMapperTests
{
    [Fact]
    public void Project_RoundTrip_ShouldPreserveAllFields()
    {
        var project = new Project
        {
            Id = "test-id-123",
            Name = "My Project",
            Status = ProjectStatus.InProgress,
            ExecutionState = ProjectExecutionState.Paused,
            VerificationStatus = VerificationStatus.InProgress,
            Timestamps = new ProjectTimestamps
            {
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc),
                AnalyzedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            Paths = new ProjectPaths
            {
                Specification = "./myproj/spec.md",
                Output = "./myproj/workspace",
                Analysis = "./myproj/analysis.json",
                TaskFiles = new Dictionary<string, string>
                {
                    ["backend"] = "./myproj/tasks/backend-tasks.md",
                    ["frontend"] = "./myproj/tasks/frontend-tasks.md"
                }
            },
            Tracking = new ProjectTracking
            {
                SpecificationId = "spec-1",
                WyvernId = "wyv-1",
                ErrorMessage = "test error",
                PendingAreas = new List<string> { "backend", "frontend" }
            },
            Agents = new AgentsConfig
            {
                Kobold = new AgentConfig { Enabled = true, Provider = "claude", MaxParallel = 4 }
            },
            Security = new SecurityConfig
            {
                SandboxMode = "relaxed",
                AllowedExternalPaths = new List<string> { "C:\\Source" }
            },
            Metadata = new Dictionary<string, string> { ["key"] = "val" }
        };

        var entity = EntityMapper.ToEntity(project);
        var roundTripped = EntityMapper.ToProject(entity);

        roundTripped.Id.Should().Be(project.Id);
        roundTripped.Name.Should().Be(project.Name);
        roundTripped.Status.Should().Be(ProjectStatus.InProgress);
        roundTripped.ExecutionState.Should().Be(ProjectExecutionState.Paused);
        roundTripped.Timestamps.CreatedAt.Should().Be(project.Timestamps.CreatedAt);
        roundTripped.Timestamps.AnalyzedAt.Should().Be(project.Timestamps.AnalyzedAt);
        roundTripped.Paths.Specification.Should().Be("./myproj/spec.md");
        roundTripped.Paths.TaskFiles.Should().HaveCount(2);
        roundTripped.Tracking.SpecificationId.Should().Be("spec-1");
        roundTripped.Tracking.PendingAreas.Should().BeEquivalentTo(new[] { "backend", "frontend" });
        roundTripped.Agents.Kobold.Provider.Should().Be("claude");
        roundTripped.Agents.Kobold.MaxParallel.Should().Be(4);
        roundTripped.Security.SandboxMode.Should().Be("relaxed");
        roundTripped.Security.AllowedExternalPaths.Should().Contain("C:\\Source");
        roundTripped.Metadata["key"].Should().Be("val");
    }

    [Fact]
    public void TaskRecord_RoundTrip_ShouldPreserveAllFields()
    {
        var task = new TaskRecord
        {
            Id = "task-id-456",
            Task = "Implement login (depends on: backend-1, backend-2)",
            AssignedAgent = "typescript",
            ProjectId = "proj-1",
            Status = TaskStatus.Working,
            Priority = TaskPriority.High,
            CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            ErrorMessage = "timeout",
            ErrorCategory = "Transient",
            SpecificationVersion = 3,
            CommitSha = "abc123def",
            FeatureId = "feat-1",
            RetryCount = 2,
            Provider = "openai",
            Dependencies = new List<string> { "backend-1", "backend-2" },
            OutputFiles = new List<string> { "src/login.ts", "src/login.css" }
        };

        var entity = EntityMapper.ToEntity(task, "frontend");
        var roundTripped = EntityMapper.ToTaskRecord(entity);

        roundTripped.Id.Should().Be(task.Id);
        roundTripped.Task.Should().Be(task.Task);
        roundTripped.AssignedAgent.Should().Be("typescript");
        roundTripped.Status.Should().Be(TaskStatus.Working);
        roundTripped.Priority.Should().Be(TaskPriority.High);
        roundTripped.ErrorMessage.Should().Be("timeout");
        roundTripped.ErrorCategory.Should().Be("Transient");
        roundTripped.CommitSha.Should().Be("abc123def");
        roundTripped.RetryCount.Should().Be(2);
        roundTripped.Dependencies.Should().BeEquivalentTo(new[] { "backend-1", "backend-2" });
        roundTripped.OutputFiles.Should().BeEquivalentTo(new[] { "src/login.ts", "src/login.css" });

        // Check entity area name
        entity.AreaName.Should().Be("frontend");
    }

    [Fact]
    public void UpdateEntity_ShouldPreserveGuid()
    {
        var project = new Project { Id = "proj-1", Name = "original" };
        var entity = EntityMapper.ToEntity(project);
        var originalGuid = entity.Guid;

        project.Name = "updated";
        project.Status = ProjectStatus.Completed;
        EntityMapper.UpdateEntity(entity, project);

        entity.Guid.Should().Be(originalGuid);
        entity.Name.Should().Be("updated");
        entity.Status.Should().Be((int)ProjectStatus.Completed);
    }
}
