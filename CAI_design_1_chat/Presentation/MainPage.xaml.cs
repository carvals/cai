using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using System.Net.Http;
using System.Text;
using CAI_design_1_chat.Services;

namespace CAI_design_1_chat.Presentation;

public sealed partial class MainPage : Page
{
    private DispatcherTimer? _animationTimer;
    private double _animationStartWidth;
    private double _animationTargetWidth;
    private DateTime _animationStartTime;
    private TimeSpan _animationDuration = TimeSpan.FromMilliseconds(250);
    
    // AI Services
    private readonly OpenAIService _openAIService;
    private bool _isAnimating = false;
    
    // Auto-scroll state tracking
    private bool _isUserScrolling = false;
    private bool _shouldAutoScroll = true;
    private const double SCROLL_THRESHOLD = 50.0; // pixels from bottom to trigger auto-scroll
    
    // Tab navigation state
    private bool _isChatTabActive = true;

    public MainPage()
    {
        this.InitializeComponent();
        
        // Initialize AI services
        _openAIService = new OpenAIService();
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
            // Update AI model indicator to reflect new settings
            UpdateAIModelIndicator();
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
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4)
        };
        ToolTipService.SetToolTip(copyButton, "Copy message");

        var copyButtonContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        var copyIcon = new FontIcon
        {
            Glyph = "\uE8C8",
            FontSize = 12
        };

        var copyText = new TextBlock
        {
            Text = "Copy",
            FontSize = 12
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

        var thinkingLabel = new TextBlock
        {
            Text = "AI Thinking Process",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(thinkingLabel, 1);

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
        headerGrid.Children.Add(thinkingLabel);
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
            var localSettings = ApplicationData.Current.LocalSettings;
            var serverUrl = localSettings.Values["OllamaUrl"]?.ToString() ?? "http://localhost:11434";
            var model = localSettings.Values["OllamaModel"]?.ToString() ?? "llama2";

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var requestBody = new
            {
                model = model,
                prompt = message,
                stream = false
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{serverUrl}/api/generate", content);
            
            if (!response.IsSuccessStatusCode)
            {
                return $"Ollama API error: {response.StatusCode} - {response.ReasonPhrase}";
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var responseData = System.Text.Json.JsonSerializer.Deserialize<OllamaGenerateResponse>(responseText);
            return responseData?.response ?? "No response from Ollama";
        }
        catch (HttpRequestException ex)
        {
            return $"Connection error: {ex.Message}. Make sure Ollama is running.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
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

            var response = await _openAIService.SendMessageAsync(message);
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
            
            await _openAIService.SendMessageStreamAsync(message, (chunk) =>
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
            var dbService = new DatabaseService();
            await dbService.InitializeDatabaseAsync();
            
            // Run database test
            await DatabaseTest.RunDatabaseTestAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
        }
    }

    private async Task<string> GetAnthropicResponseAsync(string message)
    {
        // Placeholder - will implement with Anthropic API
        await Task.Delay(2000);
        return "Anthropic integration not yet implemented. Please configure Ollama for now.";
    }

    private async Task<string> GetGeminiResponseAsync(string message)
    {
        // Placeholder - will implement with Gemini API
        await Task.Delay(2000);
        return "Gemini integration not yet implemented. Please configure Ollama for now.";
    }

    private async Task<string> GetMistralResponseAsync(string message)
    {
        // Placeholder - will implement with Mistral API
        await Task.Delay(2000);
        return "Mistral integration not yet implemented. Please configure Ollama for now.";
    }

    // Data model for Ollama response (reusing from AISettingsDialog)
    public class OllamaGenerateResponse
    {
        public string? response { get; set; }
        public bool done { get; set; }
    }

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
                
                // Switch to summary view if toggle is on
                if (OverlayViewModeToggle.IsOn)
                {
                    OverlayPreviewTextBox.Text = summary;
                }
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
}