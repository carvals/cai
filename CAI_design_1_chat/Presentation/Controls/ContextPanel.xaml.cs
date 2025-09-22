using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using CAI_design_1_chat.Services;

namespace CAI_design_1_chat.Presentation.Controls;

public sealed partial class ContextPanel : UserControl
{
    private readonly DatabaseService _databaseService;
    private int _currentSessionId;

    public ContextPanel()
    {
        this.InitializeComponent();
        _databaseService = new DatabaseService();
    }

    public async Task LoadContextFilesAsync(int sessionId)
    {
        _currentSessionId = sessionId;
        
        try
        {
            var contextFiles = await GetContextFilesForSessionAsync(sessionId);
            
            // Clear existing content
            ContextFilesContainer.Children.Clear();
            
            if (contextFiles.Count == 0)
            {
                // Show empty state
                EmptyStateText.Visibility = Visibility.Visible;
                Console.WriteLine($"Context panel loaded: 0 files for session {sessionId}");
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
                
                Console.WriteLine($"Context panel loaded: {contextFiles.Count} files for session {sessionId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading context files: {ex.Message}");
            EmptyStateText.Text = "Error loading context files";
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }

    private async Task<List<ContextFileInfo>> GetContextFilesForSessionAsync(int sessionId)
    {
        var files = new List<ContextFileInfo>();
        
        using var connection = new SqliteConnection(_databaseService.GetConnectionString());
        await connection.OpenAsync();
        
        var sql = @"
            SELECT 
                cfl.id,
                fd.name as display_name,
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
            
            files.Add(new ContextFileInfo
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
                OriginalName = reader.GetString(reader.GetOrdinal("original_name")),
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

        // File name
        var fileName = new TextBlock
        {
            Text = file.DisplayName,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(fileName, 0);
        headerGrid.Children.Add(fileName);

        // Action buttons (placeholder for now)
        var actionsPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 4 
        };
        
        // Pen button (rename) - placeholder
        var penButton = new Button
        {
            Content = "🖊",
            FontSize = 12,
            Width = 28,
            Height = 28,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(penButton, "Rename");
        
        // Eye button (toggle visibility) - placeholder
        var eyeButton = new Button
        {
            Content = file.IsExcluded ? "👁‍🗨" : "👁",
            FontSize = 12,
            Width = 28,
            Height = 28,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(eyeButton, file.IsExcluded ? "Show in context" : "Hide from context");
        
        // Delete button - placeholder
        var deleteButton = new Button
        {
            Content = "🗑",
            FontSize = 12,
            Width = 28,
            Height = 28,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(deleteButton, "Remove from context");
        
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

        // Use summary checkbox
        var summaryCheckbox = new CheckBox
        {
            Content = "Utiliser le sommaire",
            IsChecked = file.UseSummary,
            FontSize = 12
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
