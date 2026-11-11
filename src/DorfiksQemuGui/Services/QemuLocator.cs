namespace DorfiksQemuGui.Services;

public static class QemuLocator
{
    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "QEMU");

    /// <summary>Returns the folder containing qemu-system-x86_64.exe, or null.</summary>
    public static string? Find()
    {
        var candidates = new List<string>();
        var custom = AppSettings.Current.QemuDir;
        if (!string.IsNullOrWhiteSpace(custom)) candidates.Add(custom);
        candidates.Add(DefaultInstallDir);
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "qemu"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "qemu"));
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        candidates.AddRange(path.Split(';', StringSplitOptions.RemoveEmptyEntries));

        foreach (var dir in candidates)
        {
            try
            {
                if (File.Exists(Path.Combine(dir.Trim('"'), "qemu-system-x86_64.exe")))
                    return dir.Trim('"');
            }
            catch { }
        }
        return null;
    }
}
