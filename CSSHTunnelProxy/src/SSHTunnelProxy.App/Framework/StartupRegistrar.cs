using Microsoft.Win32;

namespace SSHTunnelProxy.App.Framework;

/// <summary>
/// 管理"开机自启"注册表项：HKCU\Software\Microsoft\Windows\CurrentVersion\Run。
/// 勾选时写入当前可执行文件路径，取消时删除。
/// 使用 HKCU（当前用户）作用域，无需管理员权限。
/// </summary>
public static class StartupRegistrar
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SSHTunnelProxy";

    /// <summary>当前是否已注册开机自启。</summary>
    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is not null;
    }

    /// <summary>写入或更新开机自启项，指向当前可执行文件。</summary>
    public static void Register()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, GetExecutableCommand());
    }

    /// <summary>移除开机自启项（不存在则无操作）。</summary>
    public static void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// 构造启动命令行：用引号包裹可执行文件路径。
    /// Environment.ProcessPath 在普通与便携发布下均指向实际 exe。
    /// </summary>
    private static string GetExecutableCommand()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            throw new InvalidOperationException("无法确定当前可执行文件路径。");
        return $"\"{exePath}\"";
    }
}
