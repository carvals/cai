using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CAI_design_1_chat.Services;

namespace CAI_design_1_chat.Presentation.Controls;

public sealed partial class ContextPanel : UserControl
{
    private DatabaseService _databaseService;
    private ContextObjectService _contextObjectService;
    private int _currentSessionId;

    public ContextPanel()
    {
        this.InitializeComponent();
        _databaseService = new DatabaseService();
        // Note: ContextObjectService will be set via SetServices method
        _contextObjectService = null!; // Will be initialized via SetServices
    }

    /// <summary>
    /// Set the shared service instances (called from MainPage)
    /// </summary>
    public void SetServices(DatabaseService databaseService, ChatContextService chatContextService)
    {
        // Update the database service reference
        _databaseService = databaseService;
        
        // Create ContextObjectService with shared instances
        _contextObjectService = new ContextObjectService(databaseService, chatContextService);
    }

    /// <summary>
    /// Clear context panel UI and update to new session (called when session is cleared)
    /// </summary>
    public async Task ClearAndUpdateSessionAsync(int newSessionId)
    {
        try
        {
            Console.WriteLine($"Clearing context panel and updating to session {newSessionId}");
            
            // Clear current file list from UI
            ContextFilesContainer.Children.Clear();
            
            // Show empty state (keep panel expanded as requested)
            EmptyStateText.Visibility = Visibility.Visible;
            
            // Update to new session
            _currentSessionId = newSessionId;
            
            // Refresh with new session (should show empty state)
            await RefreshContextFilesAsync();
            
            Console.WriteLine($"Context panel cleared and updated to session {newSessionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing context panel: {ex.Message}");
        }
    }

    public async Task LoadContextFilesAsync(int sessionId)
    {
        _currentSessionId = sessionId;
        await RefreshContextFilesAsync();
    }

    private async Task RefreshContextFilesAsync()
    {
        try
        {
            // Guard clause: Don't load if session ID is invalid
            if (_currentSessionId <= 0)
            {
                Console.WriteLine($"ContextPanel: Cannot load context files - invalid session ID: {_currentSessionId}");
                EmptyStateText.Text = "No session selected";
                EmptyStateText.Visibility = Visibility.Visible;
                return;
            }

            Console.WriteLine($"ContextPanel: Loading context files for session {_currentSessionId}");

            // Invalidate cache before refreshing
            if (_databaseService != null)
            {
                var contextCacheService = _databaseService.GetContextCacheService();
                if (contextCacheService != null)
                {
                    await contextCacheService.InvalidateContextAsync(_currentSessionId, ContextChangeTypes.ManualRefresh, null, "Manual context refresh triggered");
                }
            }
            
            var contextFiles = await GetContextFilesForSessionAsync(_currentSessionId);
            
            // Clear existing content
            ContextFilesContainer.Children.Clear();
            
            if (contextFiles.Count == 0)
            {
                // Show empty state
                EmptyStateText.Visibility = Visibility.Visible;
                Console.WriteLine($"Context panel loaded: 0 files for session {_currentSessionId}");
            }
            else
            {
                // Hide empty state and show files
                EmptyStateText.Visibility = Visibility.Collapsed;
                
                foreach (var file in contextFiles)
                {
                    var fileCard = CreateFileCard(file);
                    ContextFilesContainer.Children.Add(fileCard);
                }
                
                Console.WriteLine($"Context panel loaded: {contextFiles.Count} files for session {_currentSessionId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading context files for session {_currentSessionId}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            EmptyStateText.Text = $"Error loading context files (Session: {_currentSessionId})";
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Provide visual feedback during refresh
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                var originalContent = button.Content;
                
                // Show loading state
                var loadingIcon = new FontIcon 
                { 
                    Glyph = "⟳", 
                    FontSize = 14,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                };
                button.Content = loadingIcon;
                
                // Refresh the context files
                await RefreshContextFilesAsync();
                
                // Restore button state
                button.Content = originalContent;
                button.IsEnabled = true;
            }
            
            Console.WriteLine($"Context files refreshed for session {_currentSessionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing context files: {ex.Message}");
            
            // Restore button state on error
            var button = sender as Button;
            if (button != null)
            {
                button.Content = new FontIcon 
                { 
                    Glyph = "🔄", 
                    FontSize = 14,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                };
                button.IsEnabled = true;
            }
        }
    }

    private async void ViewContextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Provide visual feedback during JSON generation
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                var originalContent = button.Content;
                
                // Show loading state
                var loadingIcon = new FontIcon 
                { 
                    Glyph = "⟳", 
                    FontSize = 14,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                };
                button.Content = loadingIcon;
                
                // Generate context JSON
                var contextJson = await _contextObjectService.BuildContextJsonAsync(_currentSessionId);
                
                // Show JSON viewer overlay
                await ShowContextViewerOverlay(contextJson);
                
                // Restore button state
                button.Content = originalContent;
                button.IsEnabled = true;
            }
            
