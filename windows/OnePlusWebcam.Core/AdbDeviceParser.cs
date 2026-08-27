namespace OnePlusWebcam;

public static class AdbDeviceParser
{
    public static IReadOnlyList<PhoneDevice> Parse(string adbDevicesDashL)
    {
        var devices = new List<PhoneDevice>();
        if (string.IsNullOrWhiteSpace(adbDevicesDashL))
        {
            return devices;
        }

        foreach (var rawLine in adbDevicesDashL.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var serial = parts[0];
            var status = ParseStatus(parts[1]);
            var model = serial;
            foreach (var extra in parts.Skip(2))
            {
                if (extra.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                {
                    model = extra["model:".Length..].Replace('_', ' ');
                    break;
                }
            }

            devices.Add(new PhoneDevice(serial, model, Sdk: 0, status));
        }

        var usbDevices = devices
            .Where(d => !d.Serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (usbDevices.Count > 0)
        {
            return usbDevices;
        }

        return devices;
    }

    public static DeviceAdbStatus ParseStatus(string token)
    {
        return token switch
        {
            "device" => DeviceAdbStatus.Device,
            "unauthorized" => DeviceAdbStatus.Unauthorized,
            "offline" => DeviceAdbStatus.Offline,
            _ => DeviceAdbStatus.Unknown,
        };
    }
}
