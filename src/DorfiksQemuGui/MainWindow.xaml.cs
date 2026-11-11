using DorfiksQemuGui.Services;
using DorfiksQemuGui.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DorfiksQemuGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Dorfik's GUI for QEMU";
        try { SystemBackdrop = new MicaBackdrop(); } catch { }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));
        Nav.SelectedItem = Nav.MenuItems[0];
        _ = EnsureQemuAsync();
    }

    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        ContentFrame.Navigate(tag == "settings" ? typeof(SettingsPage) : typeof(MachinesPage));
    }

    /// <summary>Finds QEMU, or downloads and installs it automatically.</summary>
    public async Task<bool> EnsureQemuAsync(bool force = false)
    {
        if (!force && QemuLocator.Find() != null) { QemuBar.IsOpen = false; return true; }
        if (!force && !AppSettings.Current.AutoInstallQemu)
        {
            QemuBar.Severity = InfoBarSeverity.Warning;
            QemuBar.Message = "QEMU was not found. Set its folder or enable auto-install in Settings.";
            QemuProgress.Visibility = Visibility.Collapsed;
            QemuBar.IsOpen = true;
            return false;
        }
        QemuBar.Severity = InfoBarSeverity.Informational;
        QemuProgress.Visibility = Visibility.Visible;
        QemuBar.IsOpen = true;
        var progress = new Progress<(string Status, double Percent)>(p =>
        {
            QemuBar.Message = p.Status;
            QemuProgress.Value = p.Percent;
        });
        try
        {
            await QemuInstaller.InstallAsync(progress);
            QemuBar.Severity = InfoBarSeverity.Success;
            QemuBar.Message = "QEMU is installed and ready.";
            QemuProgress.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception ex)
        {
            QemuBar.Severity = InfoBarSeverity.Error;
            QemuBar.Message = "QEMU install failed: " + ex.Message;
            QemuProgress.Visibility = Visibility.Collapsed;
            return false;
        }
    }
}
