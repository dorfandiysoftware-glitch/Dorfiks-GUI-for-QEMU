using System.Text.RegularExpressions;
using DorfiksQemuGui.Models;
using Microsoft.Win32;

namespace DorfiksQemuGui.Services.Engines;

/// <summary>Drives VirtualBox through VBoxManage.exe.</summary>
public sealed class VirtualBoxEngine : Engine
{
    public override string Id => "virtualbox";
    public override string Name => "VirtualBox";
    public override string Tagline => "Fast and beginner-friendly. Best choice on PCs where Windows' own hypervisor is off.";

    public override string? Find()
    {
        var dirs = new List<string>();
        try { dirs.Add((string?)Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Oracle\VirtualBox")?.GetValue("InstallDir") ?? ""); } catch { }
        dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Oracle", "VirtualBox"));
        foreach (var d in dirs)
            if (!string.IsNullOrWhiteSpace(d) && File.Exists(Path.Combine(d, "VBoxManage.exe"))) return d;
        return null;
    }

    string Mgr => Path.Combine(Find() ?? throw new InvalidOperationException("VirtualBox is not installed."), "VBoxManage.exe");

    public override async Task InstallAsync(IProgress<(string Status, double Percent)> progress, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DorfiksQemuGui/0.1");
        const string root = "https://download.virtualbox.org/virtualbox/";
        progress.Report(("Looking for the latest VirtualBox…", 0));
        var version = (await http.GetStringAsync(root + "LATEST.TXT", ct)).Trim();
        var html = await http.GetStringAsync($"{root}{version}/", ct);
        var file = Regex.Match(html, @"VirtualBox-[0-9.]+-[0-9]+-Win\.exe").Value;
        if (file.Length == 0) throw new InvalidOperationException("Could not find the VirtualBox installer for Windows.");
        var tmp = Path.Combine(Path.GetTempPath(), file);
        await DownloadAsync(http, $"{root}{version}/{file}", tmp, "VirtualBox", progress, ct);
        progress.Report(("Installing VirtualBox… (approve the administrator prompt)", 100));
        var (code, _) = await Run(tmp, "--silent --ignore-reboot", elevate: true, ct: ct);
        try { File.Delete(tmp); } catch { }
        if (Find() == null) throw new InvalidOperationException($"VirtualBox setup finished with code {code}, but VBoxManage.exe was not found.");
    }

    static readonly Dictionary<string, string> OsTypes = new()
    {
        ["win11"] = "Windows11_64", ["win10"] = "Windows10_64", ["win81"] = "Windows81_64", ["win8"] = "Windows8_64",
        ["win7"] = "Windows7_64", ["vista"] = "WindowsVista_64", ["winxp"] = "WindowsXP", ["win2k"] = "Windows2000",
        ["win98"] = "Windows98", ["win2022"] = "Windows2022_64", ["win2019"] = "Windows2019_64", ["win2016"] = "Windows2016_64",
        ["ubuntu"] = "Ubuntu_64", ["debian"] = "Debian_64", ["fedora"] = "Fedora_64", ["arch"] = "ArchLinux_64",
        ["manjaro"] = "ArchLinux_64", ["mint"] = "Ubuntu_64", ["opensuse"] = "OpenSUSE_64", ["kali"] = "Debian_64",
        ["popos"] = "Ubuntu_64", ["zorin"] = "Ubuntu_64", ["gentoo"] = "Gentoo_64", ["tinycore"] = "Linux26",
        ["freebsd"] = "FreeBSD_64", ["openbsd"] = "OpenBSD_64", ["netbsd"] = "NetBSD_64",
        ["openindiana"] = "OpenSolaris_64", ["solaris"] = "Solaris11_64", ["reactos"] = "Windows2003",
        ["kolibri"] = "Other", ["freedos"] = "FreeDOS", ["msdos"] = "DOS",
    };

    static string OsType(VmConfig vm) =>
        OsTypes.TryGetValue(vm.OsId, out var t) ? t : vm.Group == "Linux" || vm.OsId is "android" or "chromeos" ? "Linux26_64" : "Other_64";

    static string Q(string s) => "\"" + s.Replace("\"", "") + "\"";

    public override async Task StartAsync(VmConfig vm)
    {
        var mgr = Mgr;
        if (string.IsNullOrEmpty(vm.EngineRef)) vm.EngineRef = $"{SafeName(vm.Name)}-{vm.Id[..4]}";
        var name = vm.EngineRef;
        var exists = (await Run(mgr, $"showvminfo {Q(name)} --machinereadable")).Code == 0;

        var bus = vm.DiskBus == "ide" ? "ide" : "sata";
        var vram = vm.Cpu == "pentium3" ? 16 : 128;
        var gfx = vm.Video switch { "cirrus" => "vboxvga", "std" when vm.Group == "Windows" && vm.RamMb < 1024 => "vboxvga", "vmware" or "virtio" => "vmsvga", _ => vm.Group == "Linux" ? "vmsvga" : "vboxsvga" };
        var nic = vm.Net switch { "virtio-net-pci" => "virtio", "e1000e" => "82545EM", "e1000" => "82540EM", _ => "Am79C973" };
        var audio = vm.Sound switch { "ac97" => "ac97", "sb16" => "sb16", _ => "hda" };
        var modify = $"modifyvm {Q(name)} --memory {vm.RamMb} --cpus {vm.Cores} --firmware {(vm.Uefi ? "efi" : "bios")} " +
                     $"--ioapic on --graphicscontroller {gfx} --vram {vram} --nic1 nat --nictype1 {nic} " +
                     (vm.Audio ? $"--audio-driver dsound --audio-controller {audio} --audio-enabled on " : "--audio-enabled off ") +
                     $"--mouse {(vm.UsbTablet ? "usbtablet" : "ps2")} --rtcuseutc {(vm.LocalClock ? "off" : "on")} " +
                     $"--chipset {(vm.Machine == "q35" ? "ich9" : "piix3")} --boot1 dvd --boot2 disk --boot3 none --boot4 none";
        var iso = string.IsNullOrWhiteSpace(vm.IsoPath) ? "emptydrive" : Q(vm.IsoPath);

        if (!exists)
        {
            var (c, o) = await Run(mgr, $"createvm --name {Q(name)} --ostype {OsType(vm)} --register");
            if (c != 0) (c, o) = await Run(mgr, $"createvm --name {Q(name)} --ostype Other_64 --register");
            if (c != 0) throw new InvalidOperationException("VirtualBox could not create the machine:\n" + o);

            if (string.IsNullOrEmpty(vm.DiskPath) || !vm.DiskPath.EndsWith(".vdi", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(VmStore.DisksDir);
                vm.DiskPath = Path.Combine(VmStore.DisksDir, $"{SafeName(vm.Name)}-{vm.Id[..6]}.vdi");
            }
            if (!File.Exists(vm.DiskPath))
            {
                (c, o) = await Run(mgr, $"createmedium disk --filename {Q(vm.DiskPath)} --size {vm.DiskGb * 1024} --format VDI");
                if (c != 0) throw new InvalidOperationException("VirtualBox could not create the disk:\n" + o);
            }
            var ctl = bus == "ide" ? "--name IDE --add ide --controller PIIX4" : "--name SATA --add sata --controller IntelAhci";
            var ctlName = bus == "ide" ? "IDE" : "SATA";
            await Run(mgr, $"storagectl {Q(name)} {ctl}");
            await Run(mgr, $"storageattach {Q(name)} --storagectl {ctlName} --port 0 --device 0 --type hdd --medium {Q(vm.DiskPath)}");
            await Run(mgr, $"storageattach {Q(name)} --storagectl {ctlName} --port 1 --device 0 --type dvddrive --medium {iso}");
        }
        else
        {
            // Settings may have changed since the machine was created; these fail harmlessly if it is already running.
            var ctlName = bus == "ide" ? "IDE" : "SATA";
            await Run(mgr, $"storageattach {Q(name)} --storagectl {ctlName} --port 1 --device 0 --type dvddrive --medium {iso}");
        }
        var (mc, mo) = await Run(mgr, modify);
        if (mc != 0 && !exists) throw new InvalidOperationException("VirtualBox rejected the settings:\n" + mo);

        var (sc, so) = await Run(mgr, $"startvm {Q(name)} --type gui");
        if (sc != 0) throw new InvalidOperationException("VirtualBox could not start the machine:\n" + so);
    }
}
