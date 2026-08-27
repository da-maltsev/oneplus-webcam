using System.Text.RegularExpressions;

namespace OnePlusWebcam;

public static partial class ScrcpyVersionParser
{
    public const string BundledVersion = "4.1";

    public static string Parse(string scrcpyVersionStdout, out bool usedFallback)
    {
        usedFallback = false;
        if (!string.IsNullOrWhiteSpace(scrcpyVersionStdout))
        {
            var first = scrcpyVersionStdout.Split('\n')[0].TrimEnd('\r');
            var match = VersionRegex().Match(first);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        usedFallback = true;
        return BundledVersion;
    }

    [GeneratedRegex(@"^scrcpy\s+(\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex VersionRegex();
}
