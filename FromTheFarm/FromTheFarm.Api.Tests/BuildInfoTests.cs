using FromTheFarm.Api.Services;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class BuildInfoTests
{
    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    public void ShortCommit_IsUnknown_WhenNoCommitIsSupplied(string? sha, string expected)
    {
        Assert.Equal(expected, BuildInfo.ShortCommit(sha));
    }

    [Fact]
    public void ShortCommit_TruncatesAFullShaToSevenCharacters()
    {
        Assert.Equal("9cdafe7", BuildInfo.ShortCommit("9cdafe7d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b"));
    }

    [Fact]
    public void ShortCommit_KeepsAnAlreadyShortValue_AndTrimsWhitespace()
    {
        Assert.Equal("abc12", BuildInfo.ShortCommit("  abc12\n"));
    }
}
