using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace WattLite;

/// <summary>
/// 构造子进程第三个参数：Base64Url(MemoryPack(SubProcessArgumentIndex2Model))。
/// 直接使用官方类型与其自身的序列化器，保证字节格式完全一致。
/// </summary>
internal static class SubProcessArgs
{
    public static string Create(string appDataDirectory, string cacheDirectory)
    {
        Directory_Create(appDataDirectory);
        Directory_Create(cacheDirectory);

        var type = FindModelType();
        var model = type != null ? CreateWithOfficialType(type, appDataDirectory, cacheDirectory) : null;
        return model ?? CreateFallback(appDataDirectory, cacheDirectory);
    }

    static void Directory_Create(string dir)
    {
        try { System.IO.Directory.CreateDirectory(dir); } catch { }
    }

    static Type FindModelType()
    {
        foreach (var assemblyName in new[] { "BD.WTTS.Client.IPC", "BD.WTTS.Client" })
        {
            try
            {
                var asm = Assembly.Load(assemblyName);
                var type = asm.GetTypes().FirstOrDefault(t => t.Name == "SubProcessArgumentIndex2Model");
                if (type != null) return type;
            }
            catch (Exception ex)
            {
                Log.Write($"加载 {assemblyName} 失败: {ex.Message}");
            }
        }
        Log.Write("未找到 SubProcessArgumentIndex2Model");
        return null;
    }

    static string CreateWithOfficialType(Type type, string appDataDirectory, string cacheDirectory)
    {
        try
        {
            var model = Activator.CreateInstance(type, nonPublic: true);
            type.GetProperty("AppDataDirectory")?.SetValue(model, appDataDirectory);
            type.GetProperty("CacheDirectory")?.SetValue(model, cacheDirectory);

            var smp2 = typeof(System.Serializable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "SMP2" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (smp2 == null) return null;

            var bytes = (byte[])smp2.MakeGenericMethod(type).Invoke(null, new[] { model });
            Log.Write($"参数序列化（官方类型）{bytes.Length} 字节");
            return Base64UrlEncode(bytes);
        }
        catch (Exception ex)
        {
            Log.Write("官方类型序列化失败: " + ex);
            return null;
        }
    }

    /// <summary>MemoryPack 布局：[成员数=2][字符串1][字符串2]，字符串为 7bit 长度 + UTF8。</summary>
    static string CreateFallback(string appDataDirectory, string cacheDirectory)
    {
        var buffer = new System.Collections.Generic.List<byte> { 0x02 };
        WriteString(buffer, appDataDirectory);
        WriteString(buffer, cacheDirectory);
        Log.Write("参数序列化（回退实现）");
        return Base64UrlEncode(buffer.ToArray());
    }

    static void WriteString(System.Collections.Generic.ICollection<byte> buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");
        var len = (uint)bytes.Length;
        while (len >= 0x80)
        {
            buffer.Add((byte)(len | 0x80));
            len >>= 7;
        }
        buffer.Add((byte)len);
        foreach (var b in bytes) buffer.Add(b);
    }

    static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
