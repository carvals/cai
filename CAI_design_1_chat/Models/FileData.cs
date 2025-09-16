using System;

namespace CAI_design_1_chat.Models
{
    public class FileData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? Summary { get; set; }
        public string? Owner { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public string? FileType { get; set; }
        public long FileSize { get; set; }
        public string ProcessingStatus { get; set; } = "pending";
        public string? ExtractionMethod { get; set; }
        public string? OriginalFilePath { get; set; }
        
        // Context management fields
        public bool IsInContext { get; set; } = false;
        public bool UseSummaryInContext { get; set; } = false;
        public int ContextOrder { get; set; } = 0;
        public bool IsExcludedTemporarily { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
