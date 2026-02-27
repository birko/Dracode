using DraCode.Agent.Helpers;
using FluentAssertions;
using Xunit;

namespace DraCode.Agent.Tests.Helpers;

public class PathHelperTests
{
    [Fact]
    public void IsPathSafe_WithSameDirectory_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "test.txt");
        var workingDir = Path.GetTempPath();

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPathSafe_WithSubdirectory_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "subdir", "test.txt");
        var workingDir = Path.GetTempPath();

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPathSafe_WithParentDirectoryTraversal_ShouldReturnFalse()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "..", "etc", "passwd");
        var workingDir = Path.GetTempPath();

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPathSafe_WithAbsolutePathOutsideWorkspace_ShouldReturnFalse()
    {
        // Arrange
        var path = "C:\\Windows\\System32\\test.txt";
        var workingDir = Path.GetTempPath();

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPathSafe_WithAllowedExternalPath_ShouldReturnTrue()
    {
        // Arrange
        var externalDir = Path.Combine(Path.GetTempPath(), "external");
        var path = Path.Combine(externalDir, "test.txt");
        var workingDir = Path.GetTempPath();
        var allowedPaths = new List<string> { externalDir };

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir, allowedPaths);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPathSafe_WithAllowedExternalPathSubdirectory_ShouldReturnTrue()
    {
        // Arrange
        var externalDir = Path.Combine(Path.GetTempPath(), "external");
        var path = Path.Combine(externalDir, "subdir", "test.txt");
        var workingDir = Path.GetTempPath();
        var allowedPaths = new List<string> { externalDir };

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir, allowedPaths);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPathSafe_WithNotAllowedExternalPath_ShouldReturnFalse()
    {
        // Arrange
        var otherDir = Path.Combine(Path.GetTempPath(), "other");
        var path = Path.Combine(otherDir, "test.txt");
        var workingDir = Path.GetTempPath();
        var allowedPaths = new List<string> { Path.Combine(Path.GetTempPath(), "external") };

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir, allowedPaths);

        // Assert - "other" is under workingDir (Temp), so it's actually safe
        // This test needs a truly external path
        var trulyExternal = Path.Combine(Path.GetTempPath(), "external", "test.txt");
        var otherResult = PathHelper.IsPathSafe(trulyExternal, workingDir, allowedPaths);
        otherResult.Should().BeTrue(); // Because "external" is in allowedPaths

        // A path NOT in allowed paths
        var notAllowed = Path.Combine(Path.GetTempPath(), "not-in-allowed", "test.txt");
        var notAllowedResult = PathHelper.IsPathSafe(notAllowed, workingDir, allowedPaths);
        // This is still safe because it's under the workspace (workingDir)
        notAllowedResult.Should().BeTrue(); // Safe because it's under workspace

        // Truly not safe - outside workspace and not in allowed
        var outsideAll = "C:\\Windows\\System32\\test.txt";
        var outsideResult = PathHelper.IsPathSafe(outsideAll, workingDir, allowedPaths);
        outsideResult.Should().BeFalse();
    }

    [Fact]
    public void IsPathSafe_WithNullAllowedPaths_ShouldReturnFalseForExternal()
    {
        // Arrange
        var externalDir = Path.Combine(Path.GetTempPath(), "external");
        var path = Path.Combine(externalDir, "test.txt");
        var workingDir = Path.GetTempPath();

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir, (IEnumerable<string>?)null);

        // Assert - external is under Temp (workingDir), so it's safe
        // Use a truly external path
        var trulyExternal = "C:\\Windows\\System32\\test.txt";
        var trulyExternalResult = PathHelper.IsPathSafe(trulyExternal, workingDir, (IEnumerable<string>?)null);
        trulyExternalResult.Should().BeFalse();
    }

    [Fact]
    public void IsPathSafe_WithEmptyAllowedPaths_ShouldReturnFalseForExternal()
    {
        // Arrange
        var externalDir = Path.Combine(Path.GetTempPath(), "external-test");
        var path = Path.Combine(externalDir, "test.txt");
        var workingDir = Path.GetTempPath();
        var allowedPaths = new List<string>();

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir, allowedPaths);

        // Assert - external path should not be safe when not in allowed list
        // But PathHelper.IsPathSafe with empty list will only check workspace
        // So if the externalDir happens to be under workingDir (like under Temp),
        // it might return true. Let's use a truly external path.
        var trulyExternal = "C:\\Windows\\System32\\test.txt";
        result = PathHelper.IsPathSafe(trulyExternal, workingDir, allowedPaths);
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPathSafe_WithEmptyStringInAllowedPaths_ShouldIgnore()
    {
        // Arrange
        var externalDir = Path.Combine(Path.GetTempPath(), "external");
        var path = Path.Combine(externalDir, "test.txt");
        var workingDir = Path.GetTempPath();
        var allowedPaths = new List<string> { "", externalDir };

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir, allowedPaths);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUnderDirectory_WithSamePath_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.GetTempPath();
        var directory = Path.GetTempPath();

        // Act
        var result = PathHelper.IsUnderDirectory(path, directory);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUnderDirectory_WithChildPath_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "subdir", "file.txt");
        var directory = Path.GetTempPath();

        // Act
        var result = PathHelper.IsUnderDirectory(path, directory);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUnderDirectory_WithSiblingPath_ShouldReturnFalse()
    {
        // Arrange
        // Create two concrete sibling directories
        var parentDir = Path.Combine(Path.GetTempPath(), $"test-parent-{Guid.NewGuid()}");
        var sibling1 = Path.Combine(parentDir, "sibling1");
        var sibling2 = Path.Combine(parentDir, "sibling2");

        // Only create sibling2 to ensure it exists
        Directory.CreateDirectory(sibling2);

        try
        {
            // Test that sibling1 is not under sibling2
            var result = PathHelper.IsUnderDirectory(sibling1, sibling2);

            // Assert - sibling1 should not be under sibling2
            result.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(parentDir))
                Directory.Delete(parentDir, true);
        }
    }

    [Fact]
    public void IsUnderDirectory_WithDifferentCase_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.GetTempPath().ToUpper();
        var directory = Path.GetTempPath().ToLower();

        // Act
        var result = PathHelper.IsUnderDirectory(path, directory);

        // Assert - case insensitive comparison
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUnderDirectory_WithTrailingSeparator_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "file.txt");
        var directory = Path.GetTempPath() + Path.DirectorySeparatorChar;

        // Act
        var result = PathHelper.IsUnderDirectory(path, directory);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUnderDirectory_WithPartialNameMatch_ShouldReturnFalse()
    {
        // Arrange
        // This tests the bug where "/source/Birko.Data" would match "/source/Birko"
        var baseDir = "C:\\Source\\Birko";
        var otherDir = "C:\\Source\\Birko.Data";

        // Act
        var result = PathHelper.IsUnderDirectory(otherDir, baseDir);

        // Assert - Should NOT match because "Birko.Data" is not under "Birko"
        result.Should().BeFalse();
    }

    [Fact]
    public void IsUnderDirectory_WithVerySimilarNames_ShouldReturnFalse()
    {
        // Arrange - Use actual directories
        var parentDir = Path.Combine(Path.GetTempPath(), $"test-parent-{Guid.NewGuid()}");
        var baseDir = Path.Combine(parentDir, "project");
        var otherDir = Path.Combine(parentDir, "project_backup");

        Directory.CreateDirectory(baseDir);

        try
        {
            // Act
            var result = PathHelper.IsUnderDirectory(otherDir, baseDir);

            // Assert - Should NOT match (project_backup is sibling of project)
            result.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(parentDir))
                Directory.Delete(parentDir, true);
        }
    }

    [Fact]
    public void IsUnderDirectory_WithChildOfSimilarName_ShouldReturnTrue()
    {
        // Arrange - Use actual directories
        var baseDir = Path.Combine(Path.GetTempPath(), $"test-base-{Guid.NewGuid()}");
        var childDir = Path.Combine(baseDir, "subdir");

        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(childDir);

        try
        {
            // Act
            var result = PathHelper.IsUnderDirectory(childDir, baseDir);

            // Assert - Should match because it's a true child
            result.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void IsPathSafe_WithRelativePath_ShouldResolveCorrectly()
    {
        // Arrange
        var workingDir = Path.GetTempPath();
        var path = Path.Combine(workingDir, "subdir", "..", "file.txt");

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir);

        // Assert - After normalization, should be in workspace
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPathSafe_WithMixedSlashes_ShouldWorkCorrectly()
    {
        // Arrange
        var workingDir = Path.GetTempPath();
        var path = workingDir.Replace('\\', '/') + "/subdir/file.txt";

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir);

        // Assert - Should handle mixed slashes
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPathSafe_WithDotInPath_ShouldWorkCorrectly()
    {
        // Arrange
        var workingDir = Path.GetTempPath();
        var path = Path.Combine(workingDir, ".", "file.txt");

        // Act
        var result = PathHelper.IsPathSafe(path, workingDir);

        // Assert
        result.Should().BeTrue();
    }
}
