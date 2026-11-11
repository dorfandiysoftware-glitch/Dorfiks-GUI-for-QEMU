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

    public string Summary => $"{RamMb / 1024.0:0.#} GB RAM · {Cores} cores · {DiskGb} GB disk";
}
