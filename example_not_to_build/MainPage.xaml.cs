using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UnoApp4.Presentation;

public sealed partial class MainPage : Page
{
    private bool _isLeftPanelCollapsed;
    private const double DefaultLeftWidth = 360;
    private const double MinLeftWidth = 280;
    private bool _isAnimating;

    public MainPage()
    {
        this.InitializeComponent();

        // Event wiring is done in XAML. No extra hookup to avoid duplicate events.

        // Restore persisted UI state
        var settings = ApplicationData.Current.LocalSettings.Values;
        if (settings.TryGetValue("LeftPanelCollapsed", out var collapsedObj) && collapsedObj is bool collapsed)
        {
            _isLeftPanelCollapsed = collapsed;
        }

        // Restore width after reading collapsed flag
        if (settings.TryGetValue("LeftPanelWidth", out var widthObj) && widthObj is double w && w >= 0)
        {
            LeftPanel.Width = w;
        }
        else if (_isLeftPanelCollapsed)
        {
            LeftPanel.Width = 0;
        }
        else
        {
            LeftPanel.Width = DefaultLeftWidth;
        }
    }

    // Helper to animate width reliably on Skia and Windows without Storyboards
    private void AnimateLeftPanelTo(double targetWidth, TimeSpan? duration = null)
    {
        var dur = duration ?? TimeSpan.FromMilliseconds(250);
        var start = LeftPanel.Width <= 0 ? 0 : LeftPanel.Width;
        var delta = targetWidth - start;
        if (Math.Abs(delta) < 0.5)
        {
            LeftPanel.Width = targetWidth;
            var settingsInstant = ApplicationData.Current.LocalSettings.Values;
            settingsInstant["LeftPanelWidth"] = targetWidth;
            settingsInstant["LeftPanelCollapsed"] = targetWidth <= 0.1;
            _isAnimating = false;
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 FPS
        var sw = System.Diagnostics.Stopwatch.StartNew();
        timer.Tick += (s, e) =>
        {
            var t = sw.Elapsed.TotalMilliseconds / dur.TotalMilliseconds;
            if (t >= 1)
            {
                LeftPanel.Width = targetWidth;
                timer.Stop();
                sw.Stop();
                var settings = ApplicationData.Current.LocalSettings.Values;
                settings["LeftPanelWidth"] = targetWidth;
                settings["LeftPanelCollapsed"] = targetWidth <= 0.1;
                _isAnimating = false;
                return;
            }
            // Quadratic ease in/out
            double ease;
            if (t < 0.5)
            {
                ease = 2 * t * t;
            }
            else
            {
                ease = -1 + (4 - 2 * t) * t;
            }
            var current = start + (delta * ease);
            if (current < 0) current = 0;
            LeftPanel.Width = current;
        };
        timer.Start();
    }
    

    private void ToggleLeftPanelButton_Click(object sender, RoutedEventArgs e)
    {
        // Animated width change for a smooth experience on Skia/Windows
        var currentlyCollapsed = LeftPanel.Width <= 0.1;
        if (currentlyCollapsed)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            double target = DefaultLeftWidth;
            if (settings.TryGetValue("LeftPanelWidth", out var widthObj) && widthObj is double w && w >= MinLeftWidth)
            {
                target = w;
            }
            _isLeftPanelCollapsed = false;
            AnimateLeftPanelTo(target);
        }
        else
        {
            _isLeftPanelCollapsed = true;
            AnimateLeftPanelTo(0);
        }

        // Persist the intended state immediately; final width is persisted at animation end
        var settings2 = ApplicationData.Current.LocalSettings.Values;
        settings2["LeftPanelCollapsed"] = _isLeftPanelCollapsed;
    }

    private void LeftRightThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        // Adjust the left panel width by horizontal drag delta
        var current = LeftPanel.Width <= 0 ? 0 : LeftPanel.Width;
        var newWidth = current + e.HorizontalChange;
        // Apply basic constraints similar to MinWidth/MaxWidth
        if (newWidth < 0) newWidth = 0;
        if (newWidth < MinLeftWidth && newWidth > 0) newWidth = MinLeftWidth;
        LeftPanel.Width = newWidth;
        _isLeftPanelCollapsed = newWidth <= 0.1;

        // Persist state
        var settings = ApplicationData.Current.LocalSettings.Values;
        settings["LeftPanelCollapsed"] = _isLeftPanelCollapsed;
        settings["LeftPanelWidth"] = newWidth;
    }

    // =========================
    // Upload dialog interactions
    // =========================
    private async void BtnUpload_Click(object sender, RoutedEventArgs e)
    {
        // Ensure dialog is attached to visual tree
        UploadDialog.XamlRoot = this.XamlRoot;
        PreviewText.Text = "pas de donnée";
        PreviewToggle.IsOn = false;
        BtnSummarize.IsEnabled = false;
        await UploadDialog.ShowAsync();
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items?.OfType<IStorageFile>().FirstOrDefault();
            if (file != null)
            {
                await LoadFilePreviewAsync(file);
            }
        }
    }

    private async void DropZone_Tapped(object sender, TappedRoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFilePreviewAsync(file);
            }
        }
        catch (Exception ex)
        {
            PreviewText.Text = $"File picker not available: {ex.Message}";
        }
    }

    private async Task LoadFilePreviewAsync(IStorageFile file)
    {
        try
        {
            string text = await FileIO.ReadTextAsync(file);
            if (string.IsNullOrWhiteSpace(text))
            {
                PreviewText.Text = $"(empty or binary file) {file.Name}";
            }
            else
            {
                PreviewText.Text = text;
            }
            BtnSummarize.IsEnabled = true;
        }
        catch
        {
            PreviewText.Text = $"Unable to read file: {file.Name}";
        }
    }

    private void BtnToText_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PreviewText.Text))
        {
            PreviewText.Text = "pas de donnée";
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        PreviewText.Text = "pas de donnée";
        PreviewToggle.IsOn = false;
        BtnSummarize.IsEnabled = false;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        UploadDialog.Hide();
    }
}
