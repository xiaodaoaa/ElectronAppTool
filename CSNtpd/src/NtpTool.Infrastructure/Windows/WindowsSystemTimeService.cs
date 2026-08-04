using System.Runtime.InteropServices;
using System.Security.Principal;
using NtpTool.Core.Services;

namespace NtpTool.Infrastructure.Windows;

/// <summary>
/// Windows 系统时间服务实现。通过 P/Invoke 设置系统时间，并检测管理员权限与 w32time 服务状态。
/// 对应需求文档第 5.4 节。
/// </summary>
public sealed class WindowsSystemTimeService : ISystemTimeService
{
    public DateTime GetLocalNow() => DateTime.Now;

    public DateTime GetUtcNow() => DateTime.UtcNow;

    public bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public void SetLocalTime(DateTime localTime)
    {
        var systemTime = new SystemTime
        {
            Year = (ushort)localTime.Year,
            Month = (ushort)localTime.Month,
            Day = (ushort)localTime.Day,
            Hour = (ushort)localTime.Hour,
            Minute = (ushort)localTime.Minute,
            Second = (ushort)localTime.Second,
            Milliseconds = (ushort)localTime.Millisecond
        };

        bool success = SetLocalTime(ref systemTime);
        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            bool admin = IsAdministrator();
            if (!admin)
            {
                throw new SystemTimeException("设置系统时间需要管理员权限，请以管理员身份运行本程序。");
            }

            throw new SystemTimeException($"设置系统时间失败，Win32 错误码 {error}。");
        }
    }

    public bool IsWindowsTimeServiceRunning()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\W32Time");
            return key != null && key.GetValue("Start") is int start && start == 2;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetLocalTime(ref SystemTime lpSystemTime);
}