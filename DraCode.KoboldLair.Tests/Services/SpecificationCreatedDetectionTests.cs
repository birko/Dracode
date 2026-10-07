using DraCode.KoboldLair.Server.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Dragon announces <c>specification_created</c> only for a specification written during the request. It used to compare
/// a local file time with <see cref="DateTime.UtcNow"/>, so east of Greenwich every existing spec looked brand new and was
/// announced after every response — including a failed one (TASK-085 / FIELD-005).
/// </summary>
public class SpecificationCreatedDetectionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_spec_detect_{Guid.NewGuid():N}");

    public SpecificationCreatedDetectionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string SpecWrittenAt(string project, DateTime writtenUtc)
    {
        var path = Path.Combine(_dir, project, "specification.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# spec");
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }

    [Fact]
    public void A_specification_written_before_the_request_is_not_announced()
    {
        // Two hours old: the old local-vs-UTC comparison read this as "under 5 seconds old" on a UTC+2 machine
        var requestStarted = DateTime.UtcNow;
        SpecWrittenAt("old-project", requestStarted.AddHours(-2));

        DragonService.FindSpecificationWrittenSince(_dir, requestStarted).Should().BeNull();
    }

    [Fact]
    public void A_specification_written_during_the_request_is_announced()
    {
        var requestStarted = DateTime.UtcNow.AddSeconds(-3);
        SpecWrittenAt("old-project", requestStarted.AddHours(-2));
        var created = SpecWrittenAt("new-project", requestStarted.AddSeconds(2));

        DragonService.FindSpecificationWrittenSince(_dir, requestStarted).Should().Be(created);
    }

    [Fact]
    public void No_projects_folder_means_nothing_to_announce()
    {
        DragonService.FindSpecificationWrittenSince(Path.Combine(_dir, "missing"), DateTime.UtcNow).Should().BeNull();
    }
}
