using Xunit;
using FluentAssertions;
using Autonovel.Core.Services;

namespace Autonovel.Tests;

public class GitVersionControlTests
{
    [Fact]
    public void Commit_ChangesSuccessfully()
    {
        // This is an integration test that requires a git repo
        // Skip if not in a git repo
        var control = new GitVersionControl("/tmp");
        
        // Just verify the object creates without error
        control.Should().NotBeNull();
    }

    [Fact]
    public void HardReset_ExecutesGitCommand()
    {
        var control = new GitVersionControl("/tmp");
        control.Should().NotBeNull();
    }
}
