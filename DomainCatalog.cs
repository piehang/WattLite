using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using BD.WTTS.Enums;
using BD.WTTS.Models;

namespace WattLite;

/// <summary>
/// 从内置 domains.json 生成加速项目（仅保留游戏相关分组）。
/// 数据来源：官方 LOCAL_ACCELERATE.json（MessagePack + LZ4）解码后的精简结果。
/// </summary>
internal static class DomainCatalog
{
    static readonly HashSet<string> GameGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "Steam 服务",
        "Twitch 直播",
        "Origin",
        "Uplay",
        "Roblox",
        "Nexus Mods",
        "Mod.io",
        "Github",
    };

    public static IReadOnlyList<AccelerateProjectDTO> Load()
    {
        using var stream = typeof(DomainCatalog).Assembly.GetManifestResourceStream("WattLite.domains.json")
            ?? throw new InvalidOperationException("缺少内置域名数据 domains.json");
        using var doc = JsonDocument.Parse(stream);

        var result = new List<AccelerateProjectDTO>();
        foreach (var group in doc.RootElement.EnumerateArray())
        {
            var groupName = group.GetProperty("name").GetString() ?? "";
            if (!GameGroups.Contains(groupName)) continue;

            foreach (var item in group.GetProperty("items").EnumerateArray())
            {
                var dto = new AccelerateProjectDTO
                {
                    Name = item.GetProperty("name").GetString() ?? "",
                    Port = (ushort)item.GetProperty("port").GetInt32(),
                    MatchDomainNames = item.GetProperty("match").GetString() ?? "",
                    ForwardDomainNames = item.GetProperty("forward").GetString() ?? "",
                    IgnoreSSLCertVerification = item.GetProperty("ignoreSsl").GetBoolean(),
                    FakeServerName = item.GetProperty("fakeServerName").GetString(),
                    ProxyType = (ProxyType)item.GetProperty("proxyType").GetInt32(),
                    ListenDomainNames = item.GetProperty("listen").GetString() ?? "",
                    Checked = true,
                    Id = Guid.NewGuid(),
                    Order = result.Count,
                    Version = "1.0.0",
                };
                result.Add(dto);
            }
        }

        Log.Write($"加载加速项目 {result.Count} 个");
        return result;
    }

    /// <summary>收集用于写入 hosts 的域名（去重、去空）。</summary>
    public static List<string> CollectHostsDomains(IReadOnlyList<AccelerateProjectDTO> projects)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in projects)
        {
            if (!p.Checked) continue;
            foreach (var d in p.DomainNamesArray)
            {
                if (string.IsNullOrWhiteSpace(d)) continue;
                var domain = d.Trim();
                // hosts 不支持通配符（如 *.twitch.tv），跳过
                if (domain.Contains('*')) continue;
                set.Add(domain);
            }
        }
        return new List<string>(set);
    }

    public static string Describe(IReadOnlyList<AccelerateProjectDTO> projects)
    {
        var sb = new StringBuilder();
        foreach (var p in projects)
            sb.AppendLine($"  {p.Name} :{p.Port} [{p.ProxyType}] {p.MatchDomainNames}");
        return sb.ToString();
    }
}
