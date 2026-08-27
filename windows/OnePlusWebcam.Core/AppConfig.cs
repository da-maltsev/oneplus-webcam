namespace OnePlusWebcam;

public sealed class AppConfig
{
    public string? Serial { get; set; }
    public string CameraId { get; set; } = "0";
    public string Size { get; set; } = "1920x1080";
    public int Fps { get; set; } = 30;
    public double Zoom { get; set; } = 1.0;
    public bool Preview { get; set; }
    public bool StartWithWindows { get; set; }
}
