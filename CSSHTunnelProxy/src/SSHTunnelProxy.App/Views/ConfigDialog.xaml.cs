using Microsoft.Extensions.DependencyInjection;
using SSHTunnelProxy.App.ViewModels;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SSHTunnelProxy.App.Views;

/// <summary>
/// 配置编辑对话框代码隐藏：负责密码框同步与结果组装。
/// </summary>
public partial class ConfigDialog : Window
{
    private readonly ConfigViewModel _viewModel;

    /// <summary>对话框确定后生成的配置对象（取消则为 null）。</summary>
    public SshServerProfile? Result { get; private set; }

    public ConfigDialog(IServiceProvider services, SshServerProfile? profile = null)
    {
        InitializeComponent();

        _viewModel = new ConfigViewModel(
            services.GetRequiredService<IDpapiProtector>(),
            profile);
        DataContext = _viewModel;

        PasswordBox.Password = _viewModel.Password;
        PassphraseBox.Password = _viewModel.Passphrase;

        _viewModel.PropertyChanged += (_, e) =>
        {
            // 认证方式切换时，将对应密码框回填到相应字段。
            if (e.PropertyName == nameof(ConfigViewModel.Password) &&
                !PasswordBox.IsFocused)
                PasswordBox.Password = _viewModel.Password;
            if (e.PropertyName == nameof(ConfigViewModel.Passphrase) &&
                !PassphraseBox.IsFocused)
                PassphraseBox.Password = _viewModel.Passphrase;

            // 保存确认：同步密码框并组装结果。
            if (e.PropertyName == nameof(ConfigViewModel.Result) && _viewModel.Result)
            {
                _viewModel.Password = PasswordBox.Password;
                _viewModel.Passphrase = PassphraseBox.Password;
                Result = _viewModel.BuildProfile();
                DialogResult = true;
            }
        };

        PasswordBox.PasswordChanged += (_, _) =>
        {
            if (_viewModel.AuthMethod is Core.Models.AuthMethod.Password or
                Core.Models.AuthMethod.KeyboardInteractive)
                _viewModel.Password = PasswordBox.Password;
        };

        PassphraseBox.PasswordChanged += (_, _) =>
            _viewModel.Passphrase = PassphraseBox.Password;
    }

    /// <summary>标题栏拖拽移动窗口（WindowStyle=None 时需手动实现）。</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
