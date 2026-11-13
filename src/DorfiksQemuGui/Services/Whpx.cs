using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DorfiksQemuGui.Services;

/// <summary>Detects and enables the Windows Hypervisor Platform that QEMU needs for hardware acceleration.</summary>
public static class Whpx
{
    [DllImport("WinHvPlatform.dll")]
    private static extern int WHvGetCapability(int code, out int buffer, uint size, out uint written);

    private static bool? _cached;

    /// <summary>True when the platform DLL is present (the Windows feature is installed).</summary>
    public static bool FeatureInstalled { get; private set; }

    /// <summary>Why the last check failed, shown to the user.</summary>
    public static string Diagnostic { get; private set; } = "";

    public static bool IsAvailable(bool refresh = false)
    {
        if (!refresh && _cached is bool c) return c;
        try
        {
            // WHvCapabilityCodeHypervisorPresent = 0
            FeatureInstalled = true;
            var hr = WHvGetCapability(0, out var present, 4, out _);
            _cached = hr >= 0 && present != 0;
            Diagnostic = _cached.Value ? "" : $"WHvGetCapability hr=0x{hr:X8}, hypervisor present={present}";
        }
        catch (Exception ex) { _cached = false; FeatureInstalled = !(ex is DllNotFoundException); Diagnostic = "WinHvPlatform.dll not available (" + ex.GetType().Name + ")"; }
        return _cached.Value;
    }

    /// <summary>Turns the optional Windows feature on (asks for administrator rights). A restart is required afterwards.</summary>
    public static async Task<bool> EnableAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -Command \"Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All -NoRestart | Out-Null; " +
                "Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -All -NoRestart | Out-Null; " +
                "bcdedit /set hypervisorlaunchtype auto\"")
            { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            using var p = Process.Start(psi);
            if (p == null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch (Exception) { return false; } // UAC declined
    }
}
