using System.Text.Json;

namespace DorfiksQemuGui.Services;

public class AppSettings
{
    public string QemuDir { get; set; } = "";
    public bool AutoInstallQemu { get; set; } = true;
    /// <summary>Where new virtual disks are created. Empty = default folder next to the app data.</summary>
    public string DisksFolder { get; set; } = "";

    static string P => Path.Combine(VmStore.Root, "settings.json");
    public static AppSettings Current { get; } = Load();

    static AppSettings Load()
    {
        try { if (File.Exists(P)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(P)) ?? new(); }
        catch { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(VmStore.Root);
        File.WriteAllText(P, JsonSerializer.Serialize(this));
    }
}
