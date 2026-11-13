using System.IO;
using System.Text;

namespace CuoHandshakePatcher.Services;

public enum ElevationMode
{
    Unknown,
    RequireAdministrator,
    AsInvoker,
}

/// <summary>
/// Rewrites the embedded application manifest execution level inside RazorEnhanced.exe.
/// RE ships with requireAdministrator; Winlator and other non-UAC hosts need asInvoker.
/// Uses same-length ASCII replacement so the PE resource size stays valid.
/// </summary>
public static class ManifestElevationService
{
    // IMPORTANT: The embedded PE manifest uses a self-closing tag with a single space
    // after "requestedExecutionLevel". Commented examples use TWO spaces — do not match those.
    // mt.exe may rewrite the tag form when extracting; trust the on-disk ASCII bytes.
    private const string RequireAdministratorLive =
        "<requestedExecutionLevel level=\"requireAdministrator\" uiAccess=\"false\" />";

    // 11 trailing spaces inside the attribute keep this the same length as requireAdministrator.
    private const string AsInvokerLive =
        "<requestedExecutionLevel level=\"asInvoker           \" uiAccess=\"false\" />";

    public static ElevationMode GetMode(string exePath)
    {
        string ascii = Encoding.ASCII.GetString(File.ReadAllBytes(exePath));

        if (ascii.Contains(RequireAdministratorLive, StringComparison.Ordinal))
            return ElevationMode.RequireAdministrator;

        if (ascii.Contains(AsInvokerLive, StringComparison.Ordinal))
            return ElevationMode.AsInvoker;

        return ElevationMode.Unknown;
    }

    public static string Describe(ElevationMode mode) => mode switch
    {
        ElevationMode.RequireAdministrator => "Admin (UAC)",
        ElevationMode.AsInvoker => "No admin",
        _ => "UAC unknown",
    };

    public static bool SetAsInvoker(string exePath, Action<string>? log = null)
    {
        ElevationMode current = GetMode(exePath);
        if (current == ElevationMode.AsInvoker)
        {
            log?.Invoke("Manifest already set to asInvoker (no admin).");
            return false;
        }

        if (current != ElevationMode.RequireAdministrator)
            throw new InvalidOperationException(
                "Could not find the live requireAdministrator manifest entry. This RE build may use a different manifest layout.");

        ReplaceAscii(exePath, RequireAdministratorLive, AsInvokerLive);

        if (GetMode(exePath) != ElevationMode.AsInvoker)
            throw new InvalidOperationException("Manifest write did not stick — elevation is still not asInvoker.");

        log?.Invoke("Manifest updated: requireAdministrator → asInvoker (no UAC prompt).");
        return true;
    }

    public static bool SetRequireAdministrator(string exePath, Action<string>? log = null)
    {
        ElevationMode current = GetMode(exePath);
        if (current == ElevationMode.RequireAdministrator)
        {
            log?.Invoke("Manifest already requires administrator.");
            return false;
        }

        if (current != ElevationMode.AsInvoker)
            throw new InvalidOperationException(
                "This exe cannot be reverted in-place to requireAdministrator. Restore a backup instead.");

        ReplaceAscii(exePath, AsInvokerLive, RequireAdministratorLive);
        log?.Invoke("Manifest updated: asInvoker → requireAdministrator.");
        return true;
    }

    private static void ReplaceAscii(string exePath, string oldText, string newText)
    {
        if (oldText.Length != newText.Length)
            throw new InvalidOperationException("Manifest patch strings must be the same length.");

        byte[] bytes = File.ReadAllBytes(exePath);
        byte[] oldBytes = Encoding.ASCII.GetBytes(oldText);
        byte[] newBytes = Encoding.ASCII.GetBytes(newText);

        int index = IndexOf(bytes, oldBytes);
        if (index < 0)
            throw new InvalidOperationException($"Pattern not found in exe: {oldText}");

        Buffer.BlockCopy(newBytes, 0, bytes, index, newBytes.Length);

        string temp = exePath + ".uac-tmp";
        File.WriteAllBytes(temp, bytes);
        File.Copy(temp, exePath, overwrite: true);
        File.Delete(temp);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }
}