            Console.WriteLine($"Context JSON viewer opened for session {_currentSessionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening context viewer: {ex.Message}");
            
            // Restore button state on error
            var button = sender as Button;
            if (button != null)
            {
                button.Content = new FontIcon 
                { 
                    Glyph = "👁", 
                    FontSize = 14,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                };
                button.IsEnabled = true;
            }
            
            // Show error dialog
            var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Error",
                Content = $"Failed to generate context JSON: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    private async Task ShowContextViewerOverlay(string contextJson)
    {
        // Create JSON viewer dialog
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 600,
            MaxWidth = 800
        };

        var textBlock = new TextBlock
        {
            Text = contextJson,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 12,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(16)
        };

        scrollViewer.Content = textBlock;

        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Context JSON Viewer",
            Content = scrollViewer,
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async Task<List<ContextFileInfo>> GetContextFilesForSessionAsync(int sessionId)
    {
        var files = new List<ContextFileInfo>();
        
        using var connection = new SqliteConnection(_databaseService.GetConnectionString());
        await connection.OpenAsync();
        
        var sql = @"
            SELECT 
                cfl.id,
                fd.display_name,
                cfl.use_summary,
                cfl.is_excluded,
                cfl.order_index,
                fd.name as original_name,
                fd.content,
                fd.summary,
                LENGTH(COALESCE(fd.content, '')) as character_count
            FROM context_file_links cfl
            JOIN file_data fd ON cfl.file_id = fd.id
            WHERE cfl.context_session_id = @sessionId
            ORDER BY cfl.order_index, cfl.id";
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@sessionId", sessionId);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var contentOrdinal = reader.GetOrdinal("content");
            var summaryOrdinal = reader.GetOrdinal("summary");
            var displayNameOrdinal = reader.GetOrdinal("display_name");
            var originalNameOrdinal = reader.GetOrdinal("original_name");
            
            files.Add(new ContextFileInfo
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                DisplayName = reader.IsDBNull(displayNameOrdinal) ? "" : reader.GetString(displayNameOrdinal),
                OriginalName = reader.IsDBNull(originalNameOrdinal) ? "" : reader.GetString(originalNameOrdinal),
                UseSummary = reader.GetBoolean(reader.GetOrdinal("use_summary")),
                IsExcluded = reader.GetBoolean(reader.GetOrdinal("is_excluded")),
                OrderIndex = reader.GetInt32(reader.GetOrdinal("order_index")),
                Content = reader.IsDBNull(contentOrdinal) ? "" : reader.GetString(contentOrdinal),
                Summary = reader.IsDBNull(summaryOrdinal) ? "" : reader.GetString(summaryOrdinal),
                CharacterCount = reader.GetInt32(reader.GetOrdinal("character_count"))
            });
        }
        
        return files;
    }

    private Border CreateFileCard(ContextFileInfo file)
    {
        var card = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stackPanel = new StackPanel { Spacing = 8 };

        // File name and action buttons row
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // File name container (for switching between TextBlock and TextBox)
        var fileNameContainer = new Grid();
        
        // File name display (normal state)
        var fileName = new TextBlock
        {
            Text = file.DisplayName,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            Tag = file.Id // Store file ID for reference
        };
        
        // File name editor (edit state) - initially hidden
        var fileNameEditor = new TextBox
        {
            Text = file.DisplayName,
            FontSize = 14,
            Visibility = Visibility.Collapsed,
            Tag = file.Id // Store file ID for reference
        };
        
        fileNameContainer.Children.Add(fileName);
        fileNameContainer.Children.Add(fileNameEditor);
        
        Grid.SetColumn(fileNameContainer, 0);
        headerGrid.Children.Add(fileNameContainer);

        // Action buttons (placeholder for now)
        var actionsPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 4 
        };
        
        // Pen button (rename) - now functional
        var penButton = new AppBarButton
        {
            Width = 28,
            Height = 28,
            Tag = file.Id // Store file ID for reference
        };
        penButton.Icon = new FontIcon
        {
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            Glyph = "\uE70F" // Edit/Rename icon
        };
        ToolTipService.SetToolTip(penButton, "Rename");
        
        // Add click handler for rename functionality
        penButton.Click += async (sender, e) =>
        {
            await StartEditingFileName(fileNameContainer, fileName, fileNameEditor, file);
        };
        
        // Eye button (toggle visibility) - now functional
        var eyeButton = new AppBarButton
        {
            Width = 28,
            Height = 28,
            Tag = file.Id // Store file ID for reference
        };
        eyeButton.Icon = new FontIcon
        {
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            Glyph = file.IsExcluded ? "\uE7B3" : "\uE890" // Hidden/Visible icon
        };
        ToolTipService.SetToolTip(eyeButton, file.IsExcluded ? "Show in context" : "Hide from context");
        
        // Add click handler for visibility toggle
        eyeButton.Click += async (sender, e) =>
        {
            await ToggleFileVisibility(eyeButton, card, file);
        };
        
        // Delete button - now functional
        var deleteButton = new AppBarButton
        {
            Width = 28,
            Height = 28,
            Tag = file.Id // Store file ID for reference
        };
        deleteButton.Icon = new FontIcon
        {
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            Glyph = "\uE74D" // Delete icon
        };
        ToolTipService.SetToolTip(deleteButton, "Remove from context");
        
        // Add click handler for delete functionality
        deleteButton.Click += async (sender, e) =>
        {
            await DeleteFileFromContext(card, file);
        };
        
        actionsPanel.Children.Add(penButton);
        actionsPanel.Children.Add(eyeButton);
        actionsPanel.Children.Add(deleteButton);
        
        Grid.SetColumn(actionsPanel, 1);
        headerGrid.Children.Add(actionsPanel);
        stackPanel.Children.Add(headerGrid);

        // Character count
        var characterCount = new TextBlock
        {
            Text = $"{file.CharacterCount:N0} Caractères",
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        stackPanel.Children.Add(characterCount);

        // Summary checkbox - now functional
        var summaryCheckbox = new CheckBox
        {
            Content = "Utiliser le sommaire",
            IsChecked = file.UseSummary,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Tag = file.Id // Store file ID for reference
        };
        
        // Add change handler for summary checkbox
        summaryCheckbox.Checked += async (sender, e) =>
        {
            await UpdateUseSummary(file, true);
        };
        
        summaryCheckbox.Unchecked += async (sender, e) =>
        {
            await UpdateUseSummary(file, false);
        };
        
        stackPanel.Children.Add(summaryCheckbox);

        // Apply visual state for excluded files
        if (file.IsExcluded)
        {
            card.Opacity = 0.6;
        }

        card.Child = stackPanel;
        return card;
    }

    private async Task StartEditingFileName(Grid container, TextBlock displayName, TextBox editor, ContextFileInfo file)
    {
        try
        {
            // Switch to edit mode
            displayName.Visibility = Visibility.Collapsed;
            editor.Visibility = Visibility.Visible;
            editor.Focus(FocusState.Programmatic);
            editor.SelectAll();
            
            // Store original name for validation
            var originalName = editor.Text;
            
            // Add event handlers for completing the edit
            void CompleteEdit() => _ = CompleteEditingFileName(container, displayName, editor, file, originalName);
            void CancelEdit() => CancelEditingFileName(container, displayName, editor, originalName);
            
            // Handle Enter key (complete edit)
            editor.KeyDown += (sender, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    CompleteEdit();
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    CancelEdit();
                    e.Handled = true;
                }
            };
            
            // Handle losing focus (complete edit)
            editor.LostFocus += (sender, e) => CompleteEdit();
            
            Console.WriteLine($"Started editing file name: {file.DisplayName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting file name edit: {ex.Message}");
        }
    }

    private async Task CompleteEditingFileName(Grid container, TextBlock displayName, TextBox editor, ContextFileInfo file, string originalName)
    {
        try
        {
            var newName = editor.Text.Trim();
            
            // Validate new name
            if (string.IsNullOrEmpty(newName))
            {
                // Revert to original name if empty
                editor.Text = originalName;
                CancelEditingFileName(container, displayName, editor, originalName);
                return;
            }
            
            // Check for duplicates (excluding current file)
            if (await IsDisplayNameDuplicate(newName, file.Id))
            {
                // Show error and revert
                Console.WriteLine($"Display name '{newName}' already exists. Reverting to '{originalName}'");
                editor.Text = originalName;
                CancelEditingFileName(container, displayName, editor, originalName);
                return;
            }
            
            // Update database
            await UpdateFileDisplayName(file.Id, newName);
            
            // Update UI
            displayName.Text = newName;
            file.DisplayName = newName;
            
            // Switch back to display mode
            editor.Visibility = Visibility.Collapsed;
            displayName.Visibility = Visibility.Visible;
            
            Console.WriteLine($"File renamed: '{originalName}' → '{newName}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error completing file name edit: {ex.Message}");
            CancelEditingFileName(container, displayName, editor, originalName);
        }
    }

    private void CancelEditingFileName(Grid container, TextBlock displayName, TextBox editor, string originalName)
    {
        try
        {
            // Revert to original name
            editor.Text = originalName;
            
            // Switch back to display mode
            editor.Visibility = Visibility.Collapsed;
            displayName.Visibility = Visibility.Visible;
            
            Console.WriteLine($"File rename cancelled: {originalName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cancelling file name edit: {ex.Message}");
        }
    }

    private async Task<bool> IsDisplayNameDuplicate(string displayName, int excludeFileId)
    {
        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();
            
            // Check for duplicates in file_data table (global check)
            var sql = @"
                SELECT COUNT(*) 
                FROM file_data 
                WHERE display_name = @displayName 
                AND id != (
                    SELECT file_id 
                    FROM context_file_links 
                    WHERE id = @excludeId
                )";
            
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@displayName", displayName);
            command.Parameters.AddWithValue("@excludeId", excludeFileId);
            
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking display name duplicate: {ex.Message}");
            return false; // Allow rename if check fails
        }
    }

    private async Task UpdateFileDisplayName(int contextLinkId, string newDisplayName)
    {
        try
        {
            // Use the new context-aware database method
            await _databaseService.UpdateContextFileDisplayNameAsync(contextLinkId, newDisplayName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating file display name: {ex.Message}");
            throw;
        }
    }

    private async Task ToggleFileVisibility(AppBarButton eyeButton, Border card, ContextFileInfo file)
    {
        try
        {
            // Toggle the excluded state
            file.IsExcluded = !file.IsExcluded;
            
            // Update database
            await UpdateFileVisibility(file.Id, file.IsExcluded);
            
            // Update button icon and tooltip
            ((FontIcon)eyeButton.Icon).Glyph = file.IsExcluded ? "\uE7B3" : "\uE890";
            ToolTipService.SetToolTip(eyeButton, file.IsExcluded ? "Show in context" : "Hide from context");
            
            // Update visual state of the card
            card.Opacity = file.IsExcluded ? 0.6 : 1.0;
            
            Console.WriteLine($"File visibility toggled: {file.DisplayName} → {(file.IsExcluded ? "excluded" : "included")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling file visibility: {ex.Message}");
            
            // Revert the state on error
            file.IsExcluded = !file.IsExcluded;
        }
    }

    private async Task UpdateFileVisibility(int contextLinkId, bool isExcluded)
    {
        try
        {
            // Use the new context-aware database method
            await _databaseService.UpdateContextFileVisibilityAsync(contextLinkId, isExcluded);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating file visibility: {ex.Message}");
            throw;
        }
    }

    private async Task DeleteFileFromContext(Border card, ContextFileInfo file)
    {
        try
        {
            // Show confirmation dialog
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Remove from Context",
                Content = $"Are you sure you want to remove \"{file.DisplayName}\" from the context?\n\nThis will not delete the file itself, only remove it from this context session.",
                PrimaryButtonText = "Remove",
                SecondaryButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            
            if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                // Remove from database using context-aware method
                await _databaseService.RemoveContextFileAsync(file.Id);
                
                // Remove from UI
                var parent = card.Parent as StackPanel;
                if (parent != null)
                {
                    parent.Children.Remove(card);
                    
                    // Check if we need to show empty state
                    if (parent.Children.Count == 0)
                    {
                        EmptyStateText.Visibility = Visibility.Visible;
                    }
                }
                
                Console.WriteLine($"File removed from context: {file.DisplayName}");
            }
            else
            {
                Console.WriteLine($"File removal cancelled: {file.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting file from context: {ex.Message}");
            
            // Show error dialog
            var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Error",
                Content = $"Failed to remove file from context: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }


    private async Task UpdateUseSummary(ContextFileInfo file, bool useSummary)
    {
        try
        {
            // Update the file object
            file.UseSummary = useSummary;
            
            // Update database
            await UpdateFileSummaryUsage(file.Id, useSummary);
            
            Console.WriteLine($"Summary usage updated: {file.DisplayName} → use_summary = {useSummary}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating summary usage: {ex.Message}");
            
            // Revert the state on error
            file.UseSummary = !useSummary;
        }
    }

    private async Task UpdateFileSummaryUsage(int contextLinkId, bool useSummary)
    {
        try
        {
            // Use the new context-aware database method
            await _databaseService.UpdateContextFileSummaryUsageAsync(contextLinkId, useSummary);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating file summary usage: {ex.Message}");
            throw;
        }
    }

    public class ContextFileInfo
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = "";
        public string OriginalName { get; set; } = "";
        public bool UseSummary { get; set; }
        public bool IsExcluded { get; set; }
        public int OrderIndex { get; set; }
        public string Content { get; set; } = "";
        public string Summary { get; set; } = "";
        public int CharacterCount { get; set; }
    }
}
