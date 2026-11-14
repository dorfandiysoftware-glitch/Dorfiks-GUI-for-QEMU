using System.Diagnostics;
using System.Text;
using DorfiksQemuGui.Models;
using Microsoft.Win32;

namespace DorfiksQemuGui.Services.Engines;

/// <summary>Drives VMware Workstation: writes a .vmx, creates the disk with vmware-vdiskmanager and starts it with vmrun.</summary>
public sealed class VmwareEngine : Engine
{
    public override string Id => "vmware";
    public override string Name => "VMware Workstation";
    public override string Tagline => "Polished and fast. Free for personal use; needs the Broadcom-hosted installer.";

    public const string DownloadPage = "https://www.vmware.com/products/desktop-hypervisor/workstation-and-fusion";

    public override string? Find()
    {
        var dirs = new List<string>();
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(@"SOFTWARE\VMware, Inc.\VMware Workstation");
                if (k?.GetValue("InstallPath") is string s) dirs.Add(s);
            }
            catch { }
        }
        dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VMware", "VMware Workstation"));
        dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VMware", "VMware Workstation"));
        foreach (var d in dirs)
            if (!string.IsNullOrWhiteSpace(d) && File.Exists(Path.Combine(d, "vmrun.exe"))) return d;
        return null;
    }

    public override async Task InstallAsync(IProgress<(string Status, double Percent)> progress, CancellationToken ct = default)
    {
        // Broadcom hides direct download links behind a login, and the winget package is stale (17.0.1),
        // so we cannot install silently: open the official page and let the user install it.
        await Task.CompletedTask;
        progress.Report(("Opening the VMware download page…", 100));
        try { Process.Start(new ProcessStartInfo(DownloadPage) { UseShellExecute = true }); } catch { }
        throw new InvalidOperationException("VMware Workstation Pro can only be downloaded from the Broadcom site (free account, free for personal use). " +
            "The page was opened: install it, then press Start again.");
    }

    static readonly Dictionary<string, string> GuestOs = new()
    {
        ["win11"] = "windows11-64", ["win10"] = "windows9-64", ["win81"] = "windows8-64", ["win8"] = "windows8-64",
        ["win7"] = "windows7-64", ["vista"] = "winvista-64", ["winxp"] = "winxppro", ["win2k"] = "win2000", ["win98"] = "win98",
        ["win2022"] = "windows2019srvnext-64", ["win2019"] = "windows2019srv-64", ["win2016"] = "windows9srv-64",
        ["ubuntu"] = "ubuntu-64", ["debian"] = "debian10-64", ["fedora"] = "fedora-64", ["opensuse"] = "opensuse-64",
        ["freebsd"] = "freebsd-64", ["solaris"] = "solaris11-64", ["openindiana"] = "solaris11-64",
        ["freedos"] = "dos", ["msdos"] = "dos",
    };

    static string Guest(VmConfig vm) =>
        GuestOs.TryGetValue(vm.OsId, out var g) ? g : vm.Group == "Linux" || vm.OsId is "android" or "chromeos" ? "other5xlinux-64" : "other-64";

    public override async Task StartAsync(VmConfig vm)
    {
        var dir = Find() ?? throw new InvalidOperationException("VMware Workstation is not installed.");
        var vmrun = Path.Combine(dir, "vmrun.exe");
        var vmDir = Path.Combine(VmStore.Root, "vmware", $"{SafeName(vm.Name)}-{vm.Id[..6]}");
        Directory.CreateDirectory(vmDir);
        var vmx = Path.Combine(vmDir, "machine.vmx");
        var vmdk = Path.Combine(vmDir, "disk.vmdk");

        if (!File.Exists(vmdk))
        {
            var mgr = Path.Combine(dir, "vmware-vdiskmanager.exe");
            var (c, o) = await Run(mgr, $"-c -s {vm.DiskGb}GB -a lsilogic -t 0 \"{vmdk}\"");
            if (c != 0) throw new InvalidOperationException("VMware could not create the disk:\n" + o);
        }
        vm.DiskPath = vmdk;
        vm.EngineRef = vmx;

        var ide = vm.DiskBus == "ide";
        var nic = vm.Net switch { "virtio-net-pci" => "vmxnet3", "e1000e" => "e1000e", "e1000" => "e1000", _ => "e1000" };
        var snd = vm.Sound switch { "ac97" => "es1371", "sb16" => "sb16", _ => "hdaudio" };
        var sb = new StringBuilder();
        void L(string k, string v) => sb.Append(k).Append(" = \"").Append(v).Append("\"\n");
        L(".encoding", "UTF-8"); L("config.version", "8"); L("virtualHW.version", "19");
        L("displayName", vm.Name); L("guestOS", Guest(vm));
        L("memsize", vm.RamMb.ToString()); L("numvcpus", vm.Cores.ToString());
        if (vm.Uefi) L("firmware", "efi");
        var bus = ide ? "ide0" : "sata0";
        L($"{bus}.present", "TRUE");
        L($"{bus}:0.present", "TRUE"); L($"{bus}:0.fileName", vmdk);
        L($"{bus}:1.present", "TRUE");
        if (string.IsNullOrWhiteSpace(vm.IsoPath)) { L($"{bus}:1.deviceType", "cdrom-raw"); L($"{bus}:1.fileName", "auto detect"); }
        else { L($"{bus}:1.deviceType", "cdrom-image"); L($"{bus}:1.fileName", vm.IsoPath); }
        L("ethernet0.present", "TRUE"); L("ethernet0.connectionType", "nat"); L("ethernet0.virtualDev", nic); L("ethernet0.addressType", "generated");
        if (vm.Audio) { L("sound.present", "TRUE"); L("sound.virtualDev", snd); L("sound.autoDetect", "TRUE"); }
        L("usb.present", "TRUE"); L("ehci.present", "TRUE");
        L("tools.syncTime", "TRUE"); L("mks.enable3d", "FALSE");
        L("bios.bootDelay", "1000");
        await File.WriteAllTextAsync(vmx, sb.ToString());

        var (sc, so) = await Run(vmrun, $"-T ws start \"{vmx}\" gui");
        if (sc != 0) throw new InvalidOperationException("VMware could not start the machine:\n" + so);
    }
}
