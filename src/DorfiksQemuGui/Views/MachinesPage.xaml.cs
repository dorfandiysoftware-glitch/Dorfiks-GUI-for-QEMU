using System.Collections.ObjectModel;
using DorfiksQemuGui.Models;
using DorfiksQemuGui.Services;
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
        vms.CollectionChanged += (_, _) => Refresh();
        Refresh();
    }

    void Refresh() => Empty.Visibility = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    async void New_Click(object sender, RoutedEventArgs e)
    {
        var vm = new VmConfig();
        if (await VmEditDialog.ShowAsync(XamlRoot, vm))
        {
            vms.Add(vm);
            VmStore.Save(vms);
        }
    }

    async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not VmConfig vm) return;
        if (await VmEditDialog.ShowAsync(XamlRoot, vm))
        {
            var i = vms.IndexOf(vm);
            vms[i] = vm; // refresh row
            VmStore.Save(vms);
        }
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not VmConfig vm) return;
        vms.Remove(vm);
        VmStore.Save(vms);
    }

    async void Start_Click(object sender, object e)
    {
        if (List.SelectedItem is not VmConfig vm) return;
        var dir = QemuLocator.Find();
        if (dir == null)
        {
            if (App.MainWindow == null || !await App.MainWindow.EnsureQemuAsync()) return;
            dir = QemuLocator.Find();
            if (dir == null) return;
        }
        try
        {
            await QemuLauncher.StartAsync(vm, dir);
            VmStore.Save(vms);
        }
        catch (Exception ex)
        {
            await new ContentDialog { Title = "Could not start", Content = ex.Message, CloseButtonText = "OK", XamlRoot = XamlRoot }.ShowAsync();
        }
    }
}
