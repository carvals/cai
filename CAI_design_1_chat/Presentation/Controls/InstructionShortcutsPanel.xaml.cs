using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Microsoft.UI;
using CAI_design_1_chat.Models;
using CAI_design_1_chat.Services;

namespace CAI_design_1_chat.Presentation.Controls
{
    public sealed partial class InstructionShortcutsPanel : UserControl
    {
        private InstructionShortcutService? _shortcutService;
        private List<InstructionShortcut> _allShortcuts = new();
        private List<InstructionShortcut> _filteredShortcuts = new();
        private bool _showDeleted = false;

        // Event to notify when user wants to edit a shortcut
        public event EventHandler<InstructionShortcut>? EditShortcutRequested;
        public event EventHandler? AddNewShortcutRequested;

        public InstructionShortcutsPanel()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Initialize the panel with the shortcut service
        /// </summary>
        public async Task InitializeAsync(InstructionShortcutService shortcutService)
        {
            _shortcutService = shortcutService;
            await LoadShortcutsAsync();
            await LoadCategoryFilterAsync();
        }

        /// <summary>
        /// Load all shortcuts and refresh the display
        /// </summary>
        public async Task LoadShortcutsAsync()
        {
            if (_shortcutService == null) return;

            try
            {
                _allShortcuts = await _shortcutService.GetAllShortcutsAsync(_showDeleted);
                await ApplyFiltersAsync();
                Console.WriteLine($"InstructionShortcutsPanel: Loaded {_allShortcuts.Count} shortcuts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading shortcuts: {ex.Message}");
            }
        }

        private async Task LoadCategoryFilterAsync()
        {
            if (_shortcutService == null) return;

            try
            {
                var categories = await _shortcutService.GetDistinctPromptTypesAsync();
                
                CategoryFilterComboBox.Items.Clear();
                CategoryFilterComboBox.Items.Add("All Categories");
                
                foreach (var category in categories)
                {
                    CategoryFilterComboBox.Items.Add(category);
                }
                
                CategoryFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading categories: {ex.Message}");
            }
        }

        private async Task ApplyFiltersAsync()
        {
            _filteredShortcuts = _allShortcuts.ToList();

            // Apply category filter
            var selectedCategory = CategoryFilterComboBox.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedCategory) && selectedCategory != "All Categories")
            {
                _filteredShortcuts = _filteredShortcuts
                    .Where(s => string.Equals(s.PromptType, selectedCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Apply active/deleted filter
            if (!_showDeleted)
            {
                _filteredShortcuts = _filteredShortcuts.Where(s => s.IsActive).ToList();
            }

            RefreshShortcutsDisplay();
        }

        private void RefreshShortcutsDisplay()
        {
            ShortcutsContainer.Children.Clear();

            foreach (var shortcut in _filteredShortcuts)
            {
                var shortcutRow = CreateShortcutRow(shortcut);
                ShortcutsContainer.Children.Add(shortcutRow);
            }

            Console.WriteLine($"InstructionShortcutsPanel: Displaying {_filteredShortcuts.Count} shortcuts");
        }

        private Border CreateShortcutRow(InstructionShortcut shortcut)
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

            // Add click handler - this will trigger the overlay
            border.Tapped += (s, e) => EditShortcutRequested?.Invoke(this, shortcut);
            border.DoubleTapped += (s, e) => EditShortcutRequested?.Invoke(this, shortcut);

            return border;
        }

        // Event Handlers
        private void AddNewShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewShortcutRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ApplyFiltersAsync();
        }

        private async void ViewDeletedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _showDeleted = ViewDeletedCheckBox.IsChecked == true;
            await LoadShortcutsAsync();
        }
    }
}
