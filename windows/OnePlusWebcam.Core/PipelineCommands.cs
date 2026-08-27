using System.Globalization;

namespace OnePlusWebcam;

public static class PipelineCommands
{
    public const int ForwardPort = 27199;
    public const string RemoteJar = "/data/local/tmp/scrcpy-server-oneplus.jar";
    public const string VcamDeviceId = "OnePlusWebcam";
    public const string VcamDescription = "OnePlus Webcam";
    public const int MinAndroidSdk = 31;
    public const string VirtualCameraDriverHelp =
        "The virtual webcam driver is not running. Click \"Install webcam driver\" and accept the administrator prompt, then Refresh.";

    public static readonly string[] SizeChoices =
    [
        "3840x2160",
        "1920x1080",
        "1280x720",
        "640x480",
    ];

    public static CommandSpec AdbDevices(string adb) =>
        new(adb, "devices -l");

    public static CommandSpec AdbGetprop(string adb, string serial, string property) =>
        new(adb, $"-s {serial} shell getprop {property}");

    public static CommandSpec AdbPushServer(string adb, string serial, string localServerPath) =>
        new(adb, $"-s {serial} push \"{localServerPath}\" {RemoteJar}");

    public static CommandSpec AdbForwardRemove(string adb, string serial) =>
        new(adb, $"-s {serial} forward --remove tcp:{ForwardPort}");

    public static CommandSpec AdbForward(string adb, string serial) =>
        new(adb, $"-s {serial} forward tcp:{ForwardPort} localabstract:scrcpy");

    public static CommandSpec AdbPkillServer(string adb, string serial) =>
        new(adb, $"-s {serial} shell pkill -f scrcpy-server-oneplus");

    public static CommandSpec ScrcpyListCameras(string scrcpy, string serial) =>
        new(scrcpy, $"-s {serial} --list-cameras");

    public static CommandSpec ScrcpyVersion(string scrcpy) =>
        new(scrcpy, "--version");

    public static CommandSpec AdbStartCameraServer(
        string adb,
        string serial,
        string serverVersion,
        string cameraId,
        string size,
        int fps,
        double zoom)
    {
        var zoomArg = string.Create(CultureInfo.InvariantCulture, $" camera_zoom={zoom}");
        var args =
            $"-s {serial} shell CLASSPATH={RemoteJar} app_process / com.genymobile.scrcpy.Server {serverVersion} " +
            "tunnel_forward=true audio=false control=false cleanup=true raw_stream=true video_source=camera " +
            $"camera_id={cameraId} camera_size={size} camera_fps={fps} stay_awake=true{zoomArg}";
        return new CommandSpec(adb, args);
    }

    public static CommandSpec FfmpegStream(string ffmpeg, string size, int fps)
    {
        var args =
            "-hide_banner -loglevel warning -fflags nobuffer -flags low_delay -probesize 32 -analyzeduration 0 " +
            $"-f h264 -i tcp://127.0.0.1:{ForwardPort} -an -pix_fmt rgb24 -s {size} -r {fps} -f rawvideo pipe:1";
        return new CommandSpec(ffmpeg, args);
    }

    public static CommandSpec AkVCamStream(string akVCamManager, string size, int fps)
    {
        ParseSize(size, out var width, out var height);
        return new CommandSpec(akVCamManager, $"stream --fps {fps} {VcamDeviceId} RGB24 {width} {height}");
    }

    public static CommandSpec FfplayPreview(string ffplay, string size, int fps)
    {
        ParseSize(size, out var width, out var height);
        var args =
            $"-loglevel error -noborder -alwaysontop -window_title \"OnePlus Webcam preview\" " +
            $"-fflags nobuffer -flags low_delay -f dshow -video_size {width}x{height} -framerate {fps} " +
            "-i \"video=OnePlus Webcam\"";
        return new CommandSpec(ffplay, args);
    }

    public static CommandSpec AkVCamDevices(string akVCamManager) =>
        new(akVCamManager, "devices");

    public static CommandSpec AkVCamAssistantInstall(string assistant) =>
        new(assistant, "--install");

    public static CommandSpec AkVCamLoad(string akVCamManager, string iniPath) =>
        new(akVCamManager, $"load \"{iniPath}\"");

    public static CommandSpec AkVCamSetPageSize(string akVCamManager) =>
        new(akVCamManager, "set-page-size 128000000");

    public static CommandSpec AkVCamRemoveDevice(string akVCamManager) =>
        new(akVCamManager, $"remove-device {VcamDeviceId}");

    public static CommandSpec AkVCamUpdate(string akVCamManager) =>
        new(akVCamManager, "update");

    public static void ParseSize(string size, out int width, out int height)
    {
        var parts = size.Split('x', 'X');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out width)
            || !int.TryParse(parts[1], out height))
        {
            throw new ArgumentException($"Invalid size '{size}'. Expected WxH.", nameof(size));
        }
    }

    public static string StatusLabel(PhoneDevice? phone, bool streaming, CameraInfo? camera, string size, int fps)
    {
        if (phone is null)
        {
            return "No phone";
        }

        var connected = phone.Status switch
        {
            DeviceAdbStatus.Device => streaming
                ? $"Streaming · Camera {camera?.Id ?? "?"} ({camera?.Facing ?? "unknown"}) · {size}@{fps}"
                : $"{phone.Model} · connected",
            DeviceAdbStatus.Unauthorized => "Unauthorized",
            DeviceAdbStatus.Offline => $"{phone.Model} · offline",
            DeviceAdbStatus.Unknown => $"{phone.Model} · unknown",
            _ => throw new InvalidOperationException($"Unhandled device status: {phone.Status}"),
        };
        return connected;
    }

    public static string? ValidateReadyToStart(PhoneDevice? phone)
    {
        if (phone is null)
        {
            return "Plug in a phone with USB debugging enabled.";
        }

        return phone.Status switch
        {
            DeviceAdbStatus.Unauthorized =>
                "Unlock the phone and tap Allow USB debugging (Always allow).",
            DeviceAdbStatus.Offline => "Plug in a phone with USB debugging enabled.",
            DeviceAdbStatus.Unknown => "Plug in a phone with USB debugging enabled.",
            DeviceAdbStatus.Device when phone.Sdk > 0 && phone.Sdk < MinAndroidSdk =>
                $"Camera webcam needs Android 12 or newer (this phone reports Android {SdkToAndroidVersion(phone.Sdk)}).",
            DeviceAdbStatus.Device => null,
            _ => throw new InvalidOperationException($"Unhandled device status: {phone.Status}"),
        };
    }

    public static int SdkToAndroidVersion(int sdk) =>
        sdk switch
        {
            31 => 12,
            32 => 12,
            33 => 13,
            34 => 14,
            35 => 15,
            36 => 16,
            _ => sdk >= 31 ? sdk - 19 : sdk,
        };
}
