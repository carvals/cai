using System;

namespace CAI_design_1_chat.Services
{
    /// <summary>
    /// Event arguments for context change notifications
    /// </summary>
    public class ContextChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The session ID where the context changed
        /// </summary>
        public int SessionId { get; set; }

        /// <summary>
        /// Type of change that occurred
        /// </summary>
        public string ChangeType { get; set; } = "Unknown";

        /// <summary>
        /// File ID if the change is related to a specific file
        /// </summary>
        public int? FileId { get; set; }

        /// <summary>
        /// Additional details about the change
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Timestamp when the change occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public ContextChangedEventArgs(int sessionId, string changeType, int? fileId = null, string? details = null)
        {
            SessionId = sessionId;
            ChangeType = changeType;
            FileId = fileId;
            Details = details;
        }
    }

    /// <summary>
    /// Constants for context change types
    /// </summary>
    public static class ContextChangeTypes
    {
        public const string FileExcluded = "FileExcluded";
        public const string FileIncluded = "FileIncluded";
        public const string FileDeleted = "FileDeleted";
        public const string FileRenamed = "FileRenamed";
        public const string SummaryToggled = "SummaryToggled";
        public const string ManualRefresh = "ManualRefresh";
        public const string FileAdded = "FileAdded";
        public const string MessageAdded = "MessageAdded";
        public const string SessionCleared = "SessionCleared";
    }
}
