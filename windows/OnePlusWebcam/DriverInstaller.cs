using System.ComponentModel;
using System.Diagnostics;

namespace OnePlusWebcam;

internal static class DriverInstaller
{
    public static bool TryElevateRegister(ToolPaths tools)
    {
        var dir = tools.AkvcamDir;
        var cmd = Path.Combine(dir, "register-vcam.cmd");
        if (!File.Exists(cmd))
        {
            return false;
        }

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = "/c \"" + cmd + "\"",
            WorkingDirectory = dir,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(120_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
