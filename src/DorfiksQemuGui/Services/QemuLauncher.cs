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
            vm.DiskPath = Path.Combine(VmStore.DisksDir, vm.Id + ".qcow2");
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
        var a = new List<string>
        {
            $"-name \"{vm.Name}\"",
            "-machine q35",
            vm.UseWhpx && Whpx.IsAvailable() ? "-accel whpx,kernel-irqchip=off -accel tcg" : "-accel tcg,thread=multi",
            "-cpu max",
            $"-smp {vm.Cores}",
            $"-m {vm.RamMb}",
            $"-drive file=\"{vm.DiskPath}\",if=virtio,format=qcow2",
            "-device virtio-net-pci,netdev=n0 -netdev user,id=n0",
            "-vga std -display sdl",
        };
        if (!string.IsNullOrWhiteSpace(vm.IsoPath))
            a.Add($"-drive file=\"{vm.IsoPath}\",media=cdrom -boot d");
        if (vm.Uefi)
        {
            var fd = Path.Combine(qemuDir, "share", "edk2-x86_64-code.fd");
            if (File.Exists(fd)) a.Add($"-drive if=pflash,format=raw,readonly=on,file=\"{fd}\"");
        }
        if (vm.UsbTablet) a.Add("-usb -device usb-tablet");
        if (vm.Audio) a.Add("-audiodev dsound,id=snd0 -device intel-hda -device hda-duplex,audiodev=snd0");
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
