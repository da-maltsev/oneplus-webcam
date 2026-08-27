using OnePlusWebcam;
using Xunit;

namespace OnePlusWebcam.Tests;

public class CameraListParserTests
{
    [Fact]
    public void Parse_PlanFixture()
    {
        const string output = """
            [server] INFO: List of cameras:
            --camera-id=0    (back, 4000x3000, fps=[15, 24, 30, 60], zoom-range=[1.0, 20.0])
            --camera-id=1    (front, 3264x2448, fps=[15, 24, 30], zoom-range=[1.0, 10.0])
            --camera-id=2    (back, 1920x1080, fps=[30])
            """;

        var cameras = CameraListParser.Parse(output);
        Assert.Equal(3, cameras.Count);

        Assert.Equal("0", cameras[0].Id);
        Assert.Equal("back", cameras[0].Facing);
        Assert.Equal("4000x3000", cameras[0].MaxSize);
        Assert.Equal(1.0, cameras[0].ZoomMin);
        Assert.Equal(20.0, cameras[0].ZoomMax);

        Assert.Equal("1", cameras[1].Id);
        Assert.Equal("front", cameras[1].Facing);
        Assert.Equal("3264x2448", cameras[1].MaxSize);
        Assert.Equal(10.0, cameras[1].ZoomMax);

        Assert.Equal("2", cameras[2].Id);
        Assert.Equal(1.0, cameras[2].ZoomMin);
        Assert.Equal(1.0, cameras[2].ZoomMax);
    }
}
