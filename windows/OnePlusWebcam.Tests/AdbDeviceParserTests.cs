using OnePlusWebcam;
using Xunit;

namespace OnePlusWebcam.Tests;

public class AdbDeviceParserTests
{
    [Fact]
    public void Parse_PrefersUsbDevicesOverEmulators()
    {
        const string output = """
            List of devices attached
            emulator-5554          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64
            26c15cd6               device usb:1-3 product:aston model:OnePlus_13 device:OP5D2BL1
            """;

        var devices = AdbDeviceParser.Parse(output);
        Assert.Single(devices);
        Assert.Equal("26c15cd6", devices[0].Serial);
        Assert.Equal("OnePlus 13", devices[0].Model);
        Assert.Equal(DeviceAdbStatus.Device, devices[0].Status);
    }

    [Fact]
    public void Parse_KeepsEmulatorWhenNoUsbDevice()
    {
        const string output = """
            List of devices attached
            emulator-5554          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64
            """;

        var devices = AdbDeviceParser.Parse(output);
        Assert.Single(devices);
        Assert.Equal("emulator-5554", devices[0].Serial);
    }

    [Fact]
    public void Parse_DropsEmulatorWhenUsbUnauthorizedPresent()
    {
        const string output = """
            List of devices attached
            emulator-5554          device
            ABCDEF                 unauthorized usb:1-1
            """;

        var devices = AdbDeviceParser.Parse(output);
        Assert.Single(devices);
        Assert.Equal("ABCDEF", devices[0].Serial);
        Assert.Equal(DeviceAdbStatus.Unauthorized, devices[0].Status);
    }

    [Fact]
    public void Parse_UnauthorizedAndOffline()
    {
        const string output = """
            List of devices attached
            ABCDEF                 unauthorized usb:1-1
            123456                 offline
            """;

        var devices = AdbDeviceParser.Parse(output);
        Assert.Equal(2, devices.Count);
        Assert.Equal(DeviceAdbStatus.Unauthorized, devices[0].Status);
        Assert.Equal(DeviceAdbStatus.Offline, devices[1].Status);
    }
}
