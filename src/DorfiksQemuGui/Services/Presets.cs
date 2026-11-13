namespace DorfiksQemuGui.Services;

/// <summary>Recommended virtual hardware for a guest OS, so beginners never touch QEMU flags.</summary>
public record OsPreset(
    string Id, string Group, string Name,
    int Ram, int Cores, int Disk, bool Uefi,
    string Machine, string Cpu, string DiskBus, string Net, string Video, string Sound,
    bool LocalClock, string Note);

public static class Presets
{
    public static readonly string[] Groups = { "Windows", "Linux", "BSD / Unix", "Other" };

    // Windows has no virtio drivers out of the box, so it gets SATA/IDE disks and Intel/Realtek NICs.
    static OsPreset Win(string id, string name, int ram, int cores, int disk, bool uefi, string machine,
        string bus, string net, string video, string sound, string cpu = "max", string note = "") =>
        new(id, "Windows", name, ram, cores, disk, uefi, machine, cpu, bus, net, video, sound, true, note);

    static OsPreset Lin(string id, string name, int ram, int disk, string note = "", int cores = 2) =>
        new(id, "Linux", name, ram, cores, disk, true, "q35", "max", "virtio", "virtio-net-pci", "virtio", "hda", false, note);

    static OsPreset Bsd(string id, string name, int ram, int disk, string note = "") =>
        new(id, "BSD / Unix", name, ram, 2, disk, true, "q35", "max", "virtio", "virtio-net-pci", "std", "hda", false, note);

    static OsPreset Oth(string id, string name, int ram, int cores, int disk, bool uefi, string machine,
        string cpu, string bus, string net, string video, string sound, string note = "") =>
        new(id, "Other", name, ram, cores, disk, uefi, machine, cpu, bus, net, video, sound, false, note);

    public static readonly OsPreset[] All =
    {
        // ---- Windows ----
        Win("win11", "Windows 11", 4096, 4, 64, true, "q35", "sata", "e1000e", "std", "hda",
            note: "Needs UEFI. The installer checks TPM/Secure Boot: use an ISO with the checks removed, or press Shift+F10 and run regedit to add BypassTPMCheck."),
        Win("win10", "Windows 10", 4096, 2, 48, true, "q35", "sata", "e1000e", "std", "hda"),
        Win("win81", "Windows 8.1", 2048, 2, 40, false, "q35", "sata", "e1000", "std", "hda"),
        Win("win8", "Windows 8", 2048, 2, 40, false, "q35", "sata", "e1000", "std", "hda"),
        Win("win7", "Windows 7", 2048, 2, 32, false, "pc", "ide", "e1000", "std", "ac97", note: "No sound driver in the installer; AC97 works after installing Realtek AC97 drivers."),
        Win("vista", "Windows Vista", 1536, 2, 25, false, "pc", "ide", "e1000", "std", "ac97"),
        Win("winxp", "Windows XP", 768, 1, 12, false, "pc", "ide", "rtl8139", "cirrus", "ac97", "pentium3",
            note: "Uses an IDE disk and Realtek network card that XP supports out of the box."),
        Win("win2k", "Windows 2000", 512, 1, 8, false, "pc", "ide", "rtl8139", "cirrus", "sb16", "pentium3"),
        Win("win98", "Windows 98 / ME", 256, 1, 4, false, "pc", "ide", "rtl8139", "cirrus", "sb16", "pentium3",
            note: "Keep RAM below 512 MB. Disk should stay under 8 GB."),
        Win("win2022", "Windows Server 2022", 4096, 2, 64, true, "q35", "sata", "e1000e", "std", "hda"),
        Win("win2019", "Windows Server 2019", 4096, 2, 64, true, "q35", "sata", "e1000e", "std", "hda"),
        Win("win2016", "Windows Server 2016", 3072, 2, 50, false, "q35", "sata", "e1000e", "std", "hda"),

        // ---- Linux ----
        Lin("ubuntu", "Ubuntu", 4096, 25, "Also fits Kubuntu, Xubuntu, Lubuntu and Ubuntu MATE."),
        Lin("debian", "Debian", 2048, 20),
        Lin("fedora", "Fedora", 4096, 25),
        Lin("arch", "Arch Linux", 2048, 20, "The live ISO boots to a shell; run archinstall to install."),
        Lin("manjaro", "Manjaro", 4096, 30),
        Lin("mint", "Linux Mint", 4096, 25),
        Lin("opensuse", "openSUSE", 4096, 30),
        Lin("kali", "Kali Linux", 4096, 40),
        Lin("popos", "Pop!_OS", 4096, 30),
        Lin("zorin", "Zorin OS", 4096, 30),
        Lin("nixos", "NixOS", 2048, 25),
        Lin("void", "Void Linux", 1024, 15),
        Lin("gentoo", "Gentoo", 4096, 50, cores: 4),
        Lin("alpine", "Alpine Linux", 512, 8, "Tiny and fast, a good first Linux for slow computers."),
        Lin("tinycore", "Tiny Core Linux", 256, 2, "Runs from RAM, 16 MB ISO."),
        Lin("linux-other", "Other Linux", 2048, 20),

        // ---- BSD / Unix ----
        Bsd("freebsd", "FreeBSD", 2048, 20),
        Bsd("openbsd", "OpenBSD", 1024, 16),
        Bsd("netbsd", "NetBSD", 1024, 16),
        Bsd("openindiana", "OpenIndiana / illumos", 3072, 24),
        Bsd("solaris", "Oracle Solaris", 4096, 32),

        // ---- Other ----
        Oth("android", "Android-x86 / BlissOS", 3072, 2, 16, false, "q35", "max", "sata", "e1000", "std", "hda",
            "Choose 'Live CD / Debug mode' first to test before installing."),
        Oth("chromeos", "ChromeOS Flex", 4096, 2, 32, true, "q35", "max", "sata", "e1000", "std", "hda"),
        Oth("reactos", "ReactOS", 1024, 1, 8, false, "pc", "pentium3", "ide", "rtl8139", "std", "ac97",
            "Open-source Windows clone, still alpha: use the 'Boot from ISO' installer."),
        Oth("haiku", "Haiku", 2048, 2, 16, true, "q35", "max", "sata", "e1000", "std", "hda"),
        Oth("kolibri", "KolibriOS", 128, 1, 1, false, "pc", "pentium3", "ide", "rtl8139", "std", "ac97",
            "Fits on a floppy: 128 MB RAM is plenty."),
        Oth("freedos", "FreeDOS", 64, 1, 2, false, "pc", "pentium3", "ide", "rtl8139", "cirrus", "sb16",
            "Disk can be 2 GB; many old DOS games work with Sound Blaster 16."),
        Oth("msdos", "MS-DOS / PC-DOS", 32, 1, 1, false, "pc", "pentium3", "ide", "rtl8139", "cirrus", "sb16"),
        Oth("templeos", "TempleOS", 512, 1, 2, false, "pc", "qemu64", "ide", "rtl8139", "std", "ac97"),
        Oth("custom", "Custom (nothing preset)", 2048, 2, 20, true, "q35", "max", "virtio", "virtio-net-pci", "std", "hda"),
    };

    public static IEnumerable<OsPreset> InGroup(string group) => All.Where(p => p.Group == group);
    public static OsPreset? ById(string id) => All.FirstOrDefault(p => p.Id == id);
}
