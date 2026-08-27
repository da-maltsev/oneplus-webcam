using OnePlusWebcam;
using Xunit;

namespace OnePlusWebcam.Tests;

public class ScrcpyVersionParserTests
{
    [Fact]
    public void Parse_FirstLine()
    {
        const string output = """
            scrcpy 4.1 <https://github.com/Genymobile/scrcpy>

            Dependencies (compiled / linked):
             - SDL: 3.4.12 / 3.4.12
            """;

        var version = ScrcpyVersionParser.Parse(output, out var usedFallback);
        Assert.Equal("4.1", version);
        Assert.False(usedFallback);
    }

    [Fact]
    public void Parse_FallsBackToBundled()
    {
        var version = ScrcpyVersionParser.Parse("not a version", out var usedFallback);
        Assert.Equal(ScrcpyVersionParser.BundledVersion, version);
        Assert.True(usedFallback);
    }
}
