# CUO Handshake Fix Patcher

CUO Handshake Fix Patcher is a portable Windows utility for Razor Enhanced and NeoUO (.NET Framework 4.7.2) users who get disconnected after about four minutes when playing through ClassicUO. It updates handshake handling for the `0xD5` server challenge and backs up the target assembly before patching. NeoUO (.NET 10) is not supported because its assistant does not load with ClassicUO.

![CUO Handshake Fix Patcher](docs/patcher-screenshot.png)

## Download

Open **Releases** on the GitHub repository and download `CuoHandshakePatcher-v1.1.0-win-x64.zip`. The portable build includes the .NET Desktop runtime. Compatibility covers **ClassicUO** with Razor Enhanced or NeoUO (.NET Framework 4.7.2) in plugin mode.

## Apply the patch

1. Close Razor Enhanced / NeoUO and ClassicUO.
2. Extract the ZIP and run `CuoHandshakePatcher.exe`. Keep the `Assets` folder next to the executable for the banner artwork.
3. Confirm the detected target or select it with **Browse**.
4. Click **Apply Patch**, read the disclaimer, and accept to continue.
5. Open the patched client from the same folder and launch ClassicUO.

To undo the patch, close both apps, reopen the patcher, and select **Restore Backup**.

## Run Razor Enhanced without an administrator prompt (Winlator)

Winlator can have trouble launching Razor Enhanced when the executable requests administrator elevation. To remove that launch requirement, close Razor Enhanced and the game client, then open the patcher and select the Razor Enhanced folder. Check **Remove admin requirement (Winlator / asInvoker)** and click **Apply Patch**. You can do this even if the handshake fix is already installed. This option does not apply to NeoUO.

The checkbox changes the Windows manifest inside `RazorEnhanced.exe` from `requireAdministrator` to `asInvoker`, so Razor Enhanced starts with the permissions of whatever launched it. The patcher displays **No admin** when it detects the updated manifest. The patcher itself still needs permission to write to the install folder and may request elevation on Windows for a protected folder.

**Restore Backup** restores the latest saved assembly, including its original elevation setting when applicable, and removes the handshake fix DLL.

[Build from source](BUILD.md) · [Third-party notices](THIRD-PARTY-NOTICES.md)
