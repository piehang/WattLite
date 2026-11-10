using System;
using System.IO;
using System.Text;

namespace WattLite;

internal static class Log
{
    static readonly object Gate = new();
    public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "WattLite.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
