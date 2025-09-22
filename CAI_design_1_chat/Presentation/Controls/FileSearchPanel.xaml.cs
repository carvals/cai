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
            
            // TODO: Update content preview based on toggle state
            if (showSummary)
            {
                ContentPreview.Text = "Summary view will be implemented when file is selected...";
            }
            else
            {
                ContentPreview.Text = "Raw text view will be implemented when file is selected...";
            }
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
                        FontSize = 14,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 32)
                    };
                    ResultsContainer.Children.Add(emptyText);
                    return;
                }

                foreach (var file in results)
                {
                    var resultCard = CreateFileResultCard(file);
                    ResultsContainer.Children.Add(resultCard);
                }

                Console.WriteLine($"FileSearchPanel: Displayed {results.Count} search results");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchPanel: Error displaying search results: {ex.Message}");
            }
        }

        private Border CreateFileResultCard(FileSearchResult file)
        {
            var card = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 2),
                Tag = file
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Date
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Size
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80, GridUnitType.Pixel) }); // Action

            // File name
            var nameText = new TextBlock
            {
                Text = file.DisplayFileName,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameText, 0);

            // Date
            var dateText = new TextBlock
            {
                Text = file.FormattedDate,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dateText, 1);

            // Size
            var sizeText = new TextBlock
            {
                Text = file.FormattedSize,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sizeText, 2);

            // Add to Context button
            var addButton = new Button
            {
                Content = file.InContext ? "✓ Added" : "+ Add",
                FontSize = 12,
                Padding = new Thickness(8, 4),
                Background = file.InContext ? 
                    (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemAccentColorLight2"] :
                    (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentButtonBackground"],
                IsEnabled = !file.InContext,
                Tag = file
            };
            addButton.Click += AddToContextButton_Click;
            Grid.SetColumn(addButton, 3);

            grid.Children.Add(nameText);
            grid.Children.Add(dateText);
            grid.Children.Add(sizeText);
            grid.Children.Add(addButton);

            card.Child = grid;

            // Add click handler for row selection
            card.Tapped += (sender, e) => SelectFile(file, card);

            return card;
        }

        private void SelectFile(FileSearchResult file, Border card)
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
                
                // Visual feedback - highlight selected card
                foreach (Border child in ResultsContainer.Children.OfType<Border>())
                {
                    child.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                }
                card.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"];
                
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
                    button.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemAccentColorLight2"];
                    
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
    }
}
