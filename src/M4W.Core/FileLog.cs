using System.Globalization;

namespace M4W.Core;

/// <summary>Tiny rolling file log: when the file passes the cap it is moved to ".1" (replacing the previous one). Messages must never contain clipboard content.</summary>
public sealed class FileLog : ILog
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _gate = new();

    public FileLog(string path, long maxBytes = 256 * 1024)
    {
        _path = path;
        _maxBytes = maxBytes;
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {level} {message}{Environment.NewLine}";
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                if (File.Exists(_path) && new FileInfo(_path).Length > _maxBytes)
                    File.Move(_path, _path + ".1", overwrite: true);
                File.AppendAllText(_path, line);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never take the app down; a locked log file just loses the line.
            }
        }
    }
}
