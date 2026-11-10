using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WattLite;

internal static class HostsFile
{
    const string BeginMark = "# >>> WattLite 加速开始 >>>";
    const string EndMark = "# <<< WattLite 加速结束 <<<";

    static string PathHosts => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32", "drivers", "etc", "hosts");

    public static void Apply(IEnumerable<string> domains)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine(BeginMark);
        foreach (var domain in domains)
        {
            sb.Append("127.0.0.1 ").Append(domain).AppendLine();
            sb.Append("::1 ").Append(domain).AppendLine();
        }
        sb.AppendLine(EndMark);

        var original = Strip(File.ReadAllText(PathHosts));
        File.WriteAllText(PathHosts, original.TrimEnd() + Environment.NewLine + sb, new UTF8Encoding(false));
        FlushDns();
    }

    public static void Remove()
    {
        var original = Strip(File.ReadAllText(PathHosts));
        File.WriteAllText(PathHosts, original, new UTF8Encoding(false));
        FlushDns();
    }

    static string Strip(string text)
    {
        var start = text.IndexOf(BeginMark, StringComparison.Ordinal);
        if (start < 0) return text;
        var end = text.IndexOf(EndMark, start, StringComparison.Ordinal);
        if (end < 0) return text;
        end += EndMark.Length;
        while (end < text.Length && (text[end] == '\r' || text[end] == '\n')) end++;
        return text.Remove(start, end - start);
    }

    static void FlushDns()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Log.Write("flushdns 失败: " + ex.Message);
        }
    }
}
