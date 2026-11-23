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
    public TargetInfo? Target { get; private set; }

    public event Action<string>? Log;

    public void SetTarget(TargetInfo target)
    {
        if (!Directory.Exists(target.InstallDirectory))
            throw new DirectoryNotFoundException(target.InstallDirectory);
        if (!File.Exists(target.PatchAssemblyPath))
            throw new FileNotFoundException("Patch target assembly not found.", target.PatchAssemblyPath);
        Target = target;
    }

    public void SetInstallDirectory(string directory)
    {
        SetTarget(TargetLocator.ResolveFromDirectory(directory));
    }

    public string? GetInstalledVersion()
    {
        if (Target == null) return null;
        try
        {
            return FileVersionInfo.GetVersionInfo(Target.PatchAssemblyPath).FileVersion;
        }
        catch
        {
            return null;
        }
    }

    public PatchStatus GetStatus()
    {
        if (Target == null) return PatchStatus.Unknown;
        try
        {
            bool marker = CecilInjector.IsPatched(Target.PatchAssemblyPath);
            bool dll = File.Exists(Target.FixDllPath);
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
        if (Target == null) return null;
        return Directory.GetFiles(Target.InstallDirectory, Target.BackupSearchPattern)
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public ElevationMode GetElevationMode()
    {
        if (Target == null || !Target.SupportsAdminPatch) return ElevationMode.Unknown;
        try { return ManifestElevationService.GetMode(Target.PatchAssemblyPath); }
        catch { return ElevationMode.Unknown; }
    }

    public void ApplyPatch(bool removeAdminRequirement = false)
    {
        if (Target == null)
            throw new InvalidOperationException("No target selected.");

        string assemblyPath = Target.PatchAssemblyPath;
        string dllDest = Target.FixDllPath;

        if (FileLockChecker.IsLocked(assemblyPath, out string lockReason))
            throw new IOException(lockReason);

        if (ElevationHelper.NeedsElevationFor(Target.InstallDirectory))
        {
            WriteLog("Administrator rights required. Relaunching elevated…");
            ElevationHelper.RelaunchElevated();
            throw new OperationCanceledException("Relaunching elevated.");
        }

        ExtractFixDll(dllDest);
        WriteLog($"CuoHandshakeFix.dll installed/updated beside {Target.PatchFileName}.");

        bool alreadyPatched = CecilInjector.IsPatched(assemblyPath);
        if (!alreadyPatched)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string bak = assemblyPath + ".bak-" + stamp;
            File.Copy(assemblyPath, bak, overwrite: false);
            WriteLog($"Backup created: {Path.GetFileName(bak)}");

            try
            {
                CecilInjector.InjectBootstrap(assemblyPath, WriteLog);
            }
            catch
            {
                File.Copy(bak, assemblyPath, overwrite: true);
                WriteLog("IL injection failed — restored assembly from backup.");
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
            if (!Target.SupportsAdminPatch)
            {
                WriteLog("Admin-manifest patch skipped (not applicable to this target).");
            }
            else
            {
                if (FindLatestBackup() == null)
                {
                    string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    string bak = assemblyPath + ".bak-" + stamp;
                    File.Copy(assemblyPath, bak, overwrite: false);
                    WriteLog($"Backup created: {Path.GetFileName(bak)}");
                }

                ManifestElevationService.SetAsInvoker(assemblyPath, WriteLog);
            }
        }

        if (Target.Product == TargetProduct.NeoUoNet472)
            WriteLog("Note: Velopack updates may overwrite this patch — re-apply after NeoUO updates.");

        WriteLog("Patch update complete.");
    }

    public void RestoreBackup()
    {
        if (Target == null)
            throw new InvalidOperationException("No target selected.");

        string assemblyPath = Target.PatchAssemblyPath;
        string? bak = FindLatestBackup()
            ?? throw new FileNotFoundException($"No {Target.BackupSearchPattern} backup found in this folder.");

        if (FileLockChecker.IsLocked(assemblyPath, out string lockReason))
            throw new IOException(lockReason);

        if (ElevationHelper.NeedsElevationFor(Target.InstallDirectory))
        {
            WriteLog("Administrator rights required. Relaunching elevated…");
            ElevationHelper.RelaunchElevated();
            throw new OperationCanceledException("Relaunching elevated.");
        }

        File.Copy(bak, assemblyPath, overwrite: true);
        WriteLog($"Restored from {Path.GetFileName(bak)}");

        if (File.Exists(Target.FixDllPath))
        {
            File.Delete(Target.FixDllPath);
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
