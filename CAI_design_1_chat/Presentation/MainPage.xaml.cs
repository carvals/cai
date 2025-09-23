using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using CAI_design_1_chat.Services;
using Microsoft.Data.Sqlite;
using Windows.UI;
using Windows.UI.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace CAI_design_1_chat.Presentation;

public sealed partial class MainPage : Page
{
    private DispatcherTimer? _animationTimer;
    private double _animationStartWidth;
    private double _animationTargetWidth;
    private DateTime _animationStartTime;
    private TimeSpan _animationDuration = TimeSpan.FromMilliseconds(250);
    
    // Services
    private readonly OpenAIService _openAIService;
    private readonly OllamaService _ollamaService;
    private readonly DatabaseService _databaseService;
    private readonly ChatContextService _chatContextService;
    private readonly ContextCacheService _contextCacheService;
    private readonly InstructionShortcutService _instructionShortcutService;
    private ContextParserService _contextParserService;
    private int _currentSessionId;
    
    // Context token count debouncing
    private DispatcherTimer? _contextUpdateTimer;
    private const int CONTEXT_UPDATE_DEBOUNCE_MS = 300;
    
    // Auto-scroll state tracking
    private bool _isUserScrolling = false;
    private bool _shouldAutoScroll = true;
    private const double SCROLL_THRESHOLD = 50.0; // pixels from bottom to trigger auto-scroll
    
    // Tab navigation state
    private bool _isChatTabActive = true;
    
    // Panel state tracking
    private enum PanelType { Workspace, Context, InstructionShortcuts }
    private PanelType? _currentPanelType = PanelType.Workspace;
    private bool _isAnimating = false;

    public MainPage()
    {
        this.InitializeComponent();
        
        // Initialize AI services
        _openAIService = new OpenAIService();
        _ollamaService = new OllamaService();
        
        // Initialize database service
        _databaseService = new DatabaseService();
        _chatContextService = new ChatContextService(_databaseService);
        _contextCacheService = new ContextCacheService(_databaseService, _chatContextService);
        _instructionShortcutService = new InstructionShortcutService(_databaseService);
        
        // Initialize context cache service with shared service instances
        _contextCacheService = new ContextCacheService(_databaseService, _chatContextService);
        _databaseService.SetContextCacheService(_contextCacheService);
        
        // Initialize context parser service
        _contextParserService = new ContextParserService(_contextCacheService);
        
        // Subscribe to context change events
        _contextCacheService.ContextChanged += OnContextChanged;
        
        // Configure ContextPanel with shared service instances
        ContextPanelControl.SetServices(_databaseService, _chatContextService);
        
        // Update context size from settings
        UpdateContextServiceFromSettings();
        
        this.Loaded += MainPage_Loaded;
        InitializeAnimationTimer();
        InitializeScrollHandlers();
        
        // Initialize database on page load
        _ = InitializeDatabaseAsync();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAIModelIndicator();
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
        HandleSidebarButtonClick(PanelType.Workspace);
    }

    private void ContextButton_Click(object sender, RoutedEventArgs e)
    {
        HandleSidebarButtonClick(PanelType.Context);
    }

