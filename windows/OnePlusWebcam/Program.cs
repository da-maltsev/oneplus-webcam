using System.Diagnostics;

namespace OnePlusWebcam;

internal static class Program
{
    private const string MutexName = @"Local\OnePlusWebcam";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            RestoreExistingWindow();
            return;
        }

        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory(ConfigStore.DefaultDirectory);
        var config = ConfigStore.Load();
        var log = new FileLogger(ConfigStore.DefaultLogPath);
        var tools = new ToolPaths();
        var pipeline = new WebcamPipeline(tools, log);
        var form = new MainForm(pipeline, log, config);
        Application.Run(new TrayApplicationContext(form, pipeline));
    }

    private static void RestoreExistingWindow()
    {
        var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName("OnePlusWebcam"))
        {
            if (process.Id == current.Id)
            {
                continue;
            }

            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                continue;
            }

            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            NativeMethods.SetForegroundWindow(handle);
            return;
        }
    }
}
