using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NtpTool.App;

/// <summary>
/// 设置窗口。保存成功后关闭，并标记 <see cref="Saved"/> 供调用方刷新。
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>是否通过"保存"成功关闭。</summary>
    public bool Saved { get; private set; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SettingsSaved += OnSettingsSaved;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // 无边框 + WindowChrome 下，最大化时窗口会覆盖任务栏，需用 WM_GETMINMAXINFO 限制到工作区。
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var rect = SystemParameters.WorkArea;
            var left = (int)rect.Left;
            var top = (int)rect.Top;
            var width = (int)rect.Width;
            var height = (int)rect.Height;
            mmi.ptMaxPosition = new POINT(left, top);
            mmi.ptMaxSize = new POINT(width, height);
            mmi.ptMaxTrackSize = new POINT(width, height);
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y) { X = x; Y = y; }
    }

    private void OnSettingsSaved(object? sender, System.EventArgs e)
    {
        Saved = true;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnNavSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ViewClient is null || ViewServer is null || ViewLog is null)
            return;
        switch (NavList.SelectedIndex)
        {
            case 0:
                ViewClient.Visibility = Visibility.Visible;
                ViewServer.Visibility = Visibility.Collapsed;
                ViewLog.Visibility = Visibility.Collapsed;
                break;
            case 1:
                ViewClient.Visibility = Visibility.Collapsed;
                ViewServer.Visibility = Visibility.Visible;
                ViewLog.Visibility = Visibility.Collapsed;
                break;
            case 2:
                ViewClient.Visibility = Visibility.Collapsed;
                ViewServer.Visibility = Visibility.Collapsed;
                ViewLog.Visibility = Visibility.Visible;
                break;
        }
    }
}