namespace OnePlusWebcam;

public enum DeviceAdbStatus
{
    Device,
    Unauthorized,
    Offline,
    Unknown,
}

public sealed record PhoneDevice(
    string Serial,
    string Model,
    int Sdk,
    DeviceAdbStatus Status);

public sealed record CameraInfo(
    string Id,
    string Facing,
    string MaxSize,
    double ZoomMin,
    double ZoomMax);

public sealed record CommandSpec(
    string FileName,
    string Arguments,
    string? WorkingDirectory = null);
