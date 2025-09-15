using Microsoft.UI.Xaml.Input;
using Windows.Storage;
using Windows.System;

namespace CAI_design_1_chat.Presentation;

public sealed partial class MainPage : Page
{
    private DispatcherTimer? _animationTimer;
    private double _animationStartWidth;
    private double _animationTargetWidth;
    private DateTime _animationStartTime;
    private TimeSpan _animationDuration = TimeSpan.FromMilliseconds(250);
    private bool _isAnimating = false;

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPage_Loaded;
        InitializeAnimationTimer();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreLeftPanelState();
    }

    private void InitializeAnimationTimer()
    {
        _animationTimer = new DispatcherTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    private void RestoreLeftPanelState()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        
        if (localSettings.Values.TryGetValue("LeftPanelCollapsed", out var collapsedObj) && 
            collapsedObj is bool collapsed && collapsed)
        {
            LeftPanelColumn.Width = new GridLength(0);
            LeftPanel.Visibility = Visibility.Collapsed;
        }
        else if (localSettings.Values.TryGetValue("LeftPanelWidth", out var widthObj) && 
                 widthObj is double savedWidth && savedWidth > 0)
        {
            LeftPanelColumn.Width = new GridLength(savedWidth);
            LeftPanel.Visibility = Visibility.Visible;
        }
        else
        {
            LeftPanel.Visibility = Visibility.Visible;
        }
    }

    private void SaveLeftPanelState()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        var currentWidth = LeftPanelColumn.Width.Value;
        
        localSettings.Values["LeftPanelCollapsed"] = currentWidth <= 0.5;
        if (currentWidth > 0.5)
        {
            localSettings.Values["LeftPanelWidth"] = currentWidth;
        }
    }

    private void ToggleLeftPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;

        var currentWidth = LeftPanelColumn.Width.Value;
        var targetWidth = currentWidth <= 0.5 ? GetLastSavedWidth() : 0.0;
        
        AnimateLeftPanelTo(targetWidth);
    }

    private double GetLastSavedWidth()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings.Values.TryGetValue("LeftPanelWidth", out var widthObj) && 
            widthObj is double savedWidth && savedWidth >= 280)
        {
            return savedWidth;
        }
        return 360; // Default width
    }

    private void AnimateLeftPanelTo(double targetWidth)
    {
        if (_isAnimating) return;

        _animationStartWidth = LeftPanelColumn.Width.Value;
        _animationTargetWidth = targetWidth;
        
        var delta = Math.Abs(_animationTargetWidth - _animationStartWidth);
        if (delta < 0.5)
        {
            LeftPanelColumn.Width = new GridLength(_animationTargetWidth);
            SplitterColumn.Width = new GridLength(_animationTargetWidth <= 0.5 ? 0 : 4);
            // Hide the left panel and splitter when collapsed
            LeftPanel.Visibility = _animationTargetWidth <= 0.5 ? Visibility.Collapsed : Visibility.Visible;
            Splitter.Visibility = _animationTargetWidth <= 0.5 ? Visibility.Collapsed : Visibility.Visible;
            SaveLeftPanelState();
            return;
        }

        _animationStartTime = DateTime.Now;
        _isAnimating = true;
        _animationTimer?.Start();
    }

    private void AnimationTimer_Tick(object? sender, object e)
    {
        var elapsed = DateTime.Now - _animationStartTime;
        var progress = Math.Min(1.0, elapsed.TotalMilliseconds / _animationDuration.TotalMilliseconds);

        if (progress >= 1.0)
        {
            LeftPanelColumn.Width = new GridLength(_animationTargetWidth);
            SplitterColumn.Width = new GridLength(_animationTargetWidth <= 0.5 ? 0 : 4);
            // Hide the left panel and splitter when collapsed
            LeftPanel.Visibility = _animationTargetWidth <= 0.5 ? Visibility.Collapsed : Visibility.Visible;
            Splitter.Visibility = _animationTargetWidth <= 0.5 ? Visibility.Collapsed : Visibility.Visible;
            _animationTimer?.Stop();
            _isAnimating = false;
            SaveLeftPanelState();
            return;
        }

        // Quadratic ease-in/out
        var ease = progress < 0.5 ? 2 * progress * progress : -1 + (4 - 2 * progress) * progress;
        var currentWidth = _animationStartWidth + (_animationTargetWidth - _animationStartWidth) * ease;
        
        LeftPanelColumn.Width = new GridLength(Math.Max(0, currentWidth));
        
        // Hide elements when getting close to 0
        if (currentWidth <= 10)
        {
            LeftPanel.Visibility = Visibility.Collapsed;
            Splitter.Visibility = Visibility.Collapsed;
            SplitterColumn.Width = new GridLength(0);
        }
        else
        {
            LeftPanel.Visibility = Visibility.Visible;
            Splitter.Visibility = Visibility.Visible;
            SplitterColumn.Width = new GridLength(4);
        }
    }

    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder for file upload functionality
        // This will be implemented later with the upload dialog
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void ChatInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !string.IsNullOrWhiteSpace(ChatInput.Text))
        {
            SendMessage();
            e.Handled = true;
        }
    }

    private void SendMessage()
    {
        var message = ChatInput.Text?.Trim();
        if (string.IsNullOrEmpty(message)) return;

        // Clear the input
        ChatInput.Text = string.Empty;
        
        // Placeholder for actual message sending logic
        // This will be implemented later with proper chat functionality
    }
}