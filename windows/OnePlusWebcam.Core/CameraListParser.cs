using System.Globalization;
using System.Text.RegularExpressions;

namespace OnePlusWebcam;

public static partial class CameraListParser
{
    public static IReadOnlyList<CameraInfo> Parse(string scrcpyListCamerasOutput)
    {
        var cameras = new List<CameraInfo>();
        if (string.IsNullOrWhiteSpace(scrcpyListCamerasOutput))
        {
            return cameras;
        }

        foreach (var rawLine in scrcpyListCamerasOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var idMatch = CameraIdRegex().Match(line);
            if (!idMatch.Success)
            {
                continue;
            }

            var facingMatch = FacingRegex().Match(line);
            var facing = facingMatch.Success ? facingMatch.Groups[1].Value : "unknown";

            var sizeMatch = SizeRegex().Match(line);
            var maxSize = sizeMatch.Success ? sizeMatch.Value : "";

            double zoomMin = 1;
            double zoomMax = 1;
            var zoomMatch = ZoomRegex().Match(line);
            if (zoomMatch.Success)
            {
                zoomMin = double.Parse(zoomMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                zoomMax = double.Parse(zoomMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            cameras.Add(new CameraInfo(idMatch.Groups[1].Value, facing, maxSize, zoomMin, zoomMax));
        }

        return cameras;
    }

    [GeneratedRegex(@"--camera-id=([0-9]+)")]
    private static partial Regex CameraIdRegex();

    [GeneratedRegex(@"\((back|front|external)")]
    private static partial Regex FacingRegex();

    [GeneratedRegex(@"\d+x\d+")]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"zoom-range=\[([0-9.]+),\s*([0-9.]+)\]")]
    private static partial Regex ZoomRegex();
}
