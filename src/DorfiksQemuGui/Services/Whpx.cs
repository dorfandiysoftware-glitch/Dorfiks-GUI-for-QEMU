using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DorfiksQemuGui.Services;

/// <summary>Detects and enables the Windows Hypervisor Platform that QEMU needs for hardware acceleration.</summary>
public static class Whpx
{
    [DllImport("WinHvPlatform.dll")]
    private static extern int WHvGetCapability(int code, out int buffer, uint size, out uint written);

    private static bool? _cached;

    public static bool IsAvailable(bool refresh = false)
    {
        if (!refresh && _cached is bool c) return c;
        try
        {
            // WHvCapabilityCodeHypervisorPresent = 0
            var hr = WHvGetCapability(0, out var present, 4, out _);
            _cached = hr >= 0 && present != 0;
        }
        catch (Exception) { _cached = false; } // DLL missing => feature is off
        return _cached.Value;
    }

    /// <summary>Turns the optional Windows feature on (asks for administrator rights). A restart is required afterwards.</summary>
    public static async Task<bool> EnableAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -Command \"Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All -NoRestart\"")
            { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            using var p = Process.Start(psi);
            if (p == null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch (Exception) { return false; } // UAC declined
    }
}
