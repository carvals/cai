using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAI_design_1_chat.Models;
using CAI_design_1_chat.Services;

namespace CAI_design_1_chat.Presentation.Dialogs;

public sealed partial class SavePromptDialog : ContentDialog
{
    private readonly IPromptInstructionService _promptService;
    private readonly string _initialInstruction;

    public PromptInstruction? SavedPrompt { get; private set; }

    public SavePromptDialog(IPromptInstructionService promptService, string instruction)
    {
        this.InitializeComponent();
        _promptService = promptService;
        _initialInstruction = instruction;
        
        // Initialize form
        InstructionTextBox.Text = instruction;
        CreatedByTextBox.Text = Environment.UserName ?? "User";
        
        // Set default selections
        TypeComboBox.SelectedIndex = 0; // "Summary"
        LanguageComboBox.SelectedIndex = 0; // "French (fr)"
        
        // Disable save button initially
        IsPrimaryButtonEnabled = false;
        
        // Wire up the primary button click
        PrimaryButtonClick += OnSaveButtonClick;
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Focus on title field
        TitleTextBox.Focus(FocusState.Programmatic);
        ValidateForm();
    }

    private async void OnSaveButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Get deferral to allow async operation
        var deferral = args.GetDeferral();
        
        try
        {
            // Validate form
            if (!IsFormValid())
            {
                args.Cancel = true;
                return;
            }

            // Create prompt instruction
            var prompt = new PromptInstruction
            {
                Title = TitleTextBox.Text.Trim(),
                PromptType = GetSelectedPromptType(),
                Language = GetSelectedLanguage(),
                Description = string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? null : DescriptionTextBox.Text.Trim(),
                Instruction = InstructionTextBox.Text.Trim(),
                CreatedBy = CreatedByTextBox.Text.Trim(),
                IsSystem = IsSystemCheckBox.IsChecked == true
            };

            // Save to database
            SavedPrompt = await _promptService.SavePromptAsync(prompt);
        }
        catch (Exception ex)
        {
            // Show error message
            ValidationMessageTextBlock.Text = $"Error saving prompt: {ex.Message}";
            ValidationMessageTextBlock.Visibility = Visibility.Visible;
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ValidateForm(object? sender = null, object? e = null)
    {
        var isValid = IsFormValid();
        IsPrimaryButtonEnabled = isValid;
        
        if (isValid)
        {
            ValidationMessageTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            var errorMessage = GetValidationErrorMessage();
            if (!string.IsNullOrEmpty(errorMessage))
            {
                ValidationMessageTextBlock.Text = errorMessage;
                ValidationMessageTextBlock.Visibility = Visibility.Visible;
            }
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(TitleTextBox.Text) &&
               TypeComboBox.SelectedItem != null &&
               LanguageComboBox.SelectedItem != null &&
               !string.IsNullOrWhiteSpace(InstructionTextBox.Text);
    }

    private string GetValidationErrorMessage()
    {
        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            return "Title is required.";
        
        if (TypeComboBox.SelectedItem == null)
            return "Please select a prompt type.";
        
        if (LanguageComboBox.SelectedItem == null)
            return "Please select a language.";
        
        if (string.IsNullOrWhiteSpace(InstructionTextBox.Text))
            return "Instruction is required.";
        
        return string.Empty;
    }

    private string GetSelectedPromptType()
    {
        if (TypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            return selectedItem.Tag?.ToString() ?? "summary";
        }
        return "summary";
    }

    private string GetSelectedLanguage()
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            return selectedItem.Tag?.ToString() ?? "fr";
        }
        return "fr";
    }
}
