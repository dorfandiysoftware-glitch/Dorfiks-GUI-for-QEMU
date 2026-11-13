using System.Text.Json;
using DorfiksQemuGui.Models;

namespace DorfiksQemuGui.Services;

public static class VmStore
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DorfiksQemuGui");
    public static string DefaultDisksDir => Path.Combine(Root, "disks");
    public static string DisksDir =>
        string.IsNullOrWhiteSpace(AppSettings.Current.DisksFolder) ? DefaultDisksDir : AppSettings.Current.DisksFolder;
    static string File_ => Path.Combine(Root, "vms.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static List<VmConfig> Load()
    {
        try
        {
            if (File.Exists(File_))
                return JsonSerializer.Deserialize<List<VmConfig>>(File.ReadAllText(File_)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(IEnumerable<VmConfig> vms)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(File_, JsonSerializer.Serialize(vms, Opt));
    }
}
