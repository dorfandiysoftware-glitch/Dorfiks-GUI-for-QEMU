using DorfiksQemuGui.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DorfiksQemuGui.Views;

/// <summary>VirtualBox-style settings dialog with a tab per area.</summary>
public static class VmEditDialog
{
    public static async Task<bool> ShowAsync(XamlRoot root, VmConfig vm)
    {
        var f = new VmForm(vm);
        var diskExists = !string.IsNullOrEmpty(vm.DiskPath) && File.Exists(vm.DiskPath);
        f.Disk.IsEnabled = !diskExists;

        var pivot = new Pivot { MinWidth = 520, Height = 460 };
        void Tab(string title, params UIElement[] items) =>
            pivot.Items.Add(new PivotItem { Header = title, Content = new ScrollViewer { Content = VmForm.Stack(items), Padding = new Thickness(0, 8, 12, 8) } });

        Tab("General", f.Name, f.IsoRow(), f.Note);
        Tab("System", f.RamLabel, f.Ram, f.CoresLabel, f.Cores, f.Machine, f.Cpu, f.Uefi, f.Whpx, f.Clock);
        Tab("Display", f.Video);
        Tab("Storage", f.DiskLabel, f.Disk, new TextBlock { Text = diskExists ? "The disk already exists, so its size cannot be changed here." : "The disk is created the first time you start the machine.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap }, f.Bus);
        Tab("Network", f.Net, new TextBlock { Text = "NAT: the machine reaches the internet through this PC.", Opacity = 0.7 });
        Tab("Audio & input", f.Sound, f.Tablet);
        Tab("Advanced", f.Extra);

        var dlg = new ContentDialog
        {
            Title = $"Settings — {vm.Name}",
            Content = pivot,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return false;
        f.Apply();
        return true;
    }
}
