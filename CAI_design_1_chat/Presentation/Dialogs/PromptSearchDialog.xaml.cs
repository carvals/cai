using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAI_design_1_chat.Models;
using CAI_design_1_chat.Services;

namespace CAI_design_1_chat.Presentation.Dialogs;

public sealed partial class PromptSearchDialog : ContentDialog
{
    private readonly IPromptInstructionService _promptService;
    private List<PromptInstruction> _allPrompts = new();
    private PromptInstruction? _selectedPrompt;

    public PromptInstruction? SelectedPrompt => _selectedPrompt;

    public PromptSearchDialog(IPromptInstructionService promptService)
    {
        this.InitializeComponent();
        _promptService = promptService;
        
        // Set default selection
        PromptTypeComboBox.SelectedIndex = 0; // "All Types"
        
        // Disable primary button initially
        IsPrimaryButtonEnabled = false;
        
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadPromptsAsync();
    }

    private async Task LoadPromptsAsync()
    {
        try
        {
            var searchTerm = string.IsNullOrWhiteSpace(SearchTextBox.Text) ? null : SearchTextBox.Text.Trim();
            var selectedType = GetSelectedPromptType();
            
            _allPrompts = await _promptService.SearchPromptsAsync(searchTerm, selectedType);
            
            // Update UI with results
            PromptsListView.ItemsSource = _allPrompts.Select(p => new PromptDisplayModel(p)).ToList();
            
            // Clear selection and preview
            PromptsListView.SelectedItem = null;
            PreviewTextBlock.Text = _allPrompts.Any() 
                ? "Select a prompt to preview its instruction..." 
                : "No prompts found matching your criteria.";
            
            IsPrimaryButtonEnabled = false;
        }
        catch (Exception ex)
        {
            PreviewTextBlock.Text = $"Error loading prompts: {ex.Message}";
        }
    }

    private string? GetSelectedPromptType()
    {
        if (PromptTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            return tag == "all" ? null : tag;
        }
        return null;
    }

    private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce search to avoid too many database calls
        await Task.Delay(300);
        if (SearchTextBox.Text == ((TextBox)sender).Text) // Only search if text hasn't changed
        {
            await LoadPromptsAsync();
        }
    }

    private async void PromptTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadPromptsAsync();
    }

    private void PromptsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PromptsListView.SelectedItem is PromptDisplayModel selectedModel)
        {
            _selectedPrompt = _allPrompts.FirstOrDefault(p => p.Id == selectedModel.Id);
            PreviewTextBlock.Text = _selectedPrompt?.Instruction ?? "No instruction available.";
            IsPrimaryButtonEnabled = true;
        }
        else
        {
            _selectedPrompt = null;
            PreviewTextBlock.Text = "Select a prompt to preview its instruction...";
            IsPrimaryButtonEnabled = false;
        }
    }

    // Display model for ListView binding
    public class PromptDisplayModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PromptType { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string UsageText { get; set; } = string.Empty;
        public bool IsSystem { get; set; }

        public PromptDisplayModel(PromptInstruction prompt)
        {
            Id = prompt.Id;
            Title = prompt.GetDisplayName();
            Description = prompt.Description ?? "No description";
            PromptType = prompt.PromptType;
            Language = prompt.Language.ToUpper();
            UsageText = prompt.GetUsageText();
            IsSystem = prompt.IsSystem;
        }
    }
}
