using EstimatorMcp.Web.Services;
using Xunit;

namespace EstimatorMcp.Tests;

public class ServerVersionInfoTests
{
    [Fact]
    public void Parse_StableCiBuild_SplitsVersionAndCommit()
    {
        var info = ServerVersionInfo.Parse("0.1.0+abc1234");

        Assert.Equal("0.1.0", info.Semantic);
        Assert.Equal("abc1234", info.Commit);
        Assert.Equal("0.1.0+abc1234", info.Full);
    }

    [Fact]
    public void Parse_LocalBuildWithoutMetadata_HasNoCommit()
    {
        var info = ServerVersionInfo.Parse("0.1.0");

        Assert.Equal("0.1.0", info.Semantic);
        Assert.Null(info.Commit);
        Assert.Equal("0.1.0", info.Full);
    }

    [Fact]
    public void Parse_PrereleaseBuild_KeepsSuffixInSemanticVersion()
    {
        // The '-rc.1' label is part of the semantic version; only '+' starts
        // build metadata. Splitting on '-' here would be wrong.
        var info = ServerVersionInfo.Parse("0.2.0-rc.1+abc1234");

        Assert.Equal("0.2.0-rc.1", info.Semantic);
        Assert.Equal("abc1234", info.Commit);
    }

    [Fact]
    public void Parse_PrereleaseWithoutMetadata_KeepsSuffix()
    {
        var info = ServerVersionInfo.Parse("0.2.0-rc.1");

        Assert.Equal("0.2.0-rc.1", info.Semantic);
        Assert.Null(info.Commit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_MissingVersion_ReportsUnknown(string? value)
    {
        var info = ServerVersionInfo.Parse(value);

        Assert.Equal("unknown", info.Semantic);
        Assert.Null(info.Commit);
    }

    [Fact]
    public void Parse_TrailingPlusWithNoMetadata_TreatedAsNoCommit()
    {
        var info = ServerVersionInfo.Parse("0.1.0+");

        Assert.Equal("0.1.0", info.Semantic);
        Assert.Null(info.Commit);
    }

    [Fact]
    public void Current_ReadsVersionFromRunningAssembly()
    {
        // Guards the attribute lookup itself: if Directory.Build.props stopped
        // flowing a version into the assembly, this would fall back to "unknown".
        Assert.NotEqual("unknown", ServerVersionInfo.Current.Semantic);
    }
}
