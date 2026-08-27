namespace OnePlusWebcam;

internal sealed class FileLogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileLogger(string path)
    {
        _path = path;
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
        lock (_gate)
        {
            File.AppendAllText(_path, line);
        }
    }

    public string Tail(int maxChars = 2000)
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return "";
            }

            var text = File.ReadAllText(_path);
            return text.Length <= maxChars ? text : text[^maxChars..];
        }
    }
}