    private async void InstructionShortcutsButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowInstructionShortcutsOverlayAsync();
    }

    private void HandleSidebarButtonClick(PanelType requestedPanelType)
    {
        if (_isAnimating) return;

        var currentWidth = LeftPanelColumn.Width.Value;
        var isPanelCollapsed = currentWidth <= 0.5;
        var isSamePanelType = _currentPanelType == requestedPanelType;

        if (isPanelCollapsed)
        {
            // Case 1: Panel Collapsed + Button Click → Expand & Show Content
            AnimateLeftPanelTo(GetLastSavedWidth());
            ShowPanel(requestedPanelType);
        }
        else if (!isSamePanelType)
        {
            // Case 2: Panel Expanded + Different Content → Switch Content
            ShowPanel(requestedPanelType);
        }
        else
        {
            // Case 3: Panel Expanded + Same Content → Collapse Panel
            AnimateLeftPanelTo(0.0);
            _currentPanelType = null; // Panel is collapsed, no active content
        }
    }

    private async void ShowPanel(PanelType panelType)
    {
        _currentPanelType = panelType;
        
        switch (panelType)
        {
            case PanelType.Workspace:
                // Update button visual states
                UpdatePanelButtonStates(isWorkspaceActive: true);
                
                // Show workspace panel, hide context panel
                WorkspacePanel.Visibility = Visibility.Visible;
                ContextPanelControl.Visibility = Visibility.Collapsed;
                
                Console.WriteLine("Workspace panel activated");
                break;
                
            case PanelType.Context:
                // Update button visual states
                UpdatePanelButtonStates(isWorkspaceActive: false);
                
                // Show context panel, hide workspace panel
                WorkspacePanel.Visibility = Visibility.Collapsed;
                ContextPanelControl.Visibility = Visibility.Visible;
                
                // Load context files for current session
                await ContextPanelControl.LoadContextFilesAsync(_currentSessionId);
                
                Console.WriteLine("Context panel activated");
                break;
                
        }
    }


    // Instruction Shortcuts Overlay Methods
    private async Task ShowInstructionShortcutsOverlayAsync()
    {
        try
        {
            InstructionShortcutsOverlay.Visibility = Visibility.Visible;
            await LoadInstructionShortcutsOverlayAsync();
            ShowOverlayEmptyState();
            Console.WriteLine("Instruction shortcuts overlay opened");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error showing instruction shortcuts overlay: {ex.Message}");
        }
    }

    private async Task LoadInstructionShortcutsOverlayAsync()
    {
        try
        {
            var shortcuts = await _instructionShortcutService.GetAllShortcutsAsync(false);
            var categories = await _instructionShortcutService.GetDistinctPromptTypesAsync();
            
            // Load categories
            OverlayCategoryFilterComboBox.Items.Clear();
            OverlayCategoryFilterComboBox.Items.Add("All Categories");
            foreach (var category in categories)
            {
                OverlayCategoryFilterComboBox.Items.Add(category);
            }
            OverlayCategoryFilterComboBox.SelectedIndex = 0;
            
            // Load shortcuts
            RefreshOverlayShortcutsDisplay(shortcuts);
            
            Console.WriteLine($"Loaded {shortcuts.Count} shortcuts in overlay");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading shortcuts overlay: {ex.Message}");
        }
    }

    private void RefreshOverlayShortcutsDisplay(List<InstructionShortcut> shortcuts)
    {
        OverlayShortcutsContainer.Children.Clear();

        foreach (var shortcut in shortcuts)
        {
            var shortcutRow = CreateOverlayShortcutRow(shortcut);
            OverlayShortcutsContainer.Children.Add(shortcutRow);
        }
    }

    private Border CreateOverlayShortcutRow(InstructionShortcut shortcut)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 8),
            Tag = shortcut
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Name column
        var nameText = new TextBlock
        {
            Text = shortcut.Title,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            Foreground = shortcut.IsActive 
                ? (Brush)Application.Current.Resources["MaterialOnSurfaceBrush"]
                : new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameText, 0);

        // Shortcut column
        var shortcutText = new TextBlock
        {
            Text = shortcut.Shortcut ?? "",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = shortcut.IsActive 
                ? (Brush)Application.Current.Resources["MaterialOnSurfaceVariantBrush"]
                : new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            FontFamily = new FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(shortcutText, 1);

        grid.Children.Add(nameText);
        grid.Children.Add(shortcutText);
        border.Child = grid;

        // Add hover effects
        border.PointerEntered += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(25, 138, 43, 226));
            ToolTipService.SetToolTip(border, shortcut.Description);
        };

        border.PointerExited += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        };

        // Add click handler
        border.Tapped += (s, e) => EditOverlayShortcut(shortcut);
        border.DoubleTapped += (s, e) => EditOverlayShortcut(shortcut);

        return border;
    }

    private void EditOverlayShortcut(InstructionShortcut shortcut)
    {
        PopulateOverlayEditForm(shortcut);
        ShowOverlayEditForm();
    }

    private void PopulateOverlayEditForm(InstructionShortcut shortcut)
    {
        OverlayFormHeaderText.Text = "Edit Instruction";
        OverlayNameTextBox.Text = shortcut.Title;
        OverlayShortcutTextBox.Text = shortcut.Shortcut ?? "";
        OverlayLanguageTextBox.Text = shortcut.Language;
        OverlayPromptTypeTextBox.Text = shortcut.PromptType;
        OverlayDescriptionTextBox.Text = shortcut.Description;
        OverlayInstructionTextBox.Text = shortcut.Instruction;
        OverlayIsActiveCheckBox.IsChecked = shortcut.IsActive;
    }

    private void ShowOverlayEditForm()
    {
        OverlayEmptyStatePanel.Visibility = Visibility.Collapsed;
        OverlayEditFormPanel.Visibility = Visibility.Visible;
    }

    private void ShowOverlayEmptyState()
    {
        OverlayEditFormPanel.Visibility = Visibility.Collapsed;
        OverlayEmptyStatePanel.Visibility = Visibility.Visible;
    }

    private void ClearOverlayEditForm()
    {
        OverlayFormHeaderText.Text = "New Instruction";
        OverlayNameTextBox.Text = "";
        OverlayShortcutTextBox.Text = "";
        OverlayLanguageTextBox.Text = "";
        OverlayPromptTypeTextBox.Text = "";
        OverlayDescriptionTextBox.Text = "";
        OverlayInstructionTextBox.Text = "";
        OverlayIsActiveCheckBox.IsChecked = true;
        ResetOverlayShortcutValidation();
    }

    private void ResetOverlayShortcutValidation()
    {
        OverlayShortcutTextBox.BorderBrush = (Brush)Application.Current.Resources["MaterialOutlineVariantBrush"];
        OverlayShortcutValidationText.Visibility = Visibility.Collapsed;
    }

    // Overlay Event Handlers
    private void BackFromInstructionShortcutsButton_Click(object sender, RoutedEventArgs e)
    {
        InstructionShortcutsOverlay.Visibility = Visibility.Collapsed;
        Console.WriteLine("Instruction shortcuts overlay closed");
    }

    private void OverlayAddNewShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        ClearOverlayEditForm();
        ShowOverlayEditForm();
    }

    private async void OverlayCategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // TODO: Implement category filtering
    }

    private async void OverlayViewDeletedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // TODO: Implement view deleted functionality
    }

    private void OverlayShortcutTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // TODO: Implement validation
    }

    private async void InstructionSaveButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement save functionality
    }

    private void InstructionCancelButton_Click(object sender, RoutedEventArgs e)
    {
        ShowOverlayEmptyState();
    }

    private void UpdatePanelButtonStates(bool isWorkspaceActive)
    {
        if (isWorkspaceActive)
        {
            // Workspace button active
            ToggleLeftPanelButton.Background = (Brush)Application.Current.Resources["MaterialPrimaryBrush"];
            ToggleLeftPanelButton.BorderBrush = (Brush)Application.Current.Resources["MaterialPrimaryBrush"];
            
            // Context button inactive
            ContextButton.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            ContextButton.BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
        }
        else
        {
            // Context button active
            ContextButton.Background = (Brush)Application.Current.Resources["MaterialPrimaryBrush"];
            ContextButton.BorderBrush = (Brush)Application.Current.Resources["MaterialPrimaryBrush"];
            
            // Workspace button inactive
            ToggleLeftPanelButton.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            ToggleLeftPanelButton.BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
        }
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
        // Show the file upload overlay instead of navigating to separate page
        ShowFileUploadOverlay();
    }


    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // Toggle settings menu visibility with animation
        if (SettingsMenu.Visibility == Visibility.Visible)
        {
            SettingsMenu.Visibility = Visibility.Collapsed;
        }
        else
        {
            SettingsMenu.Visibility = Visibility.Visible;
        }
    }

    private async void AISettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.AISettingsDialog();
        dialog.XamlRoot = this.XamlRoot;
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            dialog.SaveSettings();
            // Reload OpenAI service configuration after settings are saved
            _openAIService.ReloadConfiguration();
            // Update context service with new settings
            UpdateContextServiceFromSettings();
            // Update AI model indicator to reflect new settings
            UpdateAIModelIndicator();
            // Update context size display
            _ = UpdateContextSizeDisplayAsync();
        }
    }

    private void ChatInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !string.IsNullOrWhiteSpace(ChatInput.Text))
        {
            SendButton_Click(sender, e);
            e.Handled = true;
        }
    }

    private void SendMessage()
    {
        var message = ChatInput.Text?.Trim();
        if (string.IsNullOrEmpty(message)) return;

        // Hide empty state if this is the first message
        if (EmptyStatePanel.Visibility == Visibility.Visible)
        {
            EmptyStatePanel.Visibility = Visibility.Collapsed;
        }

        // Add user message
        AddUserMessage(message);

        // Clear the input
        ChatInput.Text = string.Empty;
        
        // Get AI response
        _ = GetAIResponseAsync(message);
    }

    private void AddUserMessage(string message)
    {
        var userMessageGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            MaxWidth = 400,
            Margin = new Thickness(0, 4, 0, 4)
        };

        var userBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(16, 16, 4, 16),
            Padding = new Thickness(12, 8, 12, 8)
        };

        var userText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        };

        userBorder.Child = userText;
        userMessageGrid.Children.Add(userBorder);
        ChatMessagesPanel.Children.Add(userMessageGrid);

        ScrollToBottom();
        
        // Save user message using context service (database + cache)
        _ = _chatContextService.AddMessageAsync(_currentSessionId, "user", message);
        
        // Update context size display after user message
        _ = UpdateContextSizeDisplayAsync();
    }

    private void AddAIMessage(string message)
    {
        // Parse thinking section if present
        var (thinkingSection, responseSection) = ParseThinkingResponse(message);
        
        // Hide empty state when first message is added
        if (ChatMessagesPanel.Children.Count == 0)
        {
            EmptyStatePanel.Visibility = Visibility.Collapsed;
        }

        var aiMessageGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 400,
            Margin = new Thickness(0, 4, 0, 4)
        };

        aiMessageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Thinking section
        aiMessageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Response section
        aiMessageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Button panel

        var currentRow = 0;

        // Add thinking section if present
        if (!string.IsNullOrEmpty(thinkingSection))
        {
            var thinkingContainer = CreateThinkingSection(thinkingSection);
            Grid.SetRow(thinkingContainer, currentRow);
            aiMessageGrid.Children.Add(thinkingContainer);
            currentRow++;
        }

        // Add main response
        var aiBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16, 16, 16, 4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, string.IsNullOrEmpty(thinkingSection) ? 0 : 4, 0, 0)
        };

        var aiTextBlock = new TextBlock
        {
            Text = responseSection,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        };

        aiBorder.Child = aiTextBlock;
        Grid.SetRow(aiBorder, currentRow);
        aiMessageGrid.Children.Add(aiBorder);
        currentRow++;

        // Add copy button
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0),
            Spacing = 8
        };

        var copyButton = new Button
        {
            Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), // Subtle white background
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), // Subtle white border
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 4, 0, 0)
        };
        ToolTipService.SetToolTip(copyButton, "Copy message");

        // Add hover effects
        copyButton.PointerEntered += (s, e) =>
        {
            copyButton.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            copyButton.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        };
        
        copyButton.PointerExited += (s, e) =>
        {
            copyButton.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
            copyButton.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        };

        var copyButtonContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        var copyIcon = new FontIcon
        {
            Glyph = "\uE8C8",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)) // High contrast white
        };

        var copyText = new TextBlock
        {
            Text = "Copy",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)) // High contrast white
        };

        copyButtonContent.Children.Add(copyIcon);
        copyButtonContent.Children.Add(copyText);
        copyButton.Content = copyButtonContent;

        copyButton.Click += (s, e) => CopyMessageToClipboard(message);

        buttonPanel.Children.Add(copyButton);
        Grid.SetRow(buttonPanel, currentRow);
        aiMessageGrid.Children.Add(buttonPanel);

        ChatMessagesPanel.Children.Add(aiMessageGrid);
        ScrollToBottom();
        
        // Save AI message using context service (database + cache)
        _ = _chatContextService.AddMessageAsync(_currentSessionId, "assistant", message);
        
        // Update context size display after AI response
        _ = UpdateContextSizeDisplayAsync();
    }

    private (string thinking, string response) ParseThinkingResponse(string message)
    {
        // Look for thinking markers like <thinking>...</thinking> or similar patterns
        var thinkingPatterns = new[]
        {
            (@"<thinking>(.*?)</thinking>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            (@"\*\*Thinking:\*\*(.*?)(?=\*\*Response:\*\*|\*\*Answer:\*\*|$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            (@"# Thinking\s*(.*?)(?=# Response|# Answer|$)", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        };

        foreach (var (pattern, options) in thinkingPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern, options);
            if (match.Success)
            {
                var thinking = match.Groups[1].Value.Trim();
                var response = message.Replace(match.Value, "").Trim();
                return (thinking, response);
            }
        }

        // No thinking section found
        return (string.Empty, message);
    }

    private Border CreateThinkingSection(string thinkingContent)
    {
        var thinkingBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorSecondaryBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8)
        };

        var thinkingContainer = new StackPanel
        {
            Spacing = 8
        };

        // Thinking header with expand/collapse button
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var thinkingIcon = new FontIcon
        {
            Glyph = "\uE9F9", // Brain/thinking icon
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
        };
        Grid.SetColumn(thinkingIcon, 0);

        var thinkingHeader = new TextBlock
        {
            Text = "AI Thinking Process",
            FontSize = 12,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(thinkingHeader, 1);

        var expandButton = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            Width = 24,
            Height = 24
        };

        var expandIcon = new FontIcon
        {
            Glyph = "\uE70D", // ChevronDown
            FontSize = 10
        };
        expandButton.Content = expandIcon;
        Grid.SetColumn(expandButton, 2);

        headerGrid.Children.Add(thinkingIcon);
        headerGrid.Children.Add(thinkingHeader);
        headerGrid.Children.Add(expandButton);

        // Thinking content (initially collapsed)
        var thinkingTextBlock = new TextBlock
        {
            Text = thinkingContent,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };

        // Toggle expand/collapse functionality
        var isExpanded = false;
        expandButton.Click += (s, e) =>
        {
            isExpanded = !isExpanded;
            thinkingTextBlock.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            expandIcon.Glyph = isExpanded ? "\uE70E" : "\uE70D"; // ChevronUp : ChevronDown
            ScrollToBottom();
        };

        thinkingContainer.Children.Add(headerGrid);
        thinkingContainer.Children.Add(thinkingTextBlock);
        thinkingBorder.Child = thinkingContainer;

        return thinkingBorder;
    }

    private void CopyMessageToClipboard(string message)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(message);
        Clipboard.SetContent(dataPackage);
    }

    private void InitializeScrollHandlers()
    {
        // Handle scroll events to detect user scrolling
        ChatScrollViewer.ViewChanged += ChatScrollViewer_ViewChanged;
        
        // DirectManipulation events are not implemented in Uno Platform
        // Use alternative approach for cross-platform compatibility
#if WINDOWS
        try
        {
            ChatScrollViewer.DirectManipulationStarted += (s, e) => _isUserScrolling = true;
            ChatScrollViewer.DirectManipulationCompleted += (s, e) => 
            {
                _isUserScrolling = false;
                UpdateAutoScrollState();
            };
        }
        catch
        {
            // Fallback for platforms where DirectManipulation is not available
            // User scrolling detection will rely on ViewChanged events only
        }
#endif
    }

    private void ChatScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (!e.IsIntermediate && !_isUserScrolling)
        {
            UpdateAutoScrollState();
        }
    }

    private void UpdateAutoScrollState()
    {
        var distanceFromBottom = ChatScrollViewer.ScrollableHeight - ChatScrollViewer.VerticalOffset;
        _shouldAutoScroll = distanceFromBottom <= SCROLL_THRESHOLD;
        
        // Show/hide scroll indicator based on auto-scroll state
        UpdateScrollIndicator();
    }

    private void UpdateScrollIndicator()
    {
        // Only show scroll indicator if there are messages and user is not at bottom
        var hasMessages = ChatMessagesPanel.Children.Count > 0 && EmptyStatePanel.Visibility == Visibility.Collapsed;
        var showIndicator = hasMessages && !_shouldAutoScroll;
        
        if (ScrollIndicator != null)
        {
            ScrollIndicator.Visibility = showIndicator ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ScrollToBottom()
    {
        if (_shouldAutoScroll || _isUserScrolling == false)
        {
            // Add extra padding to ensure copy button is fully visible
            var extraPadding = 40; // Account for copy button height + margin
            var targetOffset = Math.Max(0, ChatScrollViewer.ScrollableHeight + extraPadding);
            ChatScrollViewer.ChangeView(null, targetOffset, null, true);
        }
    }

    private void ScrollIndicator_Click(object sender, RoutedEventArgs e)
    {
        _shouldAutoScroll = true;
        // Add extra padding to ensure copy button is fully visible
        var extraPadding = 40; // Account for copy button height + margin
        var targetOffset = Math.Max(0, ChatScrollViewer.ScrollableHeight + extraPadding);
        ChatScrollViewer.ChangeView(null, targetOffset, null, false);
        UpdateScrollIndicator();
    }

    private async Task GetAIResponseAsync(string userMessage)
    {
        try
        {
            // Get AI provider settings
            var localSettings = ApplicationData.Current.LocalSettings;
            var selectedProvider = localSettings.Values["SelectedAIProvider"]?.ToString() ?? "Ollama";

            if (selectedProvider == "OpenAI")
            {
                // OpenAI uses streaming and manages its own typing indicator
                await GetOpenAIStreamingResponseAsync(userMessage);
                return;
            }

            // Show typing indicator for non-streaming providers
            var typingMessage = AddTypingIndicator();

            string response;
            switch (selectedProvider)
            {
                case "Ollama":
                    response = await GetOllamaResponseAsync(userMessage);
                    break;
                case "Anthropic":
                    response = await GetAnthropicResponseAsync(userMessage);
                    break;
                case "Gemini":
                    response = await GetGeminiResponseAsync(userMessage);
                    break;
                case "Mistral":
                    response = await GetMistralResponseAsync(userMessage);
                    break;
                default:
                    response = "No AI provider configured. Please configure an AI provider in Settings.";
                    break;
            }

            // Remove typing indicator and add actual response
            RemoveTypingIndicator(typingMessage);
            AddAIMessage(response);
        }
        catch (Exception ex)
        {
            AddAIMessage($"Error getting AI response: {ex.Message}");
        }
    }

    private Grid AddTypingIndicator()
    {
        var typingGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 500,
            Margin = new Thickness(0, 4, 0, 4)
        };

        var typingBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16, 16, 16, 4),
            Padding = new Thickness(12, 8, 12, 8)
        };

        var typingContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var progressRing = new ProgressRing
        {
            IsActive = true,
            Width = 16,
            Height = 16
        };

        var typingText = new TextBlock
        {
            Text = "AI is thinking...",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };

        typingContent.Children.Add(progressRing);
        typingContent.Children.Add(typingText);
        typingBorder.Child = typingContent;
        typingGrid.Children.Add(typingBorder);
        ChatMessagesPanel.Children.Add(typingGrid);

        ScrollToBottom();
        return typingGrid;
    }

    private void RemoveTypingIndicator(Grid typingIndicator)
    {
        ChatMessagesPanel.Children.Remove(typingIndicator);
    }

    private async Task<string> GetOllamaResponseAsync(string message)
    {
        try
        {
            // Reload configuration to ensure we have the latest settings
            _ollamaService.ReloadConfiguration();
            
            if (!_ollamaService.IsConfigured)
            {
                return "Ollama is not configured. Please:\n1. Open AI Settings\n2. Select Ollama provider\n3. Click 'Refresh' to load available models\n4. Select a model and save settings";
            }

            // Get structured context (files + message history)
            var contextData = await _contextParserService.GetContextDataAsync(_currentSessionId);
            
            Console.WriteLine($"Ollama request with enhanced context:");
            Console.WriteLine($"  - Files: {contextData.FileCount}");
            Console.WriteLine($"  - Messages: {contextData.MessageCount}");
            Console.WriteLine($"  - Total tokens: ~{contextData.TotalTokens}");
            
            var response = await _ollamaService.SendMessageWithContextAsync(message, contextData);
            return response;
        }
        catch (AIServiceException ex)
        {
            return $"Ollama Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Unexpected error with Ollama: {ex.Message}";
        }
    }

    private async Task<string> GetOpenAIResponseAsync(string message)
    {
        try
        {
            if (!_openAIService.IsConfigured)
            {
                return "OpenAI is not configured. Please set your API key and model in AI Settings.";
            }

            // Get structured context (files + message history)
            var contextData = await _contextParserService.GetContextDataAsync(_currentSessionId);
            
            Console.WriteLine($"OpenAI request with enhanced context:");
            Console.WriteLine($"  - Files: {contextData.FileCount}");
            Console.WriteLine($"  - Messages: {contextData.MessageCount}");
            Console.WriteLine($"  - Total tokens: ~{contextData.TotalTokens}");
            
            var response = await _openAIService.SendMessageWithContextAsync(message, contextData);
            return response;
        }
        catch (AIServiceException ex)
        {
            return $"OpenAI Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Unexpected error with OpenAI: {ex.Message}";
        }
    }

    private async Task GetOpenAIStreamingResponseAsync(string message)
    {
        Grid? typingIndicator = null;
        TextBlock? streamingTextBlock = null;
        Grid? streamingMessageGrid = null;
        bool typingIndicatorRemoved = false;
        
        try
        {
            if (!_openAIService.IsConfigured)
            {
                AddAIMessage("OpenAI is not configured. Please set your API key and model in AI Settings.");
                return;
            }

            // Add typing indicator
            typingIndicator = AddTypingIndicator();
            
            // Create streaming message container
            streamingMessageGrid = CreateStreamingMessageContainer();
            var streamingBorder = (Border)streamingMessageGrid.Children[0];
            streamingTextBlock = (TextBlock)streamingBorder.Child;
            
            var fullResponse = new StringBuilder();
            
            // Get structured context (files + message history)
            var contextData = await _contextParserService.GetContextDataAsync(_currentSessionId);
            
            Console.WriteLine($"OpenAI streaming request with enhanced context:");
            Console.WriteLine($"  - Files: {contextData.FileCount}");
            Console.WriteLine($"  - Messages: {contextData.MessageCount}");
            Console.WriteLine($"  - Total tokens: ~{contextData.TotalTokens}");
            
            await _openAIService.SendMessageStreamWithContextAsync(message, contextData, (chunk) =>
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    fullResponse.Append(chunk);
                    
                    // Update UI on main thread
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        // Remove typing indicator only once, on first token
                        if (typingIndicator != null && !typingIndicatorRemoved)
                        {
                            RemoveTypingIndicator(typingIndicator);
                            typingIndicator = null;
                            typingIndicatorRemoved = true;
                        }
                        
                        if (streamingTextBlock != null)
                        {
                            streamingTextBlock.Text = fullResponse.ToString();
                            ScrollToBottom();
                        }
                    });
                }
            });
            
            // Final cleanup on main thread
            DispatcherQueue.TryEnqueue(() =>
            {
                // Ensure typing indicator is removed if no tokens were received
                if (typingIndicator != null && !typingIndicatorRemoved)
                {
                    RemoveTypingIndicator(typingIndicator);
                }
                
                // Replace streaming container with final message
                if (streamingMessageGrid != null)
                {
                    ChatMessagesPanel.Children.Remove(streamingMessageGrid);
                }
                
                var finalResponse = fullResponse.ToString();
                if (!string.IsNullOrEmpty(finalResponse))
                {
                    AddAIMessage(finalResponse);
                }
                else
                {
                    AddAIMessage("No response received from OpenAI.");
                }
            });
        }
        catch (AIServiceException ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (typingIndicator != null && !typingIndicatorRemoved)
                {
                    RemoveTypingIndicator(typingIndicator);
                }
                if (streamingMessageGrid != null)
                {
                    ChatMessagesPanel.Children.Remove(streamingMessageGrid);
                }
                AddAIMessage($"OpenAI Error: {ex.Message}");
            });
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (typingIndicator != null && !typingIndicatorRemoved)
                {
                    RemoveTypingIndicator(typingIndicator);
                }
                if (streamingMessageGrid != null)
                {
                    ChatMessagesPanel.Children.Remove(streamingMessageGrid);
                }
                AddAIMessage($"Unexpected error with OpenAI: {ex.Message}");
            });
        }
    }

    private Grid CreateStreamingMessageContainer()
    {
        var streamingMessageGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 400,
            Margin = new Thickness(0, 4, 0, 4)
        };

        var streamingBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16, 16, 16, 4),
            Padding = new Thickness(12, 8, 12, 8)
        };

        var streamingTextBlock = new TextBlock
        {
            Text = "",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        };

        streamingBorder.Child = streamingTextBlock;
        streamingMessageGrid.Children.Add(streamingBorder);
        ChatMessagesPanel.Children.Add(streamingMessageGrid);
        
        ScrollToBottom();
        return streamingMessageGrid;
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _databaseService.InitializeDatabaseAsync();
            
            // Run database test
            await DatabaseTest.RunDatabaseTestAsync();
            
            // Get the current session ID for chat messages
            _currentSessionId = await _databaseService.GetCurrentSessionIdAsync();
            
            // Initialize context size display
            _ = UpdateContextSizeDisplayAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
            _currentSessionId = 1; // Fallback
        }
    }


    /// <summary>
    /// Updates the context size display in the chat header with total context tokens (files + messages)
    /// </summary>
    private async Task UpdateContextSizeDisplayAsync()
    {
        try
        {
            // Get complete context data (files + messages) for accurate token count
            var contextData = await _contextParserService.GetContextDataAsync(_currentSessionId);
            var totalTokens = contextData.TotalTokens;
            
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ContextSizeDisplay != null)
                {
                    ContextSizeDisplay.Text = $"Context size: {totalTokens:N0} tokens";
                }
            });
            
            Console.WriteLine($"Context size updated: {totalTokens} tokens for session {_currentSessionId} (Files: {contextData.FileCount}, Messages: {contextData.MessageCount})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating context size display: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles context change events with debouncing to prevent excessive updates
    /// </summary>
    private void OnContextChanged(object? sender, ContextChangedEventArgs e)
    {
        // Only handle events for the current session
        if (e.SessionId != _currentSessionId)
            return;

        Console.WriteLine($"Context change detected: {e.ChangeType} for session {e.SessionId}");

        // Cancel existing timer if running
        _contextUpdateTimer?.Stop();

        // Create or reuse timer for debouncing
        if (_contextUpdateTimer == null)
        {
            _contextUpdateTimer = new DispatcherTimer();
            _contextUpdateTimer.Interval = TimeSpan.FromMilliseconds(CONTEXT_UPDATE_DEBOUNCE_MS);
            _contextUpdateTimer.Tick += async (s, args) =>
            {
                _contextUpdateTimer?.Stop();
                await UpdateContextSizeDisplayAsync();
            };
        }

        // Start debounce timer
        _contextUpdateTimer.Start();
    }

    /// <summary>
    /// Updates the ChatContextService with current settings
    /// </summary>
    private void UpdateContextServiceFromSettings()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue("ContextMessages", out var value) && value is int contextSize)
            {
                _chatContextService.SetContextSize(contextSize);
                Console.WriteLine($"Updated context service to use {contextSize} messages");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating context service from settings: {ex.Message}");
        }
    }

    private void OverlayFileDisplayNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // This event handler can be used for real-time validation if needed
        // For now, we'll handle validation during save
    }

    private string GetValidDisplayName()
    {
        var displayName = OverlayFileDisplayNameTextBox?.Text?.Trim() ?? "";
        
        // If display name is less than 4 characters or empty, use filename
        if (displayName.Length < 4 && _currentOverlayFile != null)
        {
            displayName = System.IO.Path.GetFileNameWithoutExtension(_currentOverlayFile.Name);
        }
        
        return displayName;
    }

    private async Task<string> GetAnthropicResponseAsync(string message)
    {
        // Get conversation context for future implementation
        var conversationHistory = await _chatContextService.GetContextForAIAsync(_currentSessionId);
        Console.WriteLine($"Anthropic request with context: {conversationHistory.Count} messages");
        
        // Placeholder - will implement with Anthropic API
        await Task.Delay(2000);
        return "Anthropic integration not yet implemented. Please configure OpenAI or Ollama for now.";
    }

    private async Task<string> GetGeminiResponseAsync(string message)
    {
        // Get conversation context for future implementation
        var conversationHistory = await _chatContextService.GetContextForAIAsync(_currentSessionId);
        Console.WriteLine($"Gemini request with context: {conversationHistory.Count} messages");
        
        // Placeholder - will implement with Gemini API
        await Task.Delay(2000);
        return "Gemini integration not yet implemented. Please configure OpenAI or Ollama for now.";
    }

    private async Task<string> GetMistralResponseAsync(string message)
    {
        // Get conversation context for future implementation
        var conversationHistory = await _chatContextService.GetContextForAIAsync(_currentSessionId);
        Console.WriteLine($"Mistral request with context: {conversationHistory.Count} messages");
        
        // Placeholder - will implement with Mistral API
        await Task.Delay(2000);
        return "Mistral integration not yet implemented. Please configure OpenAI or Ollama for now.";
    }

    // Data model for Ollama response (reusing from AISettingsDialog)
    public class OllamaGenerateResponse
    {
        public string? response { get; set; }
        public bool done { get; set; }
    }

    #region Chat Session Management

    private async void ClearSessionButton_Click(object sender, RoutedEventArgs e)
    {
        // Show confirmation dialog
        var dialog = new ContentDialog()
        {
            Title = "Clear Session",
            Content = "Are you sure you want to clear the current session?\n\nThis will:\n• Clear all chat messages\n• Remove files from context panel\n• Start a new session\n\nNote: Files will remain in the database and can be re-added later.",
            PrimaryButtonText = "Clear Session",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        dialog.XamlRoot = this.XamlRoot;
        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return; // User cancelled
        }

        try
        {
            // Create new session in database
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();
            
            using var command = new SqliteCommand(
                "INSERT INTO session (session_name, user) VALUES (@sessionName, @user); SELECT last_insert_rowid();", 
                connection);
            
            command.Parameters.AddWithValue("@sessionName", $"Chat Session {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            command.Parameters.AddWithValue("@user", "current_user");
            
            var dbResult = await command.ExecuteScalarAsync();
            var newSessionId = Convert.ToInt32(dbResult);
            
            // Store old session ID for cleanup
            var oldSessionId = _currentSessionId;
            
            // Update current session ID to the new one
            _currentSessionId = newSessionId;
            
            // Clear chat context cache for the old session
            _chatContextService.ClearSessionCache(oldSessionId);
            
            // Invalidate context cache for old session and trigger refresh
            await _contextCacheService.InvalidateContextAsync(oldSessionId, ContextChangeTypes.SessionCleared, null, "Session cleared by user");
            
            // Clear and update Context Panel to new session
            await ContextPanelControl.ClearAndUpdateSessionAsync(_currentSessionId);
            
            // Clear chat UI
            ChatMessagesPanel.Children.Clear();
            EmptyStatePanel.Visibility = Visibility.Visible;
            
            // Clear chat input
            ChatInput.Text = string.Empty;
            
            Console.WriteLine($"Session cleared and new session created with ID: {newSessionId}");
            
            // Reset context size display for new session
            _ = UpdateContextSizeDisplayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing session: {ex.Message}");
            await ShowErrorDialog("Session Error", $"Failed to clear session: {ex.Message}");
        }
    }

    private async void ContextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        // Show empty popup for now - feature placeholder
        var dialog = new ContentDialog()
        {
            Title = "Context Options",
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock 
                    { 
                        Text = "Convert chat into context",
                        Margin = new Thickness(0, 8, 0, 8)
                    },
                    new TextBlock 
                    { 
                        Text = "Feature coming soon...",
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 128, 128, 128))
                    }
                }
            },
            CloseButtonText = "Close"
        };
        
        dialog.XamlRoot = this.XamlRoot;
        await dialog.ShowAsync();
    }

    #endregion

    #region Tab Navigation Event Handlers

    private void ChatTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isChatTabActive)
        {
            _isChatTabActive = true;
            UpdateTabStyles();
            // Show chat content, hide macro content
            // Content switching will be implemented when macro tab content is added
        }
    }

    private void MacroTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isChatTabActive)
        {
            _isChatTabActive = false;
            UpdateTabStyles();
            // Show macro content, hide chat content
            // Content switching will be implemented when macro tab content is added
        }
    }

    private void UpdateTabStyles()
    {
        if (_isChatTabActive)
        {
            // Chat tab active style
            ChatTabButton.Background = (Brush)Application.Current.Resources["MaterialPrimaryBrush"];
            ChatTabButton.Foreground = (Brush)Application.Current.Resources["MaterialOnPrimaryBrush"];
            ChatTabButton.BorderThickness = new Thickness(0);

            // Macro tab inactive style
            MacroTabButton.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            MacroTabButton.Foreground = (Brush)Application.Current.Resources["MaterialOnSurfaceBrush"];
            MacroTabButton.BorderBrush = (Brush)Application.Current.Resources["MaterialOutlineBrush"];
            MacroTabButton.BorderThickness = new Thickness(1);
        }
        else
        {
            // Macro tab active style
            MacroTabButton.Background = (Brush)Application.Current.Resources["MaterialPrimaryBrush"];
            MacroTabButton.Foreground = (Brush)Application.Current.Resources["MaterialOnPrimaryBrush"];
            MacroTabButton.BorderThickness = new Thickness(0);

            // Chat tab inactive style
            ChatTabButton.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            ChatTabButton.Foreground = (Brush)Application.Current.Resources["MaterialOnSurfaceBrush"];
            ChatTabButton.BorderBrush = (Brush)Application.Current.Resources["MaterialOutlineBrush"];
            ChatTabButton.BorderThickness = new Thickness(1);
        }
    }

    #endregion

    #region AI Model Indicator

    private void UpdateAIModelIndicator()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var currentProvider = localSettings.Values["CurrentAIProvider"]?.ToString();

            if (string.IsNullOrEmpty(currentProvider))
            {
                AIModelIndicator.Text = "Select an AI provider";
                AIModelIndicator.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                return;
            }

            string modelText = "";
            switch (currentProvider.ToLower())
            {
                case "openai":
                    var openAIModel = localSettings.Values["OpenAIModel"]?.ToString() ?? "gpt-3.5-turbo";
                    modelText = $"OpenAI - {openAIModel}";
                    break;
                case "ollama":
                    var ollamaModel = localSettings.Values["OllamaModel"]?.ToString() ?? "llama2";
                    modelText = $"Ollama - {ollamaModel}";
                    break;
                case "anthropic":
                    var anthropicModel = localSettings.Values["AnthropicModel"]?.ToString() ?? "claude-3-sonnet";
                    modelText = $"Anthropic - {anthropicModel}";
                    break;
                case "gemini":
                    var geminiModel = localSettings.Values["GeminiModel"]?.ToString() ?? "gemini-pro";
                    modelText = $"Gemini - {geminiModel}";
                    break;
                case "mistral":
                    var mistralModel = localSettings.Values["MistralModel"]?.ToString() ?? "mistral-medium";
                    modelText = $"Mistral - {mistralModel}";
                    break;
                default:
                    modelText = $"{currentProvider} - Unknown Model";
                    break;
            }

            AIModelIndicator.Text = modelText;
            AIModelIndicator.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }
        catch (Exception)
        {
            AIModelIndicator.Text = "Select an AI provider";
            AIModelIndicator.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }
    }

    #endregion

    #region File Upload Overlay

    private void ShowFileUploadOverlay()
    {
        FileUploadOverlay.Visibility = Visibility.Visible;
        ResetOverlayState();
    }

    private void HideFileUploadOverlay()
    {
        FileUploadOverlay.Visibility = Visibility.Collapsed;
    }

    private void ResetOverlayState()
    {
        // Reset all overlay controls
        OverlayPreviewTextBox.Text = "";
        OverlayLoadingOverlay.Visibility = Visibility.Collapsed;
        OverlaySelectedFilePanel.Visibility = Visibility.Collapsed;
        OverlaySelectedFileName.Text = "";
        OverlaySelectedFileSize.Text = "";
        
        // Reset button states
        OverlayConvertToTextButton.IsEnabled = false;
        OverlayGenerateSummaryButton.IsEnabled = false;
        OverlaySaveButton.IsEnabled = false;
        OverlaySaveInstructionButton.IsEnabled = false;
        
        // Reset toggle
        OverlayViewModeToggle.IsOn = false;
    }

    private void BackToChatButton_Click(object sender, RoutedEventArgs e)
    {
        HideFileUploadOverlay();
    }

    // File Upload Overlay Event Handlers
    private async void OverlayBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            
            // Initialize the picker with cross-platform approach
