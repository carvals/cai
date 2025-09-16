using System;
using System.ComponentModel.DataAnnotations;

namespace CAI_design_1_chat.Models;

public class PromptInstruction
{
    public int Id { get; set; }

    [Required]
    public string PromptType { get; set; } = string.Empty; // 'summary', 'extraction', 'analysis', 'custom'

    [Required]
    public string Language { get; set; } = string.Empty; // 'fr', 'en', 'es', etc.

    [Required]
    public string Instruction { get; set; } = string.Empty;

    public string? Title { get; set; } // Display name for UI

    public string? Description { get; set; } // Longer description

    public bool IsSystem { get; set; } = false; // System vs user-created prompts

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int UsageCount { get; set; } = 0;

    // Constructor
    public PromptInstruction()
    {
    }

    public PromptInstruction(string promptType, string language, string instruction, string? title = null, string? description = null)
    {
        PromptType = promptType;
        Language = language;
        Instruction = instruction;
        Title = title;
        Description = description;
    }

    // Helper methods
    public string GetDisplayName()
    {
        return !string.IsNullOrEmpty(Title) ? Title : $"{PromptType} ({Language})";
    }

    public string GetUsageText()
    {
        return UsageCount switch
        {
            0 => "Never used",
            1 => "1 use",
            _ => $"{UsageCount} uses"
        };
    }

    public void IncrementUsage()
    {
        UsageCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    // Validation
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(PromptType) &&
               !string.IsNullOrWhiteSpace(Language) &&
               !string.IsNullOrWhiteSpace(Instruction);
    }

    // Static helper for prompt types
    public static readonly string[] ValidPromptTypes = { "summary", "extraction", "analysis", "custom" };
    public static readonly string[] ValidLanguages = { "fr", "en", "es", "de", "it", "pt", "nl", "ru", "ja", "zh" };

    public static bool IsValidPromptType(string promptType)
    {
        return Array.Exists(ValidPromptTypes, t => t.Equals(promptType, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidLanguage(string language)
    {
        return Array.Exists(ValidLanguages, l => l.Equals(language, StringComparison.OrdinalIgnoreCase));
    }
}
