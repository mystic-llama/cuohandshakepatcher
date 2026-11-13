using System.IO;

namespace CuoHandshakePatcher.Services;

public static class RazorLocator
{
    public static IEnumerable<string> CandidateDirectories()
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
        ];

        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(root, "RazorEnhanced.exe")))
                yield return root;
        }
    }

    public static string? FindInstallDirectory() => CandidateDirectories().FirstOrDefault();

    public static string ExePath(string installDir) => Path.Combine(installDir, "RazorEnhanced.exe");

    public static string FixDllPath(string installDir) => Path.Combine(installDir, "CuoHandshakeFix.dll");
}
