using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CAI_design_1_chat.Services;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Presentation.Controls
{
    public sealed partial class FileSearchPanel : UserControl
    {
        public event EventHandler? BackRequested;

        private FileSearchService? _fileSearchService;
        private DatabaseService? _databaseService;
        private List<FileSearchResult> _currentResults = new();
        private FileSearchResult? _selectedFile;
        private int _currentSessionId = 1; // Default session, will be updated

        public FileSearchPanel()
        {
            this.InitializeComponent();
            InitializeServices();
        }

        private void InitializeServices()
        {
            try
            {
                _databaseService = new DatabaseService();
                _fileSearchService = new FileSearchService(_databaseService);
                Console.WriteLine("FileSearchPanel: Services initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error initializing services: {ex.Message}");
            }
        }

        public void SetCurrentSession(int sessionId)
        {
            _currentSessionId = sessionId;
            Console.WriteLine($"FileSearchPanel: Current session set to {sessionId}");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("FileSearchPanel: Back button clicked");
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var searchText = textBox?.Text ?? string.Empty;
            
            // Enable search button only if we have 3+ characters
            SearchButton.IsEnabled = searchText.Length >= 3;
            
            Console.WriteLine($"FileSearchPanel: Search text changed to '{searchText}' (length: {searchText.Length})");
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var searchText = SearchTextBox.Text?.Trim() ?? string.Empty;
            
            if (searchText.Length < 3)
            {
                Console.WriteLine("FileSearchPanel: Search text too short, minimum 3 characters required");
                return;
            }

            if (_fileSearchService == null)
            {
                Console.WriteLine("FileSearchPanel: FileSearchService not initialized");
                return;
            }

            Console.WriteLine($"FileSearchPanel: Performing search for '{searchText}'");
            
            try
            {
                // Show loading state
                StatusMessage.Text = $"Searching for '{searchText}'...";
                StatusMessage.Visibility = Visibility.Visible;
                SearchButton.IsEnabled = false;
                
                // Clear previous results
                ResultsContainer.Children.Clear();
                _currentResults.Clear();
                _selectedFile = null;
                
                // Perform search
                var results = await _fileSearchService.SearchFilesAsync(searchText, _currentSessionId);
                _currentResults = results;
                
                // Display results
                await DisplaySearchResults(results);
                
                // Update status
                if (results.Count == 0)
                {
                    StatusMessage.Text = "No files found. Try different search terms.";
                }
                else if (results.Count == 50)
                {
                    StatusMessage.Text = "Showing first 50 results, refine search for more specific results.";
                }
                else
                {
                    StatusMessage.Text = $"Found {results.Count} file(s).";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error during search: {ex.Message}");
                StatusMessage.Text = $"Search error: {ex.Message}";
                
                // Show error message in results area
                ResultsContainer.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = $"Search failed: {ex.Message}",
                    FontSize = 14,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemErrorTextColor"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 32)
                };
                ResultsContainer.Children.Add(errorText);
            }
            finally
            {
                SearchButton.IsEnabled = true;
            }
        }

        private void ContentToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            var showSummary = toggle?.IsOn ?? false;
            
            Console.WriteLine($"FileSearchPanel: Content toggle changed to {(showSummary ? "Summary" : "Raw Text")}");
            
            // Update content preview immediately when toggle changes
            UpdateContentPreview();
        }


        private async Task DisplaySearchResults(List<FileSearchResult> results)
        {
            try
            {
                ResultsContainer.Children.Clear();

                if (results.Count == 0)
                {
                    var emptyText = new TextBlock
                    {
                        Text = "No files found matching your search.",
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Courier New, monospace"),
                        FontSize = 12,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 32)
                    };
                    ResultsContainer.Children.Add(emptyText);
                    return;
                }

                foreach (var file in results)
                {
                    var resultRow = CreateFileResultRow(file);
                    ResultsContainer.Children.Add(resultRow);
                }

                Console.WriteLine($"FileSearchPanel: Displayed {results.Count} search results");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error displaying search results: {ex.Message}");
            }
        }

        private Border CreateFileResultRow(FileSearchResult file)
        {
            var isSelected = _selectedFile?.Id == file.Id;
            var isInContext = file.InContext;
            
            // Purple color matching the back button (from XAML: #19FFFFFF with purple tint)
            var purpleHover = Microsoft.UI.Colors.Purple; // Purple with transparency
            var purpleSelected = Microsoft.UI.Colors.DarkMagenta; // Darker purple for selection
            
            var row = new Border
            {
                Background = isSelected ? 
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(purpleSelected) :
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 6),
                Tag = file,
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); // Name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Date
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Size

            // File name with status indicator and 2-char margin
            var nameText = new TextBlock
            {
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            // Set text and color based on state - NO green for selection, only for context
            if (isInContext)
            {
                nameText.Text = $"  ✓ {file.DisplayFileName}";
                nameText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            }
            else if (isSelected)
            {
                nameText.Text = $"  ► {file.DisplayFileName}";
                nameText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            }
            else
            {
                nameText.Text = $"    {file.DisplayFileName}";
                nameText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            }
            
            Grid.SetColumn(nameText, 0);

            // Date - centered
            var dateText = new TextBlock
            {
                Text = file.FormattedDate,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(dateText, 1);

            // Column separator for date
            var separator1 = new Border
            {
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            Grid.SetColumn(separator1, 1);

            // Size - centered
            var sizeText = new TextBlock
            {
                Text = file.FormattedSize,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(sizeText, 2);

            // Column separator for size
            var separator2 = new Border
            {
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            Grid.SetColumn(separator2, 2);

            grid.Children.Add(nameText);
            grid.Children.Add(separator1);
            grid.Children.Add(dateText);
            grid.Children.Add(separator2);
            grid.Children.Add(sizeText);

            row.Child = grid;

            // Add hover effect with purple color
            row.PointerEntered += (sender, e) =>
            {
                if (!isSelected)
                {
                    row.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(purpleHover);
                }
            };

            row.PointerExited += (sender, e) =>
            {
                if (!isSelected)
                {
                    row.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            };

            // Add click handler for row selection (NOT context adding)
            row.Tapped += (sender, e) => SelectFileOnly(file);

            return row;
        }

        private void SelectFile(FileSearchResult file, Border row)
        {
            try
            {
                _selectedFile = file;
                
                // Update preview content
                UpdateContentPreview();
                
                // Enable Add to Context button in viewer
                AddToContextButton.IsEnabled = !file.InContext;
                
                // Update viewer title
                ViewerTitle.Text = $"Preview: {file.DisplayFileName}";
                
                Console.WriteLine($"FileSearchPanel: Selected file '{file.DisplayFileName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error selecting file: {ex.Message}");
            }
        }

        private void UpdateContentPreview()
        {
            if (_selectedFile == null)
            {
                ContentPreview.Text = "Select a file to preview its content...";
                return;
            }

            var showSummary = ContentToggle.IsOn;
            
            if (showSummary && !string.IsNullOrEmpty(_selectedFile.Summary))
            {
                ContentPreview.Text = _selectedFile.Summary;
            }
            else if (!string.IsNullOrEmpty(_selectedFile.Content))
            {
                ContentPreview.Text = _selectedFile.Content;
            }
            else
            {
                ContentPreview.Text = "No content available for this file.";
            }
        }

        private async void AddToContextButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var file = button?.Tag as FileSearchResult;
            
            if (file == null || _fileSearchService == null)
            {
                Console.WriteLine("FileSearchPanel: Cannot add to context - file or service is null");
                return;
            }

            try
            {
                button.IsEnabled = false;
                button.Content = "Adding...";
                
                var success = await _fileSearchService.AddFileToContextAsync(file.Id, _currentSessionId);
                
                if (success)
                {
                    file.InContext = true;
                    button.Content = "✓ Added";
                    button.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    
                    // Update main Add to Context button if this is the selected file
                    if (_selectedFile?.Id == file.Id)
                    {
                        AddToContextButton.IsEnabled = false;
                    }
                    
                    Console.WriteLine($"FileSearchPanel: Successfully added file '{file.DisplayFileName}' to context");
                }
                else
                {
                    button.Content = "Already Added";
                    button.IsEnabled = false;
                    Console.WriteLine($"FileSearchPanel: File '{file.DisplayFileName}' was already in context");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error adding file to context: {ex.Message}");
                button.Content = "Error";
                button.IsEnabled = true;
            }
        }

        private async void SelectFileOnly(FileSearchResult file)
        {
            try
            {
                // Just select the file, don't add to context
                _selectedFile = file;
                UpdateContentPreview();
                
                // Refresh display to show purple selection
                await DisplaySearchResults(_currentResults);
                
                Console.WriteLine($"FileSearchPanel: Selected file '{file.DisplayFileName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error selecting file: {ex.Message}");
            }
        }

        private async void SearchTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            try
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    // Trigger search on Enter key
                    var textBox = sender as TextBox;
                    if (textBox?.Text?.Length >= 3)
                    {
                        SearchButton_Click(null, null);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Down || e.Key == Windows.System.VirtualKey.Up)
                {
                    // Handle keyboard navigation
                    HandleKeyboardNavigation(e.Key == Windows.System.VirtualKey.Down);
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    // Clear selection
                    _selectedFile = null;
                    await DisplaySearchResults(_currentResults);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error handling key down: {ex.Message}");
            }
        }

        private void HandleKeyboardNavigation(bool moveDown)
        {
            try
            {
                if (_currentResults == null || _currentResults.Count == 0) return;

                int currentIndex = -1;
                if (_selectedFile != null)
                {
                    currentIndex = _currentResults.FindIndex(f => f.Id == _selectedFile.Id);
                }

                if (moveDown)
                {
                    currentIndex = (currentIndex + 1) % _currentResults.Count;
                }
                else
                {
                    currentIndex = currentIndex <= 0 ? _currentResults.Count - 1 : currentIndex - 1;
                }

                if (currentIndex >= 0 && currentIndex < _currentResults.Count)
                {
                    SelectFileOnly(_currentResults[currentIndex]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error handling keyboard navigation: {ex.Message}");
            }
        }

        private async void ToggleFileContext(FileSearchResult file)
        {
            try
            {
                // First, select the file for preview
                _selectedFile = file;
                UpdateContentPreview();
                
                // If not in context, add it (visual only for now)
                if (!file.InContext)
                {
                    file.InContext = true;
                    
                    // Refresh the display to show green filename
                    await DisplaySearchResults(_currentResults);
                    
                    Console.WriteLine($"FileSearchPanel: Added file '{file.DisplayFileName}' to context (visual only)");
                }
                else
                {
                    Console.WriteLine($"FileSearchPanel: File '{file.DisplayFileName}' already in context");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error toggling file context: {ex.Message}");
            }
        }
    }
}
