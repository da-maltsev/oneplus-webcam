namespace OnePlusWebcam;

internal sealed class ToolPaths
{
    public string InstallDir { get; }
    public string Adb { get; }
    public string Scrcpy { get; }
    public string ScrcpyServer { get; }
    public string Ffmpeg { get; }
    public string Ffplay { get; }
    public string AkvcamDir { get; }
    public string? AkVCamManager { get; }
    public string? AkVCamAssistant { get; }
    public string VcamIni { get; }

    public ToolPaths(string? baseDirectory = null)
    {
        InstallDir = baseDirectory ?? AppContext.BaseDirectory;
        Adb = Path.Combine(InstallDir, "tools", "scrcpy", "adb.exe");
        Scrcpy = Path.Combine(InstallDir, "tools", "scrcpy", "scrcpy.exe");
        ScrcpyServer = Path.Combine(InstallDir, "tools", "scrcpy", "scrcpy-server");
        Ffmpeg = Path.Combine(InstallDir, "tools", "ffmpeg", "ffmpeg.exe");
        Ffplay = Path.Combine(InstallDir, "tools", "ffmpeg", "ffplay.exe");
        AkvcamDir = Path.Combine(InstallDir, "tools", "akvcam");
        VcamIni = Path.Combine(InstallDir, "vcam.ini");
        if (!File.Exists(VcamIni))
        {
            var bundledIni = Path.Combine(AkvcamDir, "vcam.ini");
            if (File.Exists(bundledIni))
            {
                VcamIni = bundledIni;
            }
        }

        AkVCamManager = FindAkVCamManager(AkvcamDir);
        var assistant = Path.Combine(AkvcamDir, "AkVCamAssistant.exe");
        AkVCamAssistant = File.Exists(assistant) ? assistant : null;
    }

    public string? MissingCaptureToolsMessage()
    {
        if (!File.Exists(Adb) || !File.Exists(Scrcpy) || !File.Exists(ScrcpyServer) || !File.Exists(Ffmpeg))
        {
            return "Installation is incomplete. Re-run OnePlusWebcam-Setup.exe.";
        }

        return null;
    }

    public string? MissingToolsMessage()
    {
        var capture = MissingCaptureToolsMessage();
        if (capture is not null)
        {
            return capture;
        }

        if (string.IsNullOrEmpty(AkVCamManager) || !File.Exists(AkVCamManager))
        {
            return PipelineCommands.VirtualCameraDriverHelp;
        }

        return null;
    }

    private static string? FindAkVCamManager(string akvcamDir)
    {
        var bundled = Path.Combine(akvcamDir, "AkVCamManager.exe");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] known =
        [
            Path.Combine(programFiles, "akvirtualcamera", "x64", "AkVCamManager.exe"),
            Path.Combine(programFiles, "AkVirtualCamera", "AkVCamManager.exe"),
        ];
        foreach (var path in known)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