#if WINDOWS
            try
            {
                var window = Microsoft.UI.Xaml.Window.Current;
                if (window != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }
            }
            catch
            {
                // Windows-specific initialization failed, continue without it
            }
#endif
            
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".md");
            picker.FileTypeFilter.Add(".docx");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _currentOverlayFile = file;
                
                // Update file info display
                OverlaySelectedFileName.Text = file.Name;
                var fileProperties = await file.GetBasicPropertiesAsync();
                OverlaySelectedFileSize.Text = FormatFileSize(fileProperties.Size);
                
                // Set default display name to filename (without extension)
                var fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                OverlayFileDisplayNameTextBox.Text = fileNameWithoutExtension;
                
                OverlaySelectedFilePanel.Visibility = Visibility.Visible;
                
                // Enable extract text button
                OverlayConvertToTextButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("File Load Error", $"Failed to load file: {ex.Message}");
        }
    }

    private string FormatFileSize(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private async void OverlayConvertToTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOverlayFile == null) return;

        try
        {
            ShowOverlayLoading("Extracting text from file...");
            
            var databaseService = new DatabaseService();
            var fileProcessingService = new FileProcessingService(databaseService);
            var fileData = await fileProcessingService.ProcessFileAsync(_currentOverlayFile.Path);
            
            if (fileData != null && !string.IsNullOrEmpty(fileData.Content))
            {
                // Set the display name from the textbox (with validation)
                fileData.DisplayName = GetValidDisplayName();
                
                OverlayPreviewTextBox.Text = fileData.Content;
                OverlayGenerateSummaryButton.IsEnabled = true;
                OverlaySaveButton.IsEnabled = true;
                _currentOverlayFileData = fileData;
            }
            else
            {
                await ShowErrorDialog("Extraction Error", "Failed to extract text from the file.");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Processing Error", $"Failed to process file: {ex.Message}");
        }
        finally
        {
            HideOverlayLoading();
        }
    }

    private async void OverlayGenerateSummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOverlayFileData == null) return;

        try
        {
            ShowOverlayLoading("Generating AI summary...");
            
            var instruction = string.IsNullOrWhiteSpace(OverlaySummaryInstructionTextBox.Text) 
                ? "You are an executive assistant. Make a summary of the file and keep the original language of the file."
                : OverlaySummaryInstructionTextBox.Text;

            var databaseService = new DatabaseService();
            var fileProcessingService = new FileProcessingService(databaseService);
            var summary = await fileProcessingService.GenerateSummaryAsync(_currentOverlayFileData.Content, instruction);
            
            if (!string.IsNullOrEmpty(summary))
            {
                _currentOverlayFileData.Summary = summary;
                
                // Auto-switch to summary view and update toggle
                OverlayViewModeToggle.IsOn = true;
                OverlayPreviewTextBox.Text = summary;
            }
            else
            {
                await ShowErrorDialog("Summary Error", "Failed to generate summary.");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Summary Error", $"Failed to generate summary: {ex.Message}");
        }
        finally
        {
            HideOverlayLoading();
        }
    }

    private async void OverlaySaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOverlayFileData == null) return;

        try
        {
            ShowOverlayLoading("Saving to database...");
            
            var databaseService = new DatabaseService();
            var fileProcessingService = new FileProcessingService(databaseService);
            
            if (_currentOverlayFileData.Id > 0)
            {
                await fileProcessingService.UpdateFileDataAsync(_currentOverlayFileData);
            }
            else
            {
                await fileProcessingService.SaveFileDataAsync(_currentOverlayFileData);
            }
            
            await ShowSuccessDialog("Save Complete", "File has been saved to the database successfully.");
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Save Error", $"Failed to save file: {ex.Message}");
        }
        finally
        {
            HideOverlayLoading();
        }
    }

    private void OverlayResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetOverlayState();
        _currentOverlayFile = null;
        _currentOverlayFileData = null;
    }

    private void OverlayViewModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_currentOverlayFileData == null) return;

        if (OverlayViewModeToggle.IsOn)
        {
            // Show summary if available
            if (!string.IsNullOrEmpty(_currentOverlayFileData.Summary))
            {
                OverlayPreviewTextBox.Text = _currentOverlayFileData.Summary;
            }
            else
            {
                OverlayPreviewTextBox.Text = "No summary available. Click 'Generate Summary' to create one.";
            }
        }
        else
        {
            // Show raw text
            OverlayPreviewTextBox.Text = _currentOverlayFileData.Content ?? "";
        }
    }

    private void OverlaySummaryInstructionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OverlaySaveInstructionButton.IsEnabled = !string.IsNullOrWhiteSpace(OverlaySummaryInstructionTextBox.Text);
    }

    private async void OverlaySearchInstructionsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var databaseService = new DatabaseService();
            var promptService = new PromptInstructionService(databaseService);
            var dialog = new Dialogs.PromptSearchDialog(promptService);
            dialog.XamlRoot = this.XamlRoot;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.SelectedPrompt != null)
            {
                OverlaySummaryInstructionTextBox.Text = dialog.SelectedPrompt.Instruction;
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Search Error", $"Failed to search instructions: {ex.Message}");
        }
    }

    private async void OverlaySaveInstructionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var databaseService = new DatabaseService();
            var promptService = new PromptInstructionService(databaseService);
            var dialog = new Dialogs.SavePromptDialog(promptService, OverlaySummaryInstructionTextBox.Text);
            dialog.XamlRoot = this.XamlRoot;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ShowSuccessDialog("Instruction Saved", "Custom instruction has been saved successfully.");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Save Error", $"Failed to save instruction: {ex.Message}");
        }
    }

    private void ShowOverlayLoading(string message)
    {
        OverlayLoadingText.Text = message;
        OverlayLoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HideOverlayLoading()
    {
        OverlayLoadingOverlay.Visibility = Visibility.Collapsed;
    }

    // File state tracking for overlay
    private Windows.Storage.StorageFile? _currentOverlayFile;
    private Models.FileData? _currentOverlayFileData;

    // Helper methods for dialogs
    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new ContentDialog()
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ShowSuccessDialog(string title, string message)
    {
        var dialog = new ContentDialog()
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    #endregion

    #region File Search Panel Methods

    private void BtnSearchFile_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("MainPage: Search File button clicked - showing FileSearchPanel");
        ShowFileSearchPanel();
    }

    private void ShowFileSearchPanel()
    {
        try
        {
            Console.WriteLine("MainPage: Showing FileSearchPanel overlay");
            
            // Hide file upload overlay if visible
            FileUploadOverlay.Visibility = Visibility.Collapsed;
            
            // Set the current session ID in FileSearchPanel
            FileSearchOverlay.SetCurrentSession(_currentSessionId);
            
            // Show file search overlay
            FileSearchOverlay.Visibility = Visibility.Visible;
            
            Console.WriteLine($"MainPage: FileSearchPanel overlay shown with session {_currentSessionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MainPage: Error showing FileSearchPanel: {ex.Message}");
        }
    }

    private void FileSearchOverlay_BackRequested(object sender, EventArgs e)
    {
        Console.WriteLine("MainPage: FileSearchPanel back requested - hiding overlay");
        HideFileSearchPanel();
    }

    private void HideFileSearchPanel()
    {
        try
        {
            FileSearchOverlay.Visibility = Visibility.Collapsed;
            Console.WriteLine("MainPage: FileSearchPanel overlay hidden successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MainPage: Error hiding FileSearchPanel: {ex.Message}");
        }
    }

    #endregion
}