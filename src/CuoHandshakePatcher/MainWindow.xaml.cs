using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuoHandshakePatcher.Services;
using Microsoft.Win32;

namespace CuoHandshakePatcher;

public partial class MainWindow : Window
{
    private readonly PatchService _patcher = new();
    private bool _suppressTargetChange;

    public MainWindow()
    {
        InitializeComponent();
        _patcher.Log += AppendLog;
        TryLoadBanner();
        LoadTargets();
    }

    private void LoadTargets()
    {
        _suppressTargetChange = true;
        try
        {
            var targets = TargetLocator.FindAll().ToList();
            TargetCombo.ItemsSource = targets;

            if (targets.Count == 0)
            {
                AppendLog("No Razor Enhanced or NeoUO installs auto-detected. Browse to a folder.");
                return;
            }

            // Prefer NeoUO if present, otherwise first hit.
            TargetInfo prefer = targets.FirstOrDefault(t => t.Product == TargetProduct.NeoUoNet472)
                ?? targets[0];
            TargetCombo.SelectedItem = prefer;
            SelectTarget(prefer);
            AppendLog($"Detected {targets.Count} target(s). Selected: {prefer.DisplayName}");
        }
        finally
        {
            _suppressTargetChange = false;
        }
    }

    private void SelectTarget(TargetInfo target)
    {
        _patcher.SetTarget(target);
        RefreshUi();
    }

    private void TargetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTargetChange) return;
        if (TargetCombo.SelectedItem is TargetInfo target)
        {
            try
            {
                SelectTarget(target);
                AppendLog($"Selected: {target.DisplayName}");
            }
            catch (Exception ex)
            {
                AppendLog(ex.Message);
            }
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
            Title = "Select Razor Enhanced or NeoUO (.NET Framework 4.7.2) folder",
        };

        if (_patcher.Target != null)
            dialog.InitialDirectory = _patcher.Target.InstallDirectory;

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            TargetInfo target = TargetLocator.ResolveFromDirectory(dialog.FolderName);
            _suppressTargetChange = true;
            var items = (TargetCombo.ItemsSource as IEnumerable<TargetInfo>)?.ToList() ?? [];
            if (!items.Any(t => string.Equals(t.PatchAssemblyPath, target.PatchAssemblyPath, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(target);
                TargetCombo.ItemsSource = items;
            }

            TargetCombo.SelectedItem = items.First(t =>
                string.Equals(t.PatchAssemblyPath, target.PatchAssemblyPath, StringComparison.OrdinalIgnoreCase));
            SelectTarget(target);
            AppendLog($"Selected: {target.DisplayName} ({target.PatchFileName})");
        }
        catch (Exception ex)
        {
            AppendLog("Browse failed: " + ex.Message);
            MessageBox.Show(ex.Message, "CUO Handshake Fix", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _suppressTargetChange = false;
        }
    }

    private bool ConfirmDisclaimer()
    {
        var dialog = new DisclaimerWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        return dialog.ShowDialog() == true && dialog.Accepted;
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_patcher.Target == null)
        {
            MessageBox.Show(
                "Select a Razor Enhanced or NeoUO (.NET Framework 4.7.2) folder first.",
                "No target selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!ConfirmDisclaimer())
        {
            AppendLog("Patch cancelled — disclaimer declined.");
            return;
        }

        SetBusy(true);
        bool removeAdmin = RemoveAdminCheck.IsChecked == true && _patcher.Target?.SupportsAdminPatch == true;
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
        TargetInfo? target = _patcher.Target;
        PathText.Text = target == null
            ? "No target selected"
            : $"{target.PatchFileName}  —  {target.InstallDirectory}";

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

        bool adminUi = target?.SupportsAdminPatch == true;
        RemoveAdminCheck.Visibility = adminUi ? Visibility.Visible : Visibility.Collapsed;
        AdminHelpText.Visibility = adminUi ? Visibility.Visible : Visibility.Collapsed;
        ElevationChip.Visibility = adminUi ? Visibility.Visible : Visibility.Collapsed;

        if (adminUi)
        {
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
        }
        else
        {
            RemoveAdminCheck.IsChecked = false;
            ElevationText.Text = "No UAC";
        }

        TargetCombo.IsEnabled = true;
        RemoveAdminCheck.IsEnabled = adminUi;
        RestoreButton.IsEnabled = _patcher.FindLatestBackup() != null;
        ApplyButton.IsEnabled = target != null;
        ApplyButton.Content = status == PatchStatus.Patched ? "Update Fix DLL" : "Apply Patch";
    }

    private void SetBusy(bool busy)
    {
        ApplyButton.IsEnabled = !busy && _patcher.Target != null;
        RestoreButton.IsEnabled = !busy && _patcher.FindLatestBackup() != null;
        RemoveAdminCheck.IsEnabled = !busy && _patcher.Target?.SupportsAdminPatch == true;
        TargetCombo.IsEnabled = !busy;
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
