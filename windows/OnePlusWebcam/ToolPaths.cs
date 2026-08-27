namespace OnePlusWebcam;

internal sealed class ToolPaths
{
    public string InstallDir { get; }
    public string Adb { get; }
    public string Scrcpy { get; }
    public string ScrcpyServer { get; }
    public string Ffmpeg { get; }
    public string Ffplay { get; }
    public string? AkVCamManager { get; }
    public string VcamIni { get; }

    public ToolPaths(string? baseDirectory = null)
    {
        InstallDir = baseDirectory ?? AppContext.BaseDirectory;
        Adb = Path.Combine(InstallDir, "tools", "scrcpy", "adb.exe");
        Scrcpy = Path.Combine(InstallDir, "tools", "scrcpy", "scrcpy.exe");
        ScrcpyServer = Path.Combine(InstallDir, "tools", "scrcpy", "scrcpy-server");
        Ffmpeg = Path.Combine(InstallDir, "tools", "ffmpeg", "ffmpeg.exe");
        Ffplay = Path.Combine(InstallDir, "tools", "ffmpeg", "ffplay.exe");
        VcamIni = Path.Combine(InstallDir, "vcam.ini");
        AkVCamManager = FindAkVCamManager(InstallDir);
    }

    public string? MissingToolsMessage()
    {
        if (!File.Exists(Adb) || !File.Exists(Scrcpy) || !File.Exists(ScrcpyServer)
            || !File.Exists(Ffmpeg) || string.IsNullOrEmpty(AkVCamManager) || !File.Exists(AkVCamManager))
        {
            return "Installation is incomplete. Re-run OnePlusWebcam-Setup.exe.";
        }

        return null;
    }

    private static string? FindAkVCamManager(string installDir)
    {
        var bundled = Path.Combine(installDir, "tools", "akvcam", "AkVCamManager.exe");
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

        if (Directory.Exists(programFiles))
        {
            try
            {
                return Directory
                    .EnumerateFiles(programFiles, "AkVCamManager.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }
}
