using DorfiksQemuGui.Services;
using DorfiksQemuGui.Services.Engines;
using DorfiksQemuGui.Views;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DorfiksQemuGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Dorfiks VM GUI";
        try { SystemBackdrop = new MicaBackdrop(); } catch { }
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));
        Nav.SelectedItem = Nav.MenuItems[0];
        WhpxBar.IsOpen = !Whpx.IsAvailable();
        if (WhpxBar.IsOpen && Whpx.FeatureInstalled)
        {
            // The feature is on but Windows reports no usable hypervisor platform: nothing to enable.
            WhpxBar.Severity = InfoBarSeverity.Informational;
            WhpxBar.Title = "Hardware acceleration is not available";
            WhpxBar.Message = "Windows Hypervisor Platform reports no usable hypervisor on this PC (" + Whpx.Diagnostic +
                "). Virtual machines will use slower software emulation. If you just changed Windows features, restart; " +
                "otherwise the CPU or Windows build may not support it.";
            WhpxButton.Visibility = Visibility.Collapsed;
        }
        _ = EnsureQemuAsync();
    }

    async void WhpxButton_Click(object sender, RoutedEventArgs e)
    {
        WhpxButton.IsEnabled = false;
        var ok = await Whpx.EnableAsync();
        WhpxBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        WhpxBar.Title = ok ? "Almost done" : "Could not enable";
        WhpxBar.Message = ok
            ? "Restart Windows to finish. If acceleration is still off afterwards, enable virtualization (Intel VT-x / AMD-V) in BIOS."
            : "Administrator approval was declined or the feature is unavailable on this Windows edition.";
        WhpxButton.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        WhpxButton.IsEnabled = true;
    }

    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        ContentFrame.Navigate(tag == "settings" ? typeof(SettingsPage) : typeof(MachinesPage));
    }

    /// <summary>Finds QEMU, or downloads and installs it automatically.</summary>
    public async Task<bool> EnsureQemuAsync(bool force = false)
    {
        QemuBar.Title = "QEMU";
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

    /// <summary>Installs VirtualBox / VMware (or QEMU) if it is missing, showing progress in the top bar.</summary>
    public async Task<bool> EnsureEngineAsync(Engine e)
    {
        if (e.Installed) return true;
        if (e.Id == "qemu") return await EnsureQemuAsync();
        QemuBar.Title = e.Name;
        QemuBar.Severity = InfoBarSeverity.Informational;
        QemuBar.Message = "Preparing…";
        QemuProgress.Value = 0;
        QemuProgress.Visibility = Visibility.Visible;
        QemuBar.IsOpen = true;
        var progress = new Progress<(string Status, double Percent)>(p => { QemuBar.Message = p.Status; QemuProgress.Value = p.Percent; });
        try
        {
            await e.InstallAsync(progress);
            QemuBar.Severity = InfoBarSeverity.Success;
            QemuBar.Message = e.Name + " is installed and ready.";
            QemuProgress.Visibility = Visibility.Collapsed;
            return e.Installed;
        }
        catch (Exception ex)
        {
            QemuBar.Severity = InfoBarSeverity.Error;
            QemuBar.Message = e.Name + " install failed: " + ex.Message;
            QemuProgress.Visibility = Visibility.Collapsed;
            return false;
        }
    }
}
