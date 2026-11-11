using DorfiksQemuGui.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DorfiksQemuGui.Views;

public sealed partial class SettingsPage : Page
{
    bool loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        AutoInstall.IsOn = AppSettings.Current.AutoInstallQemu;
        QemuDir.Text = QemuLocator.Find() ?? AppSettings.Current.QemuDir;
        UpdateStatus();
        loading = false;
    }

    void UpdateStatus() =>
        Status.Text = QemuLocator.Find() is { } d ? "QEMU found: " + d : "QEMU not found.";

    void AutoInstall_Toggled(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        AppSettings.Current.AutoInstallQemu = AutoInstall.IsOn;
        AppSettings.Current.Save();
    }

    void QemuDir_LostFocus(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.QemuDir = QemuDir.Text.Trim();
        AppSettings.Current.Save();
        UpdateStatus();
    }

    async void Reinstall_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow != null) await App.MainWindow.EnsureQemuAsync(force: true);
        QemuDir.Text = QemuLocator.Find() ?? "";
        UpdateStatus();
    }
}
