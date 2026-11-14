using System.Collections.ObjectModel;
using DorfiksQemuGui.Models;
using DorfiksQemuGui.Services;
using DorfiksQemuGui.Services.Engines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DorfiksQemuGui.Views;

public sealed partial class MachinesPage : Page
{
    readonly ObservableCollection<VmConfig> vms = new(VmStore.Load());

    public MachinesPage()
    {
        InitializeComponent();
        List.ItemsSource = vms;
        if (vms.Count > 0) List.SelectedIndex = 0; else ShowDetails(null);
    }

    void List_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowDetails(List.SelectedItem as VmConfig);

    // ---- right-hand details panel (like VirtualBox's) ----
    void ShowDetails(VmConfig? vm)
    {
        var has = vm != null;
        SettingsBtn.IsEnabled = CloneBtn.IsEnabled = DeleteBtn.IsEnabled = StartBtn.IsEnabled = has;
        Details.Children.Clear();
        if (vm == null)
        {
            Details.Children.Add(new TextBlock { Text = "Welcome to Dorfiks VM GUI", Style = (Style)Application.Current.Resources["TitleTextBlockStyle"], Margin = new Thickness(0, 24, 0, 0) });
            Details.Children.Add(new TextBlock
            {
                Text = "Your virtual machines live in the list on the left. There is none yet, so press “New” to create the first one: " +
                       "choose an operating system and the right settings are filled in for you.",
                TextWrapping = TextWrapping.Wrap, Opacity = 0.8,
            });
            return;
        }
        Details.Children.Add(new TextBlock { Text = vm.Name, Style = (Style)Application.Current.Resources["TitleTextBlockStyle"], Margin = new Thickness(0, 8, 0, 0) });
        var disk = string.IsNullOrEmpty(vm.DiskPath) ? "created on first start" : vm.DiskPath;
        Details.Children.Add(Card("General",
            ("Name", vm.Name), ("Operating system", vm.Preset), ("Engine", Engine.ById(vm.Engine).Name), ("Installer ISO", string.IsNullOrWhiteSpace(vm.IsoPath) ? "— none —" : vm.IsoPath)));
        Details.Children.Add(Card("System",
            ("Memory", $"{vm.RamMb} MB"), ("Processors", vm.Cores.ToString()), ("Chipset", vm.Machine),
            ("Firmware", vm.Uefi ? "UEFI" : "BIOS"),
            ("Acceleration", vm.Engine != "qemu" ? "Provided by " + Engine.ById(vm.Engine).Name : vm.UseWhpx && Whpx.IsAvailable() ? "WHPX (hardware)" : "Software (TCG)")));
        Details.Children.Add(Card("Display", ("Graphics", vm.Video)));
        Details.Children.Add(Card("Storage", ("Disk", $"{vm.DiskGb} GB, {vm.DiskBus}"), ("File", disk)));
        Details.Children.Add(Card("Network", ("Adapter", vm.Net), ("Mode", "NAT")));
        Details.Children.Add(Card("Audio", ("Device", vm.Audio ? vm.Sound : "disabled")));
    }

    static Border Card(string title, params (string Label, string Value)[] rows)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"] });
        foreach (var (label, value) in rows)
        {
            var g = new Grid { ColumnSpacing = 12 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(new TextBlock { Text = label + ":", Opacity = 0.7 });
            var v = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            panel.Children.Add(g);
        }
        return new Border
        {
            Child = panel, Padding = new Thickness(16, 12, 16, 14), CornerRadius = new CornerRadius(8),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        };
    }

    // ---- toolbar actions ----
    async void New_Click(object sender, RoutedEventArgs e)
    {
        var vm = await NewMachineWizard.ShowAsync(XamlRoot);
        if (vm == null) return;
        vms.Add(vm);
        VmStore.Save(vms);
        List.SelectedItem = vm;
    }

    async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not VmConfig vm) return;
        if (await VmEditDialog.ShowAsync(XamlRoot, vm))
        {
            var i = vms.IndexOf(vm);
            vms[i] = vm; // refresh row
            List.SelectedItem = vm;
            VmStore.Save(vms);
            ShowDetails(vm);
        }
    }

    void Clone_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not VmConfig vm) return;
        var json = System.Text.Json.JsonSerializer.Serialize(vm);
        var copy = System.Text.Json.JsonSerializer.Deserialize<VmConfig>(json)!;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = vm.Name + " (copy)";
        copy.DiskPath = ""; // a clone starts with its own empty disk
        vms.Add(copy);
        VmStore.Save(vms);
        List.SelectedItem = copy;
    }

    async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not VmConfig vm) return;
        var dlg = new ContentDialog
        {
            Title = $"Delete “{vm.Name}”?",
            Content = "The machine is removed from the list. Its disk file is kept so nothing is lost by accident.",
            PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        vms.Remove(vm);
        VmStore.Save(vms);
        if (vms.Count > 0) List.SelectedIndex = 0; else ShowDetails(null);
    }

    async void Start_Click(object sender, object e)
    {
        if (List.SelectedItem is not VmConfig vm) return;
        var engine = Engine.ById(vm.Engine);
        if (!engine.Installed && (App.MainWindow == null || !await App.MainWindow.EnsureEngineAsync(engine))) return;
        try
        {
            await engine.StartAsync(vm);
            VmStore.Save(vms);
            ShowDetails(vm);
        }
        catch (Exception ex)
        {
            await new ContentDialog { Title = "Could not start", Content = ex.Message, CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();
        }
    }
}
