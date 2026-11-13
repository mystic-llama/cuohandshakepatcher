using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuoHandshakePatcher.Services;
using Microsoft.Win32;

namespace CuoHandshakePatcher;

public partial class MainWindow : Window
{
    private readonly PatchService _patcher = new();

    public MainWindow()
    {
        InitializeComponent();
        _patcher.Log += AppendLog;
        TryLoadBanner();

        string? detected = RazorLocator.FindInstallDirectory();
        if (detected != null)
        {
            try
            {
                _patcher.SetInstallDirectory(detected);
                RefreshUi();
                AppendLog($"Detected Razor Enhanced at: {detected}");
            }
            catch (Exception ex)
            {
                AppendLog(ex.Message);
            }
        }
        else
        {
            AppendLog("No Razor Enhanced install auto-detected. Browse to your folder.");
        }
    }

    private void TryLoadBanner()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "banner.png");
            if (!File.Exists(path))
                return;
            BannerImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            // gradient banner is enough
        }
    }

    private void Caption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Razor Enhanced install folder",
        };

        if (_patcher.InstallDirectory != null)
            dialog.InitialDirectory = _patcher.InstallDirectory;

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _patcher.SetInstallDirectory(dialog.FolderName);
            RefreshUi();
            AppendLog($"Selected: {dialog.FolderName}");
        }
        catch (Exception ex)
        {
            AppendLog("Browse failed: " + ex.Message);
            MessageBox.Show(ex.Message, "CUO Handshake Fix", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        bool removeAdmin = RemoveAdminCheck.IsChecked == true;
        try
        {
            await Task.Run(() => _patcher.ApplyPatch(removeAdmin));
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "Patch failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshUi();
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            await Task.Run(() => _patcher.RestoreBackup());
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshUi();
        }
    }

    private void RefreshUi()
    {
        PathText.Text = _patcher.InstallDirectory ?? "No folder selected";
        VersionText.Text = _patcher.GetInstalledVersion() is string v ? $"Version {v}" : string.Empty;

        PatchStatus status = _patcher.GetStatus();
        switch (status)
        {
            case PatchStatus.Patched:
                StatusText.Text = "Patched";
                StatusText.Foreground = (Brush)FindResource("SuccessBrush");
                StatusChip.BorderBrush = (Brush)FindResource("SuccessBrush");
                break;
            case PatchStatus.Unpatched:
                StatusText.Text = "Unpatched";
                StatusText.Foreground = (Brush)FindResource("WarningBrush");
                StatusChip.BorderBrush = (Brush)FindResource("WarningBrush");
                break;
            default:
                StatusText.Text = "Unknown";
                StatusText.Foreground = (Brush)FindResource("TextMutedBrush");
                StatusChip.BorderBrush = (Brush)FindResource("AppBorderBrush");
                break;
        }

        ElevationMode elevation = _patcher.GetElevationMode();
        ElevationText.Text = ManifestElevationService.Describe(elevation);
        ElevationChip.BorderBrush = elevation switch
        {
            ElevationMode.AsInvoker => (Brush)FindResource("SuccessBrush"),
            ElevationMode.RequireAdministrator => (Brush)FindResource("WarningBrush"),
            _ => (Brush)FindResource("AppBorderBrush"),
        };
        ElevationText.Foreground = ElevationChip.BorderBrush;
        if (elevation == ElevationMode.AsInvoker)
            RemoveAdminCheck.IsChecked = true;

        RestoreButton.IsEnabled = _patcher.FindLatestBackup() != null;
        // Keep Apply enabled when already patched so the runtime DLL / UAC option can refresh.
        ApplyButton.IsEnabled = _patcher.InstallDirectory != null;
        ApplyButton.Content = status == PatchStatus.Patched ? "Update Fix DLL" : "Apply Patch";
    }

    private void SetBusy(bool busy)
    {
        ApplyButton.IsEnabled = !busy && _patcher.InstallDirectory != null;
        RestoreButton.IsEnabled = !busy && _patcher.FindLatestBackup() != null;
        RemoveAdminCheck.IsEnabled = !busy;
    }

    private void AppendLog(string message)
    {
        void Write()
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (LogBox.Text.Length > 0)
                LogBox.AppendText(Environment.NewLine);
            LogBox.AppendText(line);
            LogScroll.ScrollToEnd();
        }

        if (!Dispatcher.CheckAccess())
            Dispatcher.Invoke(Write);
        else
            Write();
    }
}
