using System.IO;

namespace CuoHandshakePatcher.Services;

public static class TargetLocator
{
    public static IReadOnlyList<TargetInfo> FindAll()
    {
        var list = new List<TargetInfo>();

        foreach (string dir in RazorCandidateDirectories())
        {
            string exe = Path.Combine(dir, "RazorEnhanced.exe");
            if (File.Exists(exe))
            {
                list.Add(new TargetInfo(
                    TargetProduct.RazorEnhanced,
                    $"Razor Enhanced ({dir})",
                    dir,
                    exe,
                    supportsAdminPatch: true));
            }
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        TryAddNeo(list, TargetProduct.NeoUoNet472, "NeoUO (.NET Framework 4.7.2)",
            Path.Combine(local, "neouo-net472", "current"), "NeoUO-net472.exe");

        return list;
    }

    public static TargetInfo? ResolveFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);

        string neo472 = Path.Combine(directory, "NeoUO-net472.exe");
        if (File.Exists(neo472))
        {
            return new TargetInfo(TargetProduct.NeoUoNet472, "NeoUO (.NET Framework 4.7.2)", directory, neo472, false);
        }

        // User may browse the Velopack root (neouo-net472).
        string current = Path.Combine(directory, "current");
        if (Directory.Exists(current))
            return ResolveFromDirectory(current);

        if (File.Exists(Path.Combine(directory, "NeoUO-net10.dll"))
            || File.Exists(Path.Combine(directory, "NeoUO-net10.exe"))
            || directory.Contains("neouo-net10", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "NeoUO (.NET 10) is not supported. The assistant does not load with ClassicUO, so the handshake patch cannot take effect. Use NeoUO (.NET Framework 4.7.2) or Razor Enhanced.");
        }

        string razor = Path.Combine(directory, "RazorEnhanced.exe");
        if (File.Exists(razor))
        {
            return new TargetInfo(TargetProduct.RazorEnhanced, "Razor Enhanced", directory, razor, true);
        }

        throw new FileNotFoundException(
            "No supported target found. Expected RazorEnhanced.exe or NeoUO-net472.exe.",
            directory);
    }

    private static void TryAddNeo(List<TargetInfo> list, TargetProduct product, string label, string currentDir, string fileName)
    {
        string path = Path.Combine(currentDir, fileName);
        if (File.Exists(path))
        {
            list.Add(new TargetInfo(product, label, currentDir, path, supportsAdminPatch: false));
        }
    }

    private static IEnumerable<string> RazorCandidateDirectories()
    {
        string[] roots =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Razor Enhanced"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Razor Enhanced"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RazorEnhanced"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RazorEnhanced"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Razor Enhanced"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RazorEnhanced"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Razor  Enhanced 1.0.0.15"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Razor Enhanced 1.0.0.15"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Patched Razor Enhanced"),
        ];

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
