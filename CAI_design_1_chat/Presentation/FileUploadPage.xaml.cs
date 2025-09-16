using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using System;
using System.IO;
using System.Threading.Tasks;
using CAI_design_1_chat.Services;
using CAI_design_1_chat.Models;
using CAI_design_1_chat.Presentation.Dialogs;
using WinRT.Interop;

namespace CAI_design_1_chat.Presentation
{
    public sealed partial class FileUploadPage : Page
    {
        private StorageFile? _selectedFile;
        private FileProcessingService _fileProcessingService;
        private DatabaseService _databaseService;
        private IPromptInstructionService _promptService;
        private string _rawContent = string.Empty;
        private string _summaryContent = string.Empty;
        private bool _isInSummaryMode = false;
        private FileData? _currentFileData = null;

        public FileUploadPage()
        {
            this.InitializeComponent();
            _databaseService = new DatabaseService();
            _fileProcessingService = new FileProcessingService(_databaseService);
            _promptService = new PromptInstructionService(_databaseService);
            UpdateLLMIndicator();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Initialize database when page loads
            _ = InitializeDatabaseAsync();
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                await _databaseService.InitializeDatabaseAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Database initialization failed: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back to MainPage (chat)
            Frame.GoBack();
        }

        private async void AISettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AISettingsDialog
            {
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                dialog.SaveSettings();
                UpdateLLMIndicator();
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            
            // Initialize the picker with the current window
            try
            {
                var window = Microsoft.UI.Xaml.Window.Current;
                if (window != null)
                {
                    var hwnd = WindowNative.GetWindowHandle(window);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }
            }
            catch
            {
                // Fallback - picker might still work without window initialization
            }
            
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".docx");
            picker.FileTypeFilter.Add(".md");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await HandleFileSelection(file);
            }
        }

        private async void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            
            // Visual feedback for drag over
            if (sender is Border border)
            {
                border.BorderBrush = App.Current.Resources["MaterialPrimaryBrush"] as Microsoft.UI.Xaml.Media.Brush;
                border.BorderThickness = new Thickness(3);
            }
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            // Reset visual feedback
            if (sender is Border border)
            {
                border.BorderBrush = App.Current.Resources["MaterialOutlineVariantBrush"] as Microsoft.UI.Xaml.Media.Brush;
                border.BorderThickness = new Thickness(2);
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    await HandleFileSelection(file);
                }
            }
        }

        private async Task HandleFileSelection(StorageFile file)
        {
            try
            {
                _selectedFile = file;
                
                // Update UI with file info
                SelectedFileText.Text = $"Selected File: {file.Name}";
                var properties = await file.GetBasicPropertiesAsync();
                FileSizeText.Text = $"Size: {FormatFileSize((long)properties.Size)}";
                
                // Show file info panel
                FileInfoPanel.Visibility = Visibility.Visible;
                
                // Enable action buttons
                ConvertToTextButton.IsEnabled = true;
                GenerateSummaryButton.IsEnabled = true;
                
                // Clear preview text and wait for user to click Extract Text
                PreviewTextBox.Text = "Click 'Extract Text' to process the file content.";
                _rawContent = string.Empty;
                _summaryContent = string.Empty;
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Error handling file: {ex.Message}");
            }
        }

        private async void ConvertToTextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null) return;
            
            await ExtractTextFromFile();
        }

        private async void GenerateSummaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_rawContent))
            {
                await ShowErrorAsync("No content to summarize. Please extract text first.");
                return;
            }

            ShowLoading("Generating AI summary...");
            
            try
            {
                // Get custom instruction from TextBox or use default
                var customInstruction = string.IsNullOrWhiteSpace(SummaryInstructionTextBox.Text) 
                    ? "You are an executive assistant. Make a summary of the file and keep the original language of the file."
                    : SummaryInstructionTextBox.Text.Trim();

                // Generate summary with custom instruction
                _summaryContent = await _fileProcessingService.GenerateSummaryAsync(_rawContent, customInstruction);
                
                // Switch to summary view
                ViewModeToggle.IsOn = true;
                PreviewTextBox.Text = _summaryContent;
                _isInSummaryMode = true;
                
                // Enable save button
                SaveButton.IsEnabled = true;
                
                HideLoading();
            }
            catch (Exception ex)
            {
                HideLoading();
                await ShowErrorAsync($"Error generating summary: {ex.Message}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile == null || string.IsNullOrEmpty(PreviewTextBox.Text))
            {
                await ShowErrorAsync("No content to save");
                return;
            }

            ShowLoading("Saving to database...");

            try
            {
                if (_currentFileData != null)
                {
                    // Update existing file data with the generated summary
                    _currentFileData.Summary = _isInSummaryMode ? _summaryContent : null;
                    _currentFileData.ProcessingStatus = "completed";
                    
                    await _fileProcessingService.UpdateFileDataAsync(_currentFileData);
                }
                else
                {
                    // Create new file data for non-PDF files
                    var basicProperties = await _selectedFile.GetBasicPropertiesAsync();
                    var fileData = new FileData
                    {
                        Name = _selectedFile.Name,
                        OriginalFilePath = _selectedFile.Path,
                        FileSize = (long)basicProperties.Size,
                        FileType = Path.GetExtension(_selectedFile.Name),
                        Content = _rawContent,
                        Summary = _isInSummaryMode ? _summaryContent : null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        ProcessingStatus = "completed"
                    };

                    // Save new file data
                    _currentFileData = await _fileProcessingService.ProcessFileAsync(_selectedFile.Path);
                    if (_isInSummaryMode && !string.IsNullOrEmpty(_summaryContent))
                    {
                        _currentFileData.Summary = _summaryContent;
                        await _fileProcessingService.UpdateFileDataAsync(_currentFileData);
                    }
                }
                
                HideLoading();
                
                // Show success message and reset
                await ShowSuccessAsync("File saved successfully!");
                ResetForm();
            }
            catch (Exception ex)
            {
                HideLoading();
                await ShowErrorAsync($"Failed to save: {ex.Message}");
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetForm();
        }

        private void ViewModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (ViewModeToggle.IsOn)
            {
                // Switch to summary mode
                if (!string.IsNullOrEmpty(_summaryContent))
                {
                    PreviewTextBox.Text = _summaryContent;
                }
                else
                {
                    PreviewTextBox.Text = "Click 'Generate Summary' to create an AI-powered summary of the content.";
                }
                _isInSummaryMode = true;
            }
            else if (!string.IsNullOrEmpty(_rawContent))
            {
                // Switch to raw text mode
                PreviewTextBox.Text = _rawContent;
                _isInSummaryMode = false;
            }
        }

        private async Task ExtractTextFromFile()
        {
            if (_selectedFile == null) return;

            ShowLoading("Extracting text from file...");

            try
            {
                // Extract text based on file type
                switch (Path.GetExtension(_selectedFile.Name).ToLowerInvariant())
                {
                    case ".txt":
                    case ".md":
                        _rawContent = await File.ReadAllTextAsync(_selectedFile.Path);
                        break;
                    case ".pdf":
                        _currentFileData = await _fileProcessingService.ProcessFileAsync(_selectedFile.Path);
                        _rawContent = _currentFileData.Content ?? "Failed to extract PDF content.";
                        break;
                    case ".docx":
                        _rawContent = "DOCX text extraction not yet implemented. Please use a text file for now.";
                        break;
                    default:
                        _rawContent = "Unsupported file type.";
                        break;
                }
                
                if (!ViewModeToggle.IsOn)
                {
                    PreviewTextBox.Text = _rawContent;
                    _isInSummaryMode = false;
                }
                
                // Enable summary generation
                GenerateSummaryButton.IsEnabled = !string.IsNullOrEmpty(_rawContent);
                
                HideLoading();
            }
            catch (Exception ex)
            {
                HideLoading();
                await ShowErrorAsync($"Error extracting text: {ex.Message}");
            }
        }

        private string GenerateBasicSummary(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "No content to summarize.";

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var summary = $"Document Summary:\n\n";
            summary += $"• Total lines: {lines.Length}\n";
            summary += $"• Total words: {words.Length}\n";
            summary += $"• Total characters: {content.Length}\n\n";
            
            if (lines.Length > 0)
            {
                summary += $"First few lines:\n";
                for (int i = 0; i < Math.Min(3, lines.Length); i++)
                {
                    summary += $"• {lines[i].Trim()}\n";
                }
            }

            return summary;
        }

        private void ResetForm()
        {
            _selectedFile = null;
            _rawContent = string.Empty;
            _summaryContent = string.Empty;
            _isInSummaryMode = false;
            
            // Reset UI
            FileInfoPanel.Visibility = Visibility.Collapsed;
            PreviewTextBox.Text = string.Empty;
            ViewModeToggle.IsOn = false;
            
            // Disable buttons
            ConvertToTextButton.IsEnabled = false;
            GenerateSummaryButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            
            // Reset drop zone visual state
            DropZone.BorderBrush = App.Current.Resources["MaterialOutlineVariantBrush"] as Microsoft.UI.Xaml.Media.Brush;
            DropZone.BorderThickness = new Thickness(2);
        }

        private void UpdateLLMIndicator()
        {
            // Read AI provider settings from local storage
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var selectedProvider = localSettings.Values["SelectedAIProvider"]?.ToString() ?? "Ollama";
            
            string model = "";
            switch (selectedProvider)
            {
                case "OpenAI":
                    model = localSettings.Values["OpenAIModel"]?.ToString() ?? "gpt-4";
                    break;
                case "Anthropic":
                    model = localSettings.Values["AnthropicModel"]?.ToString() ?? "claude-3-sonnet";
                    break;
                case "Gemini":
                    model = localSettings.Values["GeminiModel"]?.ToString() ?? "gemini-pro";
                    break;
                case "Mistral":
                    model = localSettings.Values["MistralModel"]?.ToString() ?? "mistral-large";
                    break;
                case "Ollama":
                default:
                    model = localSettings.Values["OllamaModel"]?.ToString() ?? "llama2";
                    break;
            }
            
            LLMIndicatorText.Text = $"AI Model: {selectedProvider} - {model}";
        }

        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            StatusPanel.Visibility = Visibility.Visible;
            StatusText.Text = message;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            StatusPanel.Visibility = Visibility.Collapsed;
        }

        private string FormatFileSize(long bytes)
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

        private async Task ShowSuccessAsync(string message)
        {
            var successDialog = new ContentDialog
            {
                Title = "Success",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await successDialog.ShowAsync();
        }

        // Prompt Instruction System Event Handlers

        private void SummaryInstructionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable/disable Save button based on text content
            var hasText = !string.IsNullOrWhiteSpace(SummaryInstructionTextBox.Text);
            SaveInstructionButton.IsEnabled = hasText;
        }

        private async void SearchInstructionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var searchDialog = new PromptSearchDialog(_promptService)
                {
                    XamlRoot = this.XamlRoot
                };

                var result = await searchDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary && searchDialog.SelectedPrompt != null)
                {
                    // Populate the instruction text box with selected prompt
                    SummaryInstructionTextBox.Text = searchDialog.SelectedPrompt.Instruction;
                    
                    // Increment usage count
                    await _promptService.IncrementUsageAsync(searchDialog.SelectedPrompt.Id);
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to open prompt search: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void SaveInstructionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var instruction = SummaryInstructionTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(instruction))
                {
                    return;
                }

                var saveDialog = new SavePromptDialog(_promptService, instruction)
                {
                    XamlRoot = this.XamlRoot
                };

                var result = await saveDialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary && saveDialog.SavedPrompt != null)
                {
                    // Show success message
                    var successDialog = new ContentDialog
                    {
                        Title = "Success",
                        Content = $"Prompt instruction '{saveDialog.SavedPrompt.Title}' saved successfully!",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to save prompt instruction: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }
}
