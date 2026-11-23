using System.IO;

namespace CuoHandshakePatcher.Services;

public enum TargetProduct
{
    RazorEnhanced,
    NeoUoNet472,
}

public sealed class TargetInfo
{
    public TargetInfo(
        TargetProduct product,
        string displayName,
        string installDirectory,
        string patchAssemblyPath,
        bool supportsAdminPatch)
    {
        Product = product;
        DisplayName = displayName;
        InstallDirectory = installDirectory;
        PatchAssemblyPath = patchAssemblyPath;
        SupportsAdminPatch = supportsAdminPatch;
    }

    public TargetProduct Product { get; }
    public string DisplayName { get; }
    public string InstallDirectory { get; }
    public string PatchAssemblyPath { get; }
    public bool SupportsAdminPatch { get; }

    public string PatchFileName => Path.GetFileName(PatchAssemblyPath);
    public string FixDllPath => Path.Combine(InstallDirectory, "CuoHandshakeFix.dll");
    public string BackupSearchPattern => PatchFileName + ".bak-*";

    public override string ToString() => DisplayName;
}
