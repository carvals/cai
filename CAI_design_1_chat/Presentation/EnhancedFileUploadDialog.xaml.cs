using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using CAI_design_1_chat.Services;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Presentation;

public sealed partial class EnhancedFileUploadDialog : ContentDialog
{
    private readonly FileProcessingService _fileProcessingService;
    private readonly DatabaseService _databaseService;
    private StorageFile? _selectedFile;
    private string? _extractedText;
    private string? _generatedSummary;
    private bool _isInSummaryMode = false;

    public EnhancedFileUploadDialog()
    {
        this.InitializeComponent();
        _databaseService = new DatabaseService();
        _fileProcessingService = new FileProcessingService(_databaseService);
        
        // Initialize LLM indicator
        UpdateLLMIndicator();
        
        // Set up event handlers
        this.Loaded += EnhancedFileUploadDialog_Loaded;
    }

    private async void EnhancedFileUploadDialog_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize database if needed
        try
        {
            await _databaseService.InitializeDatabaseAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
        }
    }

    private void UpdateLLMIndicator()
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var selectedProvider = localSettings.Values["SelectedAIProvider"]?.ToString() ?? "Not configured";
            
            string llmText = selectedProvider switch
            {
                "Ollama" => $"LLM: Ollama - {localSettings.Values["OllamaModel"]?.ToString() ?? "No model"}",
                "OpenAI" => $"LLM: OpenAI - {localSettings.Values["OpenAIModel"]?.ToString() ?? "gpt-3.5-turbo"}",
                "Anthropic" => $"LLM: Anthropic - {localSettings.Values["AnthropicModel"]?.ToString() ?? "claude-3-sonnet"}",
                "Gemini" => $"LLM: Gemini - {localSettings.Values["GeminiModel"]?.ToString() ?? "gemini-pro"}",
                "Mistral" => $"LLM: Mistral - {localSettings.Values["MistralModel"]?.ToString() ?? "mistral-medium"}",
                _ => "LLM: Not configured"
            };
            
            LLMIndicatorText.Text = llmText;
        }
        catch (Exception ex)
        {
            LLMIndicatorText.Text = "LLM: Error loading settings";
            System.Diagnostics.Debug.WriteLine($"Error updating LLM indicator: {ex.Message}");
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenFilePickerAsync();
    }

    private async Task OpenFilePickerAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".docx");
            picker.FileTypeFilter.Add(".md");
            
            // Get the current window handle for WinUI 3
            var window = Microsoft.UI.Xaml.Window.Current;
            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            }

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await SetSelectedFileAsync(file);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to open file picker: {ex.Message}");
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        
        // Visual feedback
        if (sender is Border border)
        {
            border.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Blue);
            border.BorderThickness = new Thickness(3);
        }
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        // Reset visual feedback
        if (sender is Border border)
        {
            border.BorderBrush = Application.Current.Resources["MaterialOutlineVariantBrush"] as Brush;
            border.BorderThickness = new Thickness(2);
        }

        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
                        if (extension == ".txt" || extension == ".pdf" || extension == ".docx" || extension == ".md")
                        {
                            await SetSelectedFileAsync(file);
                            break; // Only process first valid file
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to process dropped file: {ex.Message}");
        }
    }

    private async Task SetSelectedFileAsync(StorageFile file)
    {
        _selectedFile = file;
        
        // Update UI
        SelectedFileText.Text = $"Selected File: {file.Name}";
        var properties = await file.GetBasicPropertiesAsync();
        FileSizeText.Text = $"Size: {FormatFileSize(properties.Size)}";
        
        FileInfoPanel.Visibility = Visibility.Visible;
        ConvertToTextButton.IsEnabled = true;
        GenerateSummaryButton.IsEnabled = true;
        
        // Clear previous content
        _extractedText = null;
        _generatedSummary = null;
        PreviewTextBox.Text = string.Empty;
        _isInSummaryMode = false;
        ViewModeToggle.IsOn = false;
    }

    private string FormatFileSize(ulong bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private async void ConvertToTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFile == null) return;

        try
        {
            ShowLoading("Extracting text...");
            
            // Extract text content
            _extractedText = await ExtractTextFromFileAsync(_selectedFile);
            
            // Show in preview if in text mode
            if (!_isInSummaryMode)
            {
                PreviewTextBox.Text = _extractedText ?? "No content extracted";
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to extract text: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void GenerateSummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFile == null) return;

        try
        {
            ShowLoading("Generating summary...");
            
            // Extract text if not already done
            if (string.IsNullOrEmpty(_extractedText))
            {
                _extractedText = await ExtractTextFromFileAsync(_selectedFile);
            }
            
            if (string.IsNullOrEmpty(_extractedText))
            {
                await ShowErrorAsync("No text content available for summarization");
                return;
            }
            
            // Generate AI summary
            _generatedSummary = await GenerateAISummaryAsync(_extractedText);
            
            // Show in preview if in summary mode
            if (_isInSummaryMode)
            {
                PreviewTextBox.Text = _generatedSummary ?? "Failed to generate summary";
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to generate summary: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        // Reset all state
        _selectedFile = null;
        _extractedText = null;
        _generatedSummary = null;
        _isInSummaryMode = false;
        
        // Reset UI
        FileInfoPanel.Visibility = Visibility.Collapsed;
        ConvertToTextButton.IsEnabled = false;
        GenerateSummaryButton.IsEnabled = false;
        PreviewTextBox.Text = string.Empty;
        ViewModeToggle.IsOn = false;
        
        // Reset drop zone visual state
        DropZone.BorderBrush = Application.Current.Resources["MaterialOutlineVariantBrush"] as Brush;
        DropZone.BorderThickness = new Thickness(2);
    }

    private void ViewModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _isInSummaryMode = ViewModeToggle.IsOn;
        
        // Update preview content based on mode
        if (_isInSummaryMode)
        {
            PreviewTextBox.Text = _generatedSummary ?? "No summary available. Click 'Faire un résumé' to generate.";
        }
        else
        {
            PreviewTextBox.Text = _extractedText ?? "No text content available. Click 'Convertir en text brut' to extract.";
        }
    }

    private async Task<string> ExtractTextFromFileAsync(StorageFile file)
    {
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        
        switch (extension)
        {
            case ".txt":
            case ".md":
                return await FileIO.ReadTextAsync(file);
            
            case ".pdf":
                // Placeholder for PDF extraction
                return $"[PDF Content from {file.Name}]\n\nPDF text extraction not yet implemented. Please install a PDF processing library like iTextSharp or PdfPig.";
            
            case ".docx":
                // Placeholder for DOCX extraction
                return $"[DOCX Content from {file.Name}]\n\nDOCX text extraction not yet implemented. Please install DocumentFormat.OpenXml package.";
            
            default:
                throw new NotSupportedException($"File type {extension} is not supported");
        }
    }

    private async Task<string> GenerateAISummaryAsync(string content)
    {
        try
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var selectedProvider = localSettings.Values["SelectedAIProvider"]?.ToString() ?? "Not configured";
            
            if (selectedProvider == "Not configured")
            {
                return "AI provider not configured. Please configure an AI provider in settings.";
            }
            
            // Use the same AI services as the main chat
            switch (selectedProvider)
            {
                case "Ollama":
                    return await GetOllamaSummaryAsync(content);
                case "OpenAI":
                    return await GetOpenAISummaryAsync(content);
                case "Anthropic":
                    return await GetAnthropicSummaryAsync(content);
                default:
                    return $"Summary generation not implemented for {selectedProvider} provider.";
            }
        }
        catch (Exception ex)
        {
            return $"Failed to generate summary: {ex.Message}";
        }
    }

    private async Task<string> GetOllamaSummaryAsync(string content)
    {
        // Placeholder for Ollama integration
        await Task.Delay(2000); // Simulate processing
        return $"[AI Summary]\n\nThis document contains {content.Split(' ').Length} words. Summary generation with Ollama is not yet fully implemented.";
    }

    private async Task<string> GetOpenAISummaryAsync(string content)
    {
        // Placeholder for OpenAI integration
        await Task.Delay(2000); // Simulate processing
        return $"[AI Summary]\n\nThis document contains {content.Split(' ').Length} words. Summary generation with OpenAI is not yet fully implemented.";
    }

    private async Task<string> GetAnthropicSummaryAsync(string content)
    {
        // Placeholder for Anthropic integration
        await Task.Delay(2000); // Simulate processing
        return $"[AI Summary]\n\nThis document contains {content.Split(' ').Length} words. Summary generation with Anthropic is not yet fully implemented.";
    }

    private void ShowLoading(string message)
    {
        LoadingText.Text = message;
        LoadingOverlay.Visibility = Visibility.Visible;
    }

    private void HideLoading()
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task ShowErrorAsync(string message)
    {
        var errorDialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };

        await errorDialog.ShowAsync();
    }

    // Handle primary button (Save) click
    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Defer closing to allow async operation
        args.Cancel = true;
        
        try
        {
            await SaveContentToDatabaseAsync();
            // Close dialog after successful save
            this.Hide();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to save: {ex.Message}");
        }
    }

    private async Task SaveContentToDatabaseAsync()
    {
        try
        {
            if (_selectedFile == null || string.IsNullOrEmpty(PreviewTextBox.Text))
            {
                await ShowErrorAsync("No content to save");
                return;
            }

            ShowLoading("Saving to database...");

            var basicProperties = await _selectedFile.GetBasicPropertiesAsync();
            var fileData = new FileData
            {
                Name = _selectedFile.Name,
                OriginalFilePath = _selectedFile.Path,
                FileSize = (long)basicProperties.Size,
                FileType = Path.GetExtension(_selectedFile.Name),
                Content = PreviewTextBox.Text,
                Summary = _isInSummaryMode ? PreviewTextBox.Text : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProcessingStatus = "completed"
            };

            // Save using FileProcessingService
            await _fileProcessingService.ProcessFileAsync(_selectedFile.Path);
            
            HideLoading();
        }
        catch (Exception ex)
        {
            HideLoading();
            await ShowErrorAsync($"Failed to save content: {ex.Message}");
        }
    }
}
