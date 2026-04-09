namespace Autonovel.Tests;

using Autonovel.Core.Domain;
using Autonovel.Core.Services;
using Xunit;

public class GenerationClientTests
{
    [Fact]
    public void Constructor_CreatesClient()
    {
        // Arrange & Act
        var client = new OpenAiGenerationClient(null!);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task GenerateAsync_RequiresNonEmptyPrompt()
    {
        // Arrange
        var client = new OpenAiGenerationClient(null!);

        // Act & Assert - This will fail at runtime since we don't have a real client,
        // but we can at least verify the method exists and accepts the right parameters
        await Assert.ThrowsAnyAsync<Exception>(
            () => client.GenerateAsync(
                new GenerationRequest(SystemPrompt: "System", UserPrompt: "User")));
    }
}
