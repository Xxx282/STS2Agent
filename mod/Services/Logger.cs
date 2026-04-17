using System;
using System.IO;
using System.Text;

namespace STS2Agent.Services;

public static class Logger
{
    private static readonly string LogFilePath;
    private static readonly object Lock = new();
    private static readonly StringBuilder Buffer = new();
    private const int FlushThreshold = 10;

    static Logger()
    {
        // 固定日志路径: D:\steam\steamapps\common\Slay the Spire 2\mods\STS2Agent\logs\debug.log
        string logDir = @"D:\steam\steamapps\common\Slay the Spire 2\mods\STS2Agent\logs";
        try
        {
            Directory.CreateDirectory(logDir);
        }
        catch
        {
            // 降级到游戏目录下的 mods 路径
            logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods", "STS2Agent", "logs");
            try
            {
                Directory.CreateDirectory(logDir);
            }
            catch
            {
                // 最后降级到临时目录
                logDir = Path.Combine(Path.GetTempPath(), "STS2Agent", "logs");
                Directory.CreateDirectory(logDir);
            }
        }
        LogFilePath = Path.Combine(logDir, "debug.log");
    }

    public static string GetLogFilePath() => LogFilePath;

    private static void EnsureLogFile()
    {
        if (!File.Exists(LogFilePath))
        {
            try
            {
                using (File.Create(LogFilePath)) { }
            }
            catch { }
        }
    }

    private static void Write(string message)
    {
        lock (Lock)
        {
            try
            {
                EnsureLogFile();
                var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch { }
        }
    }

    public static void Info(string message)
    {
        Write($"[INFO] {message}");
    }

    public static void Warn(string message)
    {
        Write($"[WARN] {message}");
    }

    public static void Error(string message, Exception? ex = null)
    {
        var fullMsg = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
        Write($"[ERROR] {fullMsg}");
    }

    public static void Debug(string message)
    {
        Write($"[DEBUG] {message}");
    }

    // 兼容性别名
    public static void Log(string message) => Info(message);
}
