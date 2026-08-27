using System.Diagnostics;

namespace OnePlusWebcam;

internal static class DriverRegistration
{
    public static async Task<int> RunAsync(Action<string> log, CancellationToken cancellationToken)
    {
        if (!DriverInstaller.IsAdministrator())
        {
            log("This process is not running as administrator.");
            return 1;
        }

        var installDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        var tools = new ToolPaths(installDir);
        var sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        Directory.CreateDirectory(tools.AkvcamDir);

        var setup = FindVendorSetup(tools.AkvcamDir);
        if (setup is not null)
        {
            log("Running virtual camera installer (silent)...");
            var silent = RunVisible(setup, "/S", Path.GetDirectoryName(setup) ?? tools.AkvcamDir, TimeSpan.FromMinutes(3), log);
            log("Silent installer exit code: " + silent);
        }
        else
        {
            log("Vendor installer not found next to the app.");
        }

        CopyVendorTools(tools.AkvcamDir, log);

        if (!await WaitForServiceAsync(sc, TimeSpan.FromSeconds(5), log, cancellationToken).ConfigureAwait(false))
        {
            foreach (var assistant in FindAssistants(tools.AkvcamDir).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                log("Installing service: " + assistant);
                var install = RunCaptured(assistant, "--install", Path.GetDirectoryName(assistant) ?? tools.AkvcamDir, TimeSpan.FromSeconds(30), log);
                log("AkVCamAssistant --install exit code: " + install.ExitCode);
                if (!string.IsNullOrWhiteSpace(install.Output))
                {
                    log(install.Output.Trim());
                }
            }
        }

        log("Starting " + PipelineCommands.AssistantServiceName + "...");
        _ = RunCaptured(sc, "start " + PipelineCommands.AssistantServiceName, "", TimeSpan.FromSeconds(20), log);

        var running = await WaitForServiceAsync(sc, TimeSpan.FromSeconds(20), log, cancellationToken).ConfigureAwait(false);
        if (!running && setup is not null)
        {
            log("Silent install did not start the service. Opening the vendor installer window...");
            _ = RunVisible(setup, "", Path.GetDirectoryName(setup) ?? tools.AkvcamDir, TimeSpan.FromMinutes(5), log);
            CopyVendorTools(tools.AkvcamDir, log);
            foreach (var assistant in FindAssistants(tools.AkvcamDir).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                log("Installing service: " + assistant);
                _ = RunCaptured(assistant, "--install", Path.GetDirectoryName(assistant) ?? tools.AkvcamDir, TimeSpan.FromSeconds(30), log);
            }

            _ = RunCaptured(sc, "start " + PipelineCommands.AssistantServiceName, "", TimeSpan.FromSeconds(20), log);
            running = await WaitForServiceAsync(sc, TimeSpan.FromSeconds(20), log, cancellationToken).ConfigureAwait(false);
        }

        var manager = FindManager(tools.AkvcamDir);
        if (running && manager is not null && File.Exists(tools.VcamIni))
        {
            log("Loading OnePlus Webcam device list...");
            var load = RunCaptured(manager, "load \"" + tools.VcamIni + "\"", Path.GetDirectoryName(manager) ?? "", TimeSpan.FromSeconds(20), log);
            log("load exit code: " + load.ExitCode);
            if (!string.IsNullOrWhiteSpace(load.Output))
            {
                log(load.Output.Trim());
            }

            _ = RunCaptured(manager, "set-page-size 128000000", Path.GetDirectoryName(manager) ?? "", TimeSpan.FromSeconds(15), log);
            _ = RunCaptured(manager, "update", Path.GetDirectoryName(manager) ?? "", TimeSpan.FromSeconds(15), log);
        }
        else if (manager is null)
        {
            log("AkVCamManager.exe was not found.");
        }

        running = await WaitForServiceAsync(sc, TimeSpan.FromSeconds(5), log, cancellationToken).ConfigureAwait(false);
        if (!running)
        {
            log("AkVCamAssistant service is still not running.");
            return 2;
        }

        log("AkVCamAssistant is running.");
        return 0;
    }

    private static async Task<bool> WaitForServiceAsync(
        string sc,
        TimeSpan timeout,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = RunCaptured(sc, "query " + PipelineCommands.AssistantServiceName, "", TimeSpan.FromSeconds(5), log: null);
            if (PipelineCommands.ScQueryReportsRunning(query.Output))
            {
                log("Service is RUNNING.");
                return true;
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static string? FindVendorSetup(string akvcamDir)
    {
        string[] names =
        [
            Path.Combine(akvcamDir, "akvirtualcamera-setup.exe"),
            Path.Combine(akvcamDir, "akvirtualcamera-windows-9.4.1.exe"),
        ];
        return names.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> FindAssistants(string akvcamDir)
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[] candidates =
        [
            Path.Combine(akvcamDir, "AkVCamAssistant.exe"),
            Path.Combine(akvcamDir, "x64", "AkVCamAssistant.exe"),
            Path.Combine(pf, "AkVirtualCamera", "x64", "AkVCamAssistant.exe"),
            Path.Combine(pf, "akvirtualcamera", "x64", "AkVCamAssistant.exe"),
            Path.Combine(pf86, "AkVirtualCamera", "x86", "AkVCamAssistant.exe"),
            Path.Combine(pf86, "akvirtualcamera", "x86", "AkVCamAssistant.exe"),
        ];
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                yield return path;
            }
        }
    }

    private static string? FindManager(string akvcamDir)
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] candidates =
        [
            Path.Combine(akvcamDir, "AkVCamManager.exe"),
            Path.Combine(akvcamDir, "x64", "AkVCamManager.exe"),
            Path.Combine(pf, "AkVirtualCamera", "x64", "AkVCamManager.exe"),
            Path.Combine(pf, "akvirtualcamera", "x64", "AkVCamManager.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void CopyVendorTools(string akvcamDir, Action<string> log)
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var x64 = Path.Combine(pf, "AkVirtualCamera", "x64");
        if (!Directory.Exists(x64))
        {
            x64 = Path.Combine(pf, "akvirtualcamera", "x64");
        }

        if (!Directory.Exists(x64))
        {
            return;
        }

        Directory.CreateDirectory(akvcamDir);
        foreach (var name in new[] { "AkVCamAssistant.exe", "AkVCamManager.exe" })
        {
            var src = Path.Combine(x64, name);
            if (!File.Exists(src))
            {
                continue;
            }

            var dest = Path.Combine(akvcamDir, name);
            File.Copy(src, dest, overwrite: true);
            log("Copied " + name);
        }
    }

    private static int RunVisible(string fileName, string arguments, string workingDirectory, TimeSpan timeout, Action<string> log)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                log("Failed to start " + fileName);
                return -1;
            }

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                log("Timed out: " + fileName);
                return -2;
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            log(fileName + ": " + ex.Message);
            return -1;
        }
    }

    private static (int ExitCode, string Output) RunCaptured(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        Action<string>? log)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? "" : workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                log?.Invoke("Timed out: " + fileName + " " + arguments);
                return (-2, "");
            }

            var text = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
            return (process.ExitCode, text);
        }
        catch (Exception ex)
        {
            log?.Invoke(fileName + " " + arguments + ": " + ex.Message);
            return (-1, ex.Message);
        }
    }
}
