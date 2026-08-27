using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace OnePlusWebcam;

internal static class DriverInstaller
{
    private const int ErrorCancelled = 1223;

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static ElevationOutcome TryElevateRegister()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            return new ElevationOutcome(false, false, -1);
        }

        var alreadyAdmin = IsAdministrator();
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = PipelineCommands.RegisterDriverArgument,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
            ErrorDialog = true,
        };
        if (!alreadyAdmin)
        {
            psi.Verb = "runas";
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return new ElevationOutcome(false, false, -1);
            }

            if (!process.WaitForExit(180_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return new ElevationOutcome(false, false, -2);
            }

            return new ElevationOutcome(process.ExitCode == 0, false, process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return new ElevationOutcome(false, true, ErrorCancelled);
        }
        catch (Win32Exception)
        {
            return new ElevationOutcome(false, false, -1);
        }
    }
}

internal readonly record struct ElevationOutcome(bool Ok, bool Cancelled, int ExitCode);
