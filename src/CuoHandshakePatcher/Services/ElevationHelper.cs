using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace CuoHandshakePatcher.Services;

public static class ElevationHelper
{
    public static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool NeedsElevationFor(string directory)
    {
        string testFile = Path.Combine(directory, $".cuo-handshake-write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);
            return false;
        }
        catch
        {
            return !IsElevated();
        }
    }

    public static void RelaunchElevated()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            throw new InvalidOperationException("Could not determine process path for elevation.");

        ProcessStartInfo psi = new()
        {
            FileName = exe,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };
        Process.Start(psi);
    }
}
