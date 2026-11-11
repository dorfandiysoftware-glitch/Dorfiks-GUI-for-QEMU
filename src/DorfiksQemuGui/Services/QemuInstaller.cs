using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DorfiksQemuGui.Services;

/// <summary>Downloads the newest official Windows QEMU build (qemu.weilnetz.de) and installs it silently.</summary>
public static class QemuInstaller
{
    const string Index = "https://qemu.weilnetz.de/w64/";

    public static async Task<string> InstallAsync(IProgress<(string Status, double Percent)> progress, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DorfiksQemuGui/0.1");

        progress.Report(("Looking for the latest QEMU…", 0));
        var html = await http.GetStringAsync(Index, ct);
        var match = Regex.Matches(html, @"qemu-w64-setup-(\d{8})\.exe")
            .Select(m => m.Value).Distinct().OrderByDescending(s => s).FirstOrDefault()
            ?? throw new InvalidOperationException("Could not find a QEMU installer on the download server.");

        var tmp = Path.Combine(Path.GetTempPath(), match);
        using (var resp = await http.GetAsync(Index + match, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(tmp);
            var buf = new byte[81920];
            long done = 0; int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                if (total > 0) progress.Report(($"Downloading QEMU… {done / 1048576} / {total / 1048576} MB", done * 100.0 / total));
            }
        }

        progress.Report(("Installing QEMU…", 100));
        var target = QemuLocator.DefaultInstallDir;
        Directory.CreateDirectory(target);
        var psi = new ProcessStartInfo(tmp, $"/S /D={target}") { UseShellExecute = true };
        using (var p = Process.Start(psi) ?? throw new InvalidOperationException("Installer did not start."))
            await p.WaitForExitAsync(ct);

        try { File.Delete(tmp); } catch { }
        var found = QemuLocator.Find() ?? throw new InvalidOperationException("QEMU installation finished but qemu-system-x86_64.exe was not found.");
        AppSettings.Current.QemuDir = found;
        AppSettings.Current.Save();
        return found;
    }
}
