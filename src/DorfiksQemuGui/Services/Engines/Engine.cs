using System.Diagnostics;
using DorfiksQemuGui.Models;

namespace DorfiksQemuGui.Services.Engines;

/// <summary>A virtualization product the app can drive: QEMU, VirtualBox or VMware Workstation.</summary>
public abstract class Engine
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Tagline { get; }
    /// <summary>Main executable/folder when installed, otherwise null.</summary>
    public abstract string? Find();
    public abstract Task InstallAsync(IProgress<(string Status, double Percent)> progress, CancellationToken ct = default);
    public abstract Task StartAsync(VmConfig vm);

    public bool Installed => Find() != null;

    public static readonly Engine[] All = { new QemuEngine(), new VirtualBoxEngine(), new VmwareEngine() };
    public static Engine ById(string? id) => All.FirstOrDefault(e => e.Id == id) ?? All[0];

    // ---- helpers shared by the engines ----
    protected static string SafeName(string s)
    {
        var safe = string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')).Trim('_');
        return safe.Length == 0 ? "vm" : safe;
    }

    protected static async Task<(int Code, string Output)> Run(string exe, string args, bool elevate = false, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe, args) { CreateNoWindow = true };
        if (elevate) { psi.UseShellExecute = true; psi.Verb = "runas"; }
        else { psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true; }
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start " + Path.GetFileName(exe));
        if (elevate) { await p.WaitForExitAsync(ct); return (p.ExitCode, ""); }
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, (await o + await e).Trim());
    }

    protected static async Task DownloadAsync(HttpClient http, string url, string dest, string label,
        IProgress<(string Status, double Percent)> progress, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(dest);
        var buf = new byte[81920];
        long done = 0; int n;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            done += n;
            if (total > 0) progress.Report(($"Downloading {label}… {done / 1048576} / {total / 1048576} MB", done * 100.0 / total));
        }
    }
}

public sealed class QemuEngine : Engine
{
    public override string Id => "qemu";
    public override string Name => "QEMU";
    public override string Tagline => "Free and flexible, runs almost anything. Slow without hardware acceleration.";
    public override string? Find() => QemuLocator.Find();
    public override async Task InstallAsync(IProgress<(string Status, double Percent)> progress, CancellationToken ct = default)
        => await QemuInstaller.InstallAsync(progress, ct);
    public override async Task StartAsync(VmConfig vm)
    {
        var dir = QemuLocator.Find() ?? throw new InvalidOperationException("QEMU is not installed.");
        await QemuLauncher.StartAsync(vm, dir);
    }
}
