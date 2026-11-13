namespace DorfiksQemuGui.Models;

public class VmConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New machine";
    public string Preset { get; set; } = "Custom";
    public int RamMb { get; set; } = 4096;
    public int Cores { get; set; } = 2;
    public int DiskGb { get; set; } = 64;
    public string DiskPath { get; set; } = "";
    public string IsoPath { get; set; } = "";
    public bool Uefi { get; set; } = true;
    public bool UseWhpx { get; set; } = true;
    public bool UsbTablet { get; set; } = true;
    public bool Audio { get; set; } = true;
    public string ExtraArgs { get; set; } = "";

    // Added with presets (all have defaults so old vms.json files keep loading).
    public string OsId { get; set; } = "";
    public string Machine { get; set; } = "q35";      // q35 | pc
    public string Cpu { get; set; } = "max";          // max | qemu64 | pentium3 ...
    public string DiskBus { get; set; } = "virtio";   // virtio | sata | ide
    public string Net { get; set; } = "virtio-net-pci"; // virtio-net-pci | e1000 | e1000e | rtl8139
    public string Video { get; set; } = "std";        // std | virtio | cirrus | vmware
    public string Sound { get; set; } = "hda";        // hda | ac97 | sb16 | none
    public bool LocalClock { get; set; }              // Windows expects local time in the RTC
    public string Group { get; set; } = "Other";

    /// <summary>qemu | virtualbox | vmware</summary>
    public string Engine { get; set; } = "qemu";
    /// <summary>Name inside VirtualBox, or path of the .vmx for VMware.</summary>
    public string EngineRef { get; set; } = "";

    public string Summary => $"{RamMb / 1024.0:0.#} GB RAM · {Cores} cores · {DiskGb} GB disk";
}
