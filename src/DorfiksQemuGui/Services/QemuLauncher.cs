using System.Diagnostics;
using DorfiksQemuGui.Models;

namespace DorfiksQemuGui.Services;

public static class QemuLauncher
{
    public static async Task EnsureDiskAsync(VmConfig vm, string qemuDir)
    {
        if (string.IsNullOrWhiteSpace(vm.DiskPath))
        {
            Directory.CreateDirectory(VmStore.DisksDir);
            var safe = string.Concat(vm.Name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
            if (safe.Length == 0) safe = "machine";
            vm.DiskPath = Path.Combine(VmStore.DisksDir, $"{safe}-{vm.Id[..6]}.qcow2");
        }
        if (File.Exists(vm.DiskPath)) return;
        var psi = new ProcessStartInfo(Path.Combine(qemuDir, "qemu-img.exe"),
            $"create -f qcow2 \"{vm.DiskPath}\" {vm.DiskGb}G")
        { CreateNoWindow = true, UseShellExecute = false };
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
    }

    public static string BuildArgs(VmConfig vm, string qemuDir)
    {
        var accel = vm.UseWhpx && Whpx.IsAvailable() ? "-accel whpx,kernel-irqchip=off -accel tcg" : "-accel tcg,thread=multi,tb-size=1024";
        var a = new List<string>
        {
            $"-name \"{vm.Name}\"",
            $"-machine {vm.Machine}",
            accel,
            $"-cpu {vm.Cpu}",
            $"-smp {vm.Cores}",
            $"-m {vm.RamMb}",
        };
        // Disk
        switch (vm.DiskBus)
        {
            case "virtio": a.Add($"-drive file=\"{vm.DiskPath}\",if=virtio,format=qcow2"); break;
            case "sata":
                a.Add($"-drive file=\"{vm.DiskPath}\",if=none,id=d0,format=qcow2");
                a.Add(vm.Machine == "q35" ? "-device ide-hd,drive=d0,bus=ide.0" : "-device ahci,id=ahci -device ide-hd,drive=d0,bus=ahci.0");
                break;
            default: a.Add($"-drive file=\"{vm.DiskPath}\",if=ide,format=qcow2"); break;
        }
        // Network (user-mode NAT, like VirtualBox's default)
        a.Add($"-device {vm.Net},netdev=n0 -netdev user,id=n0");
        // Display
        a.Add(vm.Video == "virtio" ? "-device virtio-vga" : $"-vga {vm.Video}");
        a.Add("-display sdl");
        if (!string.IsNullOrWhiteSpace(vm.IsoPath))
            a.Add($"-drive file=\"{vm.IsoPath}\",media=cdrom -boot d");
        if (vm.Uefi)
        {
            var fd = Path.Combine(qemuDir, "share", "edk2-x86_64-code.fd");
            if (File.Exists(fd)) a.Add($"-drive if=pflash,format=raw,readonly=on,file=\"{fd}\"");
        }
        if (vm.UsbTablet) a.Add("-usb -device usb-tablet");
        if (vm.LocalClock) a.Add("-rtc base=localtime");
        if (vm.Audio) switch (vm.Sound)
        {
            case "hda": a.Add("-audiodev dsound,id=snd0 -device intel-hda -device hda-duplex,audiodev=snd0"); break;
            case "ac97": a.Add("-audiodev dsound,id=snd0 -device AC97,audiodev=snd0"); break;
            case "sb16": a.Add("-audiodev dsound,id=snd0 -device sb16,audiodev=snd0"); break;
        }
        if (!string.IsNullOrWhiteSpace(vm.ExtraArgs)) a.Add(vm.ExtraArgs);
        return string.Join(' ', a);
    }

    public static async Task StartAsync(VmConfig vm, string qemuDir)
    {
        await EnsureDiskAsync(vm, qemuDir);
        Process.Start(new ProcessStartInfo(Path.Combine(qemuDir, "qemu-system-x86_64.exe"), BuildArgs(vm, qemuDir))
        { UseShellExecute = false, WorkingDirectory = qemuDir });
    }
}
