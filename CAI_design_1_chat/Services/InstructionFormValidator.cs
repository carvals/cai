using CAI_design_1_chat.Models;
using System;
using System.Collections.Generic;

namespace CAI_design_1_chat.Services
{
    public class InstructionFormValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
            public InstructionShortcut? ValidatedShortcut { get; set; }
        }

        public ValidationResult ValidateForm(
            string name,
            string shortcut,
            string language,
            string promptType,
            string description,
            string instruction,
            bool isActive,
            InstructionShortcut? existingShortcut = null)
        {
            var result = new ValidationResult();
            var errors = new List<string>();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Name is required");
            }

            if (string.IsNullOrWhiteSpace(instruction))
            {
                errors.Add("Instruction is required");
            }

            // Validate shortcut format (if provided)
            var trimmedShortcut = shortcut?.Trim();
            if (!string.IsNullOrEmpty(trimmedShortcut))
            {
                if (trimmedShortcut.Contains(' '))
                {
                    errors.Add("Shortcut cannot contain spaces");
                }

                if (trimmedShortcut.Contains('_'))
                {
                    errors.Add("Shortcut cannot contain underscores");
                }

                if (trimmedShortcut.Length > 20)
                {
                    errors.Add("Shortcut cannot be longer than 20 characters");
                }
            }

            // Validate instruction length
            if (!string.IsNullOrEmpty(instruction) && instruction.Length > 500)
            {
                errors.Add("Instruction cannot be longer than 500 characters");
            }

            // If validation passed, create the shortcut object
            if (errors.Count == 0)
            {
                var instructionShortcut = existingShortcut ?? new InstructionShortcut();
                
                instructionShortcut.Title = name.Trim();
                instructionShortcut.Shortcut = string.IsNullOrWhiteSpace(trimmedShortcut) ? null : trimmedShortcut;
                instructionShortcut.Language = language?.Trim() ?? "";
                instructionShortcut.PromptType = promptType?.Trim() ?? "";
                instructionShortcut.Description = description?.Trim() ?? "";
                instructionShortcut.Instruction = instruction.Trim();
                instructionShortcut.IsActive = isActive;

                result.IsValid = true;
                result.ValidatedShortcut = instructionShortcut;
            }
            else
            {
                result.IsValid = false;
                result.Errors = errors;
            }

            return result;
        }
    }
}
