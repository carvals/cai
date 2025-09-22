using System;

namespace CAI_design_1_chat.Models
{
    public class FileSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileType { get; set; } = string.Empty;
        public bool InContext { get; set; }

        // Computed properties for display
        public string FormattedSize => FormatFileSize(FileSize);
        public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd");
        public string DisplayFileName => !string.IsNullOrEmpty(DisplayName) ? DisplayName : Name;

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}
