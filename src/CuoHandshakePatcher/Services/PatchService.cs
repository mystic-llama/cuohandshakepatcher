using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace CuoHandshakePatcher.Services;

public enum PatchStatus
{
    Unknown,
    Unpatched,
    Patched,
}

public sealed class PatchService
{
    public string? InstallDirectory { get; private set; }

    public event Action<string>? Log;

    public void SetInstallDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);
        string exe = RazorLocator.ExePath(directory);
        if (!File.Exists(exe))
            throw new FileNotFoundException("RazorEnhanced.exe not found.", exe);
        InstallDirectory = directory;
    }

    public string? GetInstalledVersion()
    {
        if (InstallDirectory == null) return null;
        return FileVersionInfo.GetVersionInfo(RazorLocator.ExePath(InstallDirectory)).FileVersion;
    }

    public PatchStatus GetStatus()
    {
        if (InstallDirectory == null) return PatchStatus.Unknown;
        try
        {
            bool marker = CecilInjector.IsPatched(RazorLocator.ExePath(InstallDirectory));
            bool dll = File.Exists(RazorLocator.FixDllPath(InstallDirectory));
            if (marker && dll) return PatchStatus.Patched;
            if (!marker && !dll) return PatchStatus.Unpatched;
            return PatchStatus.Unknown;
        }
        catch
        {
            return PatchStatus.Unknown;
        }
    }

    public string? FindLatestBackup()
    {
        if (InstallDirectory == null) return null;
        return Directory.GetFiles(InstallDirectory, "RazorEnhanced.exe.bak-*")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public ElevationMode GetElevationMode()
    {
        if (InstallDirectory == null) return ElevationMode.Unknown;
        try { return ManifestElevationService.GetMode(RazorLocator.ExePath(InstallDirectory)); }
        catch { return ElevationMode.Unknown; }
    }

    public void ApplyPatch(bool removeAdminRequirement = false)
    {
        if (InstallDirectory == null)
            throw new InvalidOperationException("No Razor Enhanced folder selected.");

        string exe = RazorLocator.ExePath(InstallDirectory);
        string dllDest = RazorLocator.FixDllPath(InstallDirectory);

        if (FileLockChecker.IsLocked(exe, out string lockReason))
            throw new IOException(lockReason);

        if (ElevationHelper.NeedsElevationFor(InstallDirectory))
        {
            WriteLog("Administrator rights required. Relaunching elevated…");
            ElevationHelper.RelaunchElevated();
            throw new OperationCanceledException("Relaunching elevated.");
        }

        // Always refresh the runtime DLL (safe while RE is closed).
        ExtractFixDll(dllDest);
        WriteLog("CuoHandshakeFix.dll installed/updated.");

        bool alreadyPatched = CecilInjector.IsPatched(exe);
        if (!alreadyPatched)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string bak = Path.Combine(InstallDirectory, $"RazorEnhanced.exe.bak-{stamp}");
            File.Copy(exe, bak, overwrite: false);
            WriteLog($"Backup created: {Path.GetFileName(bak)}");

            try
            {
                CecilInjector.InjectBootstrap(exe, WriteLog);
            }
            catch
            {
                File.Copy(bak, exe, overwrite: true);
                WriteLog("IL injection failed — restored exe from backup.");
                throw;
            }

            WriteLog("Handshake IL bootstrap applied.");
        }
        else
        {
            WriteLog("IL bootstrap already present. Runtime fix DLL refreshed.");
        }

        if (removeAdminRequirement)
        {
            // Ensure a backup exists before rewriting the manifest.
            if (FindLatestBackup() == null)
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string bak = Path.Combine(InstallDirectory, $"RazorEnhanced.exe.bak-{stamp}");
                File.Copy(exe, bak, overwrite: false);
                WriteLog($"Backup created: {Path.GetFileName(bak)}");
            }

            ManifestElevationService.SetAsInvoker(exe, WriteLog);
        }

        WriteLog("Patch update complete.");
    }

    public void RestoreBackup()
    {
        if (InstallDirectory == null)
            throw new InvalidOperationException("No Razor Enhanced folder selected.");

        string exe = RazorLocator.ExePath(InstallDirectory);
        string? bak = FindLatestBackup()
            ?? throw new FileNotFoundException("No RazorEnhanced.exe.bak-* backup found in this folder.");

        if (FileLockChecker.IsLocked(exe, out string lockReason))
            throw new IOException(lockReason);

        if (ElevationHelper.NeedsElevationFor(InstallDirectory))
        {
            WriteLog("Administrator rights required. Relaunching elevated…");
            ElevationHelper.RelaunchElevated();
            throw new OperationCanceledException("Relaunching elevated.");
        }

        File.Copy(bak, exe, overwrite: true);
        WriteLog($"Restored from {Path.GetFileName(bak)}");

        string dll = RazorLocator.FixDllPath(InstallDirectory);
        if (File.Exists(dll))
        {
            File.Delete(dll);
            WriteLog("Removed CuoHandshakeFix.dll");
        }

        WriteLog("Restore complete.");
    }

    private void ExtractFixDll(string destination)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("CuoHandshakeFix.dll", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded CuoHandshakeFix.dll resource is missing. Rebuild the patcher.");

        using Stream? stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Could not open embedded CuoHandshakeFix.dll.");
        using FileStream fs = File.Create(destination);
        stream.CopyTo(fs);
    }

    private void WriteLog(string message) => Log?.Invoke(message);
}
