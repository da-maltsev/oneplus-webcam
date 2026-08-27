using OnePlusWebcam;
using Xunit;

namespace OnePlusWebcam.Tests;

public class PipelineCommandsTests
{
    [Fact]
    public void StartServer_IncludesZoomInvariant()
    {
        var cmd = PipelineCommands.AdbStartCameraServer(
            "adb.exe", "SERIAL", "4.1", "0", "1920x1080", 30, 1.5);

        Assert.Equal("adb.exe", cmd.FileName);
        Assert.Contains("camera_zoom=1.5", cmd.Arguments);
        Assert.Contains("tcp:27199", PipelineCommands.AdbForward("adb.exe", "SERIAL").Arguments);
        Assert.Contains("/data/local/tmp/scrcpy-server-oneplus.jar", cmd.Arguments);
        Assert.Contains("video_source=camera", cmd.Arguments);
        Assert.DoesNotContain(',', cmd.Arguments.Split("camera_zoom=")[1]);
    }

    [Fact]
    public void FfmpegAndVcam_MatchSizeAndPort()
    {
        var ffmpeg = PipelineCommands.FfmpegStream("ffmpeg.exe", "1280x720", 30);
        Assert.Contains("tcp://127.0.0.1:27199", ffmpeg.Arguments);
        Assert.Contains("-s 1280x720", ffmpeg.Arguments);

        var vcam = PipelineCommands.AkVCamStream("AkVCamManager.exe", "1280x720", 30);
        Assert.Equal("stream --fps 30 OnePlusWebcam RGB24 1280 720", vcam.Arguments);
    }

    [Fact]
    public void ValidateReadyToStart_Messages()
    {
        Assert.Equal(
            "Plug in a phone with USB debugging enabled.",
            PipelineCommands.ValidateReadyToStart(null));

        var unauthorized = new PhoneDevice("s", "Pixel 8", 34, DeviceAdbStatus.Unauthorized);
        Assert.Equal(
            "Unlock the phone and tap Allow USB debugging (Always allow).",
            PipelineCommands.ValidateReadyToStart(unauthorized));

        var old = new PhoneDevice("s", "Old", 30, DeviceAdbStatus.Device);
        Assert.Contains("Android 12", PipelineCommands.ValidateReadyToStart(old));

        var ok = new PhoneDevice("s", "Pixel 8", 34, DeviceAdbStatus.Device);
        Assert.Null(PipelineCommands.ValidateReadyToStart(ok));
    }

    [Fact]
    public void ConfigStore_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "oneplus-webcam-test-" + Guid.NewGuid(), "config.json");
        try
        {
            var config = new AppConfig
            {
                Serial = "abc",
                CameraId = "2",
                Size = "1280x720",
                Fps = 25,
                Zoom = 2,
                Preview = true,
            };
            ConfigStore.Save(config, path);
            var loaded = ConfigStore.Load(path);
            Assert.Equal("abc", loaded.Serial);
            Assert.Equal("2", loaded.CameraId);
            Assert.Equal("1280x720", loaded.Size);
            Assert.Equal(25, loaded.Fps);
            Assert.Equal(2, loaded.Zoom);
            Assert.True(loaded.Preview);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
