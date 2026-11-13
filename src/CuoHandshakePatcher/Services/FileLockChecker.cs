using System.Diagnostics;
using System.IO;

namespace CuoHandshakePatcher.Services;

public static class FileLockChecker
{
    public static bool IsLocked(string path, out string reason)
    {
        reason = string.Empty;
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            string[] suspects = ["RazorEnhanced", "ClassicUO", "TazUO", "LegacyUO"];
            var running = Process.GetProcesses()
                .Where(p =>
                {
                    try { return suspects.Any(s => p.ProcessName.Contains(s, StringComparison.OrdinalIgnoreCase)); }
                    catch { return false; }
                })
                .Select(p => p.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            reason = running.Length > 0
                ? $"File is locked. Close: {string.Join(", ", running)}"
                : "RazorEnhanced.exe is locked. Close Razor Enhanced / CUO and try again.";
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "Access denied. The patcher may need to run as Administrator.";
            return true;
        }
    }
}
