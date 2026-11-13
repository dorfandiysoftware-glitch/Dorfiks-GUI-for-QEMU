using DorfiksQemuGui.Models;
using DorfiksQemuGui.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DorfiksQemuGui.Views;

/// <summary>Three steps: pick the system, tune the hardware, review. Presets fill in everything.</summary>
public static class NewMachineWizard
{
    public static async Task<VmConfig?> ShowAsync(XamlRoot root)
    {
        var vm = new VmConfig();
        var f = new VmForm(vm);
        f.Disk.Maximum = 512;

        // Step 1: system
        var group = new ComboBox { Header = "Type", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var g in Presets.Groups) group.Items.Add(g);
        var version = new ComboBox { Header = "Version", HorizontalAlignment = HorizontalAlignment.Stretch };
        void FillVersions()
        {
            version.Items.Clear();
            foreach (var p in Presets.InGroup((string)group.SelectedItem))
                version.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p });
            version.SelectedIndex = 0;
        }
        group.SelectionChanged += (_, _) => FillVersions();
        version.SelectionChanged += (_, _) =>
        {
            if (version.SelectedItem is not ComboBoxItem { Tag: OsPreset p }) return;
            f.ApplyPreset(p);
            if (string.IsNullOrWhiteSpace(f.Name.Text) || Presets.All.Any(x => x.Name == f.Name.Text)) f.Name.Text = p.Name;
        };
        group.SelectedIndex = 0;
        var step1 = VmForm.Stack(
            new TextBlock { Text = "Pick the operating system you want to install. Dorfik's GUI fills in the right virtual hardware for it.", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 },
            f.Name, group, version, f.Note, f.IsoRow());

        // Step 2: hardware
        var step2 = VmForm.Stack(
            new TextBlock { Text = "Hardware. The defaults come from the preset, change them only if you need to.", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 },
            f.RamLabel, f.Ram, f.CoresLabel, f.Cores, f.DiskLabel, f.Disk);

        // Step 3: review
        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var step3 = VmForm.Stack(
            new TextBlock { Text = "Ready to create", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] },
            summary,
            new TextBlock { Text = "You can change anything later with the Settings button.", Opacity = 0.7 });

        var pages = new FrameworkElement[] { step1, step2, step3 };
        var step = 0;
        var host = new ScrollViewer { MinWidth = 480, MaxHeight = 520, Padding = new Thickness(0, 0, 12, 0) };
        var dlg = new ContentDialog
        {
            Title = "New machine",
            Content = host,
            PrimaryButtonText = "Next",
            SecondaryButtonText = "Back",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        void Render()
        {
            host.Content = pages[step];
            dlg.IsSecondaryButtonEnabled = step > 0;
            dlg.PrimaryButtonText = step == pages.Length - 1 ? "Create" : "Next";
            if (step == 2)
            {
                var p = Presets.ById(f.OsId);
                summary.Text = $"{f.Name.Text}\n{p?.Name ?? "Custom"}\n\nMemory: {(int)f.Ram.Value} MB\nProcessors: {(int)f.Cores.Value}\n" +
                               $"Disk: {(int)f.Disk.Value} GB\nInstaller: {(string.IsNullOrWhiteSpace(f.Iso.Text) ? "none (empty machine)" : f.Iso.Text)}";
            }
        }
        dlg.PrimaryButtonClick += (_, a) => { if (step < pages.Length - 1) { a.Cancel = true; step++; Render(); } };
        dlg.SecondaryButtonClick += (_, a) => { a.Cancel = true; if (step > 0) step--; Render(); };
        Render();

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return null;
        f.Apply();
        return vm;
    }
}
