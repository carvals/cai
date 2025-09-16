using System;

namespace CAI_design_1_chat.Models
{
    public class ProcessingJob
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string JobType { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string? Parameters { get; set; }
        public string? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int Priority { get; set; } = 0;
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        
        // Navigation property
        public FileData? FileData { get; set; }
        
        public bool IsCompleted => Status == "completed";
        public bool IsFailed => Status == "failed";
        public bool IsRunning => Status == "running";
        public bool IsPending => Status == "pending";
        
        public TimeSpan? ProcessingDuration
        {
            get
            {
                if (StartedAt.HasValue && CompletedAt.HasValue)
                    return CompletedAt.Value - StartedAt.Value;
                return null;
            }
        }
    }
}
