using DorfiksQemuGui.Models;
using DorfiksQemuGui.Services;
using DorfiksQemuGui.Services.Engines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DorfiksQemuGui.Views;

/// <summary>All the editable controls of a machine, shared by the wizard and the settings dialog.</summary>
public sealed class VmForm
{
    readonly VmConfig vm;
    public TextBox Name = new() { Header = "Name" };
    public TextBox Iso = new() { PlaceholderText = "Optional: path to an installer .iso", HorizontalAlignment = HorizontalAlignment.Stretch };
    public Slider Ram = new() { Minimum = 128, Maximum = 32768, StepFrequency = 128 };
    public Slider Cores = new() { Minimum = 1, Maximum = Math.Max(1, Environment.ProcessorCount), StepFrequency = 1 };
    public Slider Disk = new() { Minimum = 1, Maximum = 512, StepFrequency = 1 };
    public ToggleSwitch Uefi = new() { Header = "UEFI firmware" };
    public ToggleSwitch Whpx = new() { Header = "Hardware acceleration (WHPX, when available)" };
    public ToggleSwitch Tablet = new() { Header = "USB tablet (seamless mouse)" };
    public ToggleSwitch Clock = new() { Header = "Hardware clock in local time (Windows)" };
    public ComboBox Machine = Combo("Chipset", "q35", "pc");
    public ComboBox Cpu = Combo("Processor model", "max", "qemu64", "pentium3");
    public ComboBox Bus = Combo("Disk controller", "virtio", "sata", "ide");
    public ComboBox Net = Combo("Network adapter", "virtio-net-pci", "e1000", "e1000e", "rtl8139");
    public ComboBox Video = Combo("Graphics", "std", "virtio", "cirrus", "vmware");
    public ComboBox Sound = Combo("Audio", "hda", "ac97", "sb16", "none");
    public TextBox Extra = new() { Header = "Extra QEMU arguments" };
    public TextBlock RamLabel = new(), CoresLabel = new(), DiskLabel = new();
    public TextBlock Note = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.75 };
    public string OsId = "", Group = "Other";
    public string EngineId = "qemu";
    readonly ComboBox engineBox = new() { Header = "Virtualization engine", HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock engineNote = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.75 };
    readonly Button engineInstall = new() { Content = "Install" };

    void RefreshEngines()
    {
        var keep = EngineId;
        engineBox.Items.Clear();
        foreach (var e in Engine.All)
            engineBox.Items.Add(new ComboBoxItem { Content = e.Installed ? e.Name : e.Name + " (not installed)", Tag = e });
        for (var i = 0; i < engineBox.Items.Count; i++)
            if (((ComboBoxItem)engineBox.Items[i]).Tag is Engine en && en.Id == keep) engineBox.SelectedIndex = i;
    }

    /// <summary>Picker for QEMU / VirtualBox / VMware with an Install button when the product is missing.</summary>
    public FrameworkElement EnginePicker(bool locked = false)
    {
        RefreshEngines();
        engineBox.IsEnabled = !locked;
        void Update()
        {
            if (engineBox.SelectedItem is not ComboBoxItem { Tag: Engine e }) return;
            EngineId = e.Id;
            engineNote.Text = e.Tagline;
            engineInstall.Visibility = !locked && !e.Installed ? Visibility.Visible : Visibility.Collapsed;
        }
        engineBox.SelectionChanged += (_, _) => Update();
        engineInstall.Click += async (_, _) =>
        {
            if (engineBox.SelectedItem is not ComboBoxItem { Tag: Engine e } || App.MainWindow == null) return;
            engineInstall.IsEnabled = false;
            await App.MainWindow.EnsureEngineAsync(e);
            engineInstall.IsEnabled = true;
            RefreshEngines();
            Update();
        };
        Update();
        var p = new StackPanel { Spacing = 6 };
        p.Children.Add(engineBox); p.Children.Add(engineNote); p.Children.Add(engineInstall);
        return p;
    }

    static ComboBox Combo(string header, params string[] items)
    {
        var c = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var i in items) c.Items.Add(i);
        return c;
    }

    public VmForm(VmConfig vm)
    {
        this.vm = vm;
        Name.Text = vm.Name; Iso.Text = vm.IsoPath;
        Ram.Value = vm.RamMb; Cores.Value = vm.Cores; Disk.Value = vm.DiskGb;
        Uefi.IsOn = vm.Uefi; Whpx.IsOn = vm.UseWhpx; Tablet.IsOn = vm.UsbTablet; Clock.IsOn = vm.LocalClock;
        Machine.SelectedItem = vm.Machine; Cpu.SelectedItem = vm.Cpu; Bus.SelectedItem = vm.DiskBus;
        Net.SelectedItem = vm.Net; Video.SelectedItem = vm.Video; Sound.SelectedItem = vm.Audio ? vm.Sound : "none";
        Extra.Text = vm.ExtraArgs; OsId = vm.OsId; Group = vm.Group;
        // New machines: prefer VirtualBox when it is installed and QEMU has no hardware acceleration here.
        EngineId = vm.Engine;
        if (string.IsNullOrEmpty(vm.OsId) && !Services.Whpx.IsAvailable() && Engine.ById("virtualbox").Installed) EngineId = "virtualbox";
        Ram.ValueChanged += (_, _) => UpdateLabels();
        Cores.ValueChanged += (_, _) => UpdateLabels();
        Disk.ValueChanged += (_, _) => UpdateLabels();
        UpdateLabels();
    }

    void UpdateLabels()
    {
        RamLabel.Text = $"Memory: {(int)Ram.Value} MB ({Ram.Value / 1024:0.#} GB)";
        CoresLabel.Text = $"Processors: {(int)Cores.Value} of {Environment.ProcessorCount}";
        DiskLabel.Text = $"Disk size: {(int)Disk.Value} GB (grows as you use it)";
    }

    public void ApplyPreset(OsPreset p)
    {
        OsId = p.Id; Group = p.Group;
        Ram.Value = p.Ram; Cores.Value = Math.Min(p.Cores, Environment.ProcessorCount); Disk.Value = p.Disk;
        Uefi.IsOn = p.Uefi; Clock.IsOn = p.LocalClock;
        Machine.SelectedItem = p.Machine; Cpu.SelectedItem = p.Cpu; Bus.SelectedItem = p.DiskBus;
        Net.SelectedItem = p.Net; Video.SelectedItem = p.Video; Sound.SelectedItem = p.Sound;
        Note.Text = p.Note;
        UpdateLabels();
    }

    public FrameworkElement IsoRow()
    {
        var browse = new Button { Content = "Browse…" };
        browse.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".iso");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var f = await picker.PickSingleFileAsync();
            if (f != null) Iso.Text = f.Path;
        };
        var g = new Grid { ColumnSpacing = 8 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(Iso);
        Grid.SetColumn(browse, 1);
        g.Children.Add(browse);
        var wrap = new StackPanel { Spacing = 4 };
        wrap.Children.Add(new TextBlock { Text = "Installer ISO" });
        wrap.Children.Add(g);
        return wrap;
    }

    public static StackPanel Stack(params UIElement[] items)
    {
        var p = new StackPanel { Spacing = 12 };
        foreach (var i in items) p.Children.Add(i);
        return p;
    }

    public void Apply()
    {
        vm.Name = string.IsNullOrWhiteSpace(Name.Text) ? "Machine" : Name.Text.Trim();
        vm.OsId = OsId; vm.Group = Group; vm.Engine = EngineId;
        vm.Preset = Presets.ById(OsId)?.Name ?? "Custom";
        vm.RamMb = (int)Ram.Value; vm.Cores = (int)Cores.Value; vm.DiskGb = (int)Disk.Value;
        vm.IsoPath = Iso.Text.Trim();
        vm.Uefi = Uefi.IsOn; vm.UseWhpx = Whpx.IsOn; vm.UsbTablet = Tablet.IsOn; vm.LocalClock = Clock.IsOn;
        vm.Machine = Machine.SelectedItem as string ?? "q35"; vm.Cpu = Cpu.SelectedItem as string ?? "max";
        vm.DiskBus = Bus.SelectedItem as string ?? "virtio"; vm.Net = Net.SelectedItem as string ?? "virtio-net-pci"; vm.Video = Video.SelectedItem as string ?? "std";
        vm.Sound = Sound.SelectedItem as string ?? "hda"; vm.Audio = vm.Sound != "none";
        vm.ExtraArgs = Extra.Text.Trim();
    }
}
