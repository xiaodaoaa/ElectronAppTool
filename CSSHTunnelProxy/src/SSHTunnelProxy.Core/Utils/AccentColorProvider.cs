using Microsoft.Win32;

namespace SSHTunnelProxy.Core.Utils;

/// <summary>
/// 读取 Windows 系统强调色（ARGB）。
/// 优先从注册表 <c>HKCU\Software\Microsoft\Windows\DWM\AccentColor</c> 读取，
/// 读取失败或为默认值时回退到 Win11 默认蓝。
/// </summary>
public sealed class AccentColorProvider
{
    /// <summary>回退的强调色：Win11 默认蓝 #0078D4，ARGB = 0xFF0078D4。</summary>
    public const uint DefaultAccentArgb = 0xFF0078D4;

    /// <summary>注册表路径（相对 HKCU）。</summary>
    public const string RegistryPath = @"Software\Microsoft\Windows\DWM";

    /// <summary>注册表值名。</summary>
    public const string RegistryValueName = "AccentColor";

    private readonly Func<uint?> _readArgb;

    /// <summary>
    /// 用默认的注册表读取器构造；测试可注入自定义读取器。
    /// </summary>
    public AccentColorProvider(Func<uint?>? readArgb = null)
    {
        _readArgb = readArgb ?? ReadFromRegistry;
    }

    /// <summary>当前系统强调色 ARGB；读取失败、为 0xFFFFFFFF（"跟随系统默认"哨兵值）或无效时返回 <see cref="DefaultAccentArgb"/>。</summary>
    public uint GetAccentArgb()
    {
        var argb = _readArgb();
        // 0xFFFFFFFF 表示"无显式强调色"，与 null 一样视为无效。
        return (argb is null || argb == uint.MaxValue) ? DefaultAccentArgb : argb.Value;
    }

    private static uint? ReadFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var raw = key?.GetValue(RegistryValueName);
            // DWM 存的是 int；0xFFFFFFFF（-1）表示"跟随系统默认"，视为无效。
            if (raw is int i && i != -1)
                return unchecked((uint)i);
            return null;
        }
        catch
        {
            // 注册表不可读时不崩溃，回退默认色。
            return null;
        }
    }
}
