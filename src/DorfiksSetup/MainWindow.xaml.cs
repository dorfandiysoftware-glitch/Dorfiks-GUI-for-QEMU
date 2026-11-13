using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;

namespace DorfiksSetup;

public sealed partial class MainWindow : Window
{
    const string AppName = "Dorfik's GUI for QEMU";
    const string ExeName = "DorfiksQemuGui.exe";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DorfiksQemuGui";
    static readonly string Version = (Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0").Split('+')[0];

    bool installing;
    string installDir = "";

    public MainWindow()
    {
        InitializeComponent();
        Title = AppName + " Setup";
        try { SystemBackdrop = new MicaBackdrop(); } catch { }
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(680, 520));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p) { p.IsResizable = false; p.IsMaximizable = false; }
        PathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Dorfiks GUI for QEMU");
        Heading.Text = $"Install {AppName} {Version}";
        Closed += (_, args) => { if (installing) args.Handled = true; };
    }

    async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var f = await picker.PickSingleFolderAsync();
        if (f != null) PathBox.Text = Path.Combine(f.Path, "Dorfiks GUI for QEMU");
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    void Finish_Click(object sender, RoutedEventArgs e)
    {
        if (LaunchBox.IsChecked == true)
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(installDir, ExeName)) { UseShellExecute = true, WorkingDirectory = installDir }); } catch { }
        }
        Close();
    }

    async void Install_Click(object sender, RoutedEventArgs e)
    {
        installDir = PathBox.Text.Trim();
        var payload = Path.Combine(AppContext.BaseDirectory, "payload");
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(payload))
        {
            Warn.Text = "The installer files are missing. Download the installer again.";
            Warn.Visibility = Visibility.Visible;
            return;
        }
        if (Process.GetProcessesByName("DorfiksQemuGui").Length > 0)
        {
            Warn.Text = "Dorfik's GUI for QEMU is running. Close it and press Install again.";
            Warn.Visibility = Visibility.Visible;
            return;
        }
        PageWelcome.Visibility = Visibility.Collapsed;
        PageProgress.Visibility = Visibility.Visible;
        installing = true;
        var desktop = DesktopBox.IsChecked == true;
        var progress = new Progress<(double Percent, string File)>(p => { Bar.Value = p.Percent; Status.Text = p.File; });
        try
        {
            await Task.Run(() => DoInstall(payload, installDir, desktop, progress));
            DoneSub.Text = "Installed to " + installDir + ". You can remove it from Settings → Apps.";
        }
        catch (Exception ex)
        {
            DoneText.Text = "Installation failed";
            DoneSub.Text = ex.Message;
            LaunchBox.Visibility = Visibility.Collapsed;
        }
        installing = false;
        PageProgress.Visibility = Visibility.Collapsed;
        PageDone.Visibility = Visibility.Visible;
    }

    static void DoInstall(string payload, string dir, bool desktop, IProgress<(double, string)> progress)
    {
        var files = Directory.GetFiles(payload, "*", SearchOption.AllDirectories);
        var total = Math.Max(1, files.Sum(f => new FileInfo(f).Length));
        long done = 0;
        Directory.CreateDirectory(dir);
        foreach (var src in files)
        {
            var rel = Path.GetRelativePath(payload, src);
            var dst = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
            done += new FileInfo(src).Length;
            progress.Report((done * 100.0 / total, rel));
        }
        progress.Report((100, "Creating shortcuts…"));
        var exe = Path.Combine(dir, ExeName);
        var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
        Shortcut(startMenu, exe, dir);
        if (desktop) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"), exe, dir);

        File.WriteAllText(Path.Combine(dir, "uninstall.ps1"), UninstallScript(dir, startMenu));
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", Version);
        key.SetValue("Publisher", "Dorfik");
        key.SetValue("InstallLocation", dir);
        key.SetValue("DisplayIcon", exe);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("UninstallString",
            $"powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{Path.Combine(dir, "uninstall.ps1")}\"");
    }

    static void Shortcut(string path, string target, string workDir)
    {
        var t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) return;
        dynamic sh = Activator.CreateInstance(t)!;
        dynamic lnk = sh.CreateShortcut(path);
        lnk.TargetPath = target;
        lnk.WorkingDirectory = workDir;
        lnk.Save();
    }

    static string UninstallScript(string dir, string startMenuLnk)
    {
        var n = AppName.Replace("'", "''");
        return $@"Add-Type -AssemblyName PresentationFramework
$r = [System.Windows.MessageBox]::Show('Remove {n}? Your virtual machines and disks are kept.', 'Uninstall', 'YesNo', 'Question')
if ($r -ne 'Yes') {{ exit }}
Get-Process DorfiksQemuGui -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -LiteralPath '{startMenuLnk.Replace("'", "''")}' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath ([Environment]::GetFolderPath('DesktopDirectory') + '\{n}.lnk') -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\{UninstallKey}' -ErrorAction SilentlyContinue
Start-Process cmd -ArgumentList '/c timeout /t 2 /nobreak >nul & rmdir /s /q ""{dir}""' -WindowStyle Hidden
[System.Windows.MessageBox]::Show('{n} was removed.', 'Uninstall') | Out-Null
";
    }
}
