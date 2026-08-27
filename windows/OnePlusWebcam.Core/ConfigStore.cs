using System.Text.Json;

namespace OnePlusWebcam;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnePlusWebcam");

    public static string DefaultConfigPath => Path.Combine(DefaultDirectory, "config.json");

    public static string DefaultLogPath => Path.Combine(DefaultDirectory, "oneplus-webcam.log");

    public static AppConfig Load(string? path = null)
    {
        var file = path ?? DefaultConfigPath;
        if (!File.Exists(file))
        {
            return new AppConfig();
        }

        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
    }

    public static void Save(AppConfig config, string? path = null)
    {
        var file = path ?? DefaultConfigPath;
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(file, JsonSerializer.Serialize(config, JsonOptions));
    }
}
