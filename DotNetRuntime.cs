using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WattLite;

/// <summary>
/// 加速器子进程是框架依赖应用，需要 .NET 10 运行时。
/// 优先级：程序目录\dotnet → 已安装的 Watt Toolkit 包内 dotnet → 系统 dotnet。
/// </summary>
internal static class DotNetRuntime
{
    const string PackagePrefix = "4651ED44255E.47979655102CE_";

    public static string Locate()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "dotnet");
        if (IsValid(local)) return local;

        foreach (var root in EnumeratePackageRoots())
        {
            var candidate = Path.Combine(root, "dotnet");
            if (IsValid(candidate)) return candidate;
        }

        var system = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        if (IsValid(system)) return system;

        return null;
    }

    static bool IsValid(string dir)
    {
        if (!Directory.Exists(Path.Combine(dir, "host", "fxr"))) return false;
        var core = Path.Combine(dir, "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(core)) return false;
        return Directory.EnumerateDirectories(core).Any(d =>
            Path.GetFileName(d).StartsWith("10.", StringComparison.Ordinal));
    }

    static System.Collections.Generic.IEnumerable<string> EnumeratePackageRoots()
    {
        RegistryKey packages = null;
        try
        {
            packages = Registry.ClassesRoot.OpenSubKey(
                @"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
            if (packages == null) yield break;
            foreach (var name in packages.GetSubKeyNames().Where(n =>
                         n.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase)))
            {
                string root = null;
                try { root = packages.OpenSubKey(name)?.GetValue("PackageRootFolder") as string; }
                catch { }
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) yield return root;
            }
        }
        finally
        {
            packages?.Dispose();
        }
    }
}
