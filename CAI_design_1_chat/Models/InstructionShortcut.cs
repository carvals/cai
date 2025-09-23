using System;

namespace CAI_design_1_chat.Models
{
    public class InstructionShortcut
    {
        public int Id { get; set; }
        public string PromptType { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Shortcut { get; set; }
        public bool IsSystem { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public int UsageCount { get; set; } = 0;
    }
}
