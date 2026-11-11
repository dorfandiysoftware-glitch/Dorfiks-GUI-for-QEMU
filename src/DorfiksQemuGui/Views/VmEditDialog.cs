using DorfiksQemuGui.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DorfiksQemuGui.Views;

/// <summary>Create/edit dialog, built in code to keep the project small.</summary>
public static class VmEditDialog
{
    public static async Task<bool> ShowAsync(XamlRoot root, VmConfig vm)
    {
        var name = new TextBox { Header = "Name", Text = vm.Name };
        var preset = new ComboBox { Header = "Preset", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var p in new[] { "Custom", "Windows 11", "Windows 10", "Linux", "Android-x86" }) preset.Items.Add(p);
        preset.SelectedItem = vm.Preset;
        var ram = new NumberBox { Header = "RAM (MB)", Value = vm.RamMb, Minimum = 256, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, SmallChange = 512 };
        var cores = new NumberBox { Header = "CPU cores", Value = vm.Cores, Minimum = 1, Maximum = 64, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var disk = new NumberBox { Header = "Disk (GB)", Value = vm.DiskGb, Minimum = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, SmallChange = 8 };
        var iso = new TextBox { Header = "ISO image", Text = vm.IsoPath, HorizontalAlignment = HorizontalAlignment.Stretch };
        var browse = new Button { Content = "Browse…", VerticalAlignment = VerticalAlignment.Bottom };
        browse.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".iso");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var f = await picker.PickSingleFileAsync();
            if (f != null) iso.Text = f.Path;
        };
        var isoRow = new Grid { ColumnSpacing = 8 };
        isoRow.ColumnDefinitions.Add(new ColumnDefinition());
        isoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        isoRow.Children.Add(iso);
        Grid.SetColumn(browse, 1);
        isoRow.Children.Add(browse);
        var uefi = new ToggleSwitch { Header = "UEFI firmware", IsOn = vm.Uefi };
        var whpx = new ToggleSwitch { Header = "Hardware acceleration (WHPX)", IsOn = vm.UseWhpx };
        var extra = new TextBox { Header = "Extra QEMU arguments", Text = vm.ExtraArgs };

        preset.SelectionChanged += (_, _) =>
        {
            switch (preset.SelectedItem as string)
            {
                case "Windows 11": ram.Value = 4096; cores.Value = 4; disk.Value = 64; uefi.IsOn = true; break;
                case "Windows 10": ram.Value = 4096; cores.Value = 2; disk.Value = 48; uefi.IsOn = false; break;
                case "Linux": ram.Value = 2048; cores.Value = 2; disk.Value = 24; uefi.IsOn = true; break;
                case "Android-x86": ram.Value = 3072; cores.Value = 2; disk.Value = 16; uefi.IsOn = false; break;
            }
        };

        var panel = new StackPanel { Spacing = 12, MinWidth = 440 };
        foreach (var c in new UIElement[] { name, preset, ram, cores, disk, isoRow, uefi, whpx, extra }) panel.Children.Add(c);
        var dlg = new ContentDialog
        {
            Title = "Machine",
            Content = new ScrollViewer { Content = panel, MaxHeight = 560 },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return false;

        vm.Name = string.IsNullOrWhiteSpace(name.Text) ? "Machine" : name.Text.Trim();
        vm.Preset = preset.SelectedItem as string ?? "Custom";
        vm.RamMb = (int)ram.Value;
        vm.Cores = (int)cores.Value;
        vm.DiskGb = (int)disk.Value;
        vm.IsoPath = iso.Text.Trim();
        vm.Uefi = uefi.IsOn;
        vm.UseWhpx = whpx.IsOn;
        vm.ExtraArgs = extra.Text.Trim();
        return true;
    }
}
