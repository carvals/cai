using System;
using System.Collections.Generic;
using System.Linq;

namespace CAI_design_1_chat.Services
{
    /// <summary>
    /// Structured context data for AI providers containing file context and message history
    /// </summary>
    public class ContextData
    {
        /// <summary>
        /// Previous messages in the conversation
        /// </summary>
        public List<ChatMessage> MessageHistory { get; set; } = new();

        /// <summary>
        /// File context information
        /// </summary>
        public List<FileContext> Files { get; set; } = new();

        /// <summary>
        /// Assistant role description
        /// </summary>
        public string AssistantRole { get; set; } = "You are a helpful assistant.";

        /// <summary>
        /// Context generation timestamp
        /// </summary>
        public DateTime ContextDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Total estimated token count for the complete context
        /// </summary>
        public int TotalTokens => EstimateTokens();

        /// <summary>
        /// Number of files in context (excluding excluded files)
        /// </summary>
        public int FileCount => Files?.Count(f => !f.IsExcluded) ?? 0;

        /// <summary>
        /// Number of messages in context
        /// </summary>
        public int MessageCount => MessageHistory?.Count ?? 0;

        /// <summary>
        /// Total character count of all file content
        /// </summary>
        public int TotalCharacters => Files?.Where(f => !f.IsExcluded).Sum(f => f.Content?.Length ?? 0) ?? 0;

        /// <summary>
        /// Estimates total token count for the complete context
        /// </summary>
        private int EstimateTokens()
        {
            var messageTokens = ChatMessage.EstimateTokens(MessageHistory);
            var fileTokens = Files?.Where(f => !f.IsExcluded).Sum(f => f.EstimateTokens()) ?? 0;
            var roleTokens = ChatMessage.EstimateTokens(AssistantRole);
            
            return messageTokens + fileTokens + roleTokens;
        }

        /// <summary>
        /// Creates an empty context data instance
        /// </summary>
        public static ContextData Empty => new ContextData();

        /// <summary>
        /// Creates context data with only message history (for backward compatibility)
        /// </summary>
        public static ContextData FromMessages(List<ChatMessage> messages, string assistantRole = "You are a helpful assistant.")
        {
            return new ContextData
            {
                MessageHistory = messages ?? new List<ChatMessage>(),
                AssistantRole = assistantRole,
                Files = new List<FileContext>()
            };
        }
    }

    /// <summary>
    /// File context information for AI providers
    /// </summary>
    public class FileContext
    {
        /// <summary>
        /// Display name of the file (user-customizable)
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Original filename
        /// </summary>
        public string OriginalName { get; set; } = "";

        /// <summary>
        /// File content (or summary if UseSummary is true)
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Whether to use summary instead of full content
        /// </summary>
        public bool UseSummary { get; set; }

        /// <summary>
        /// Whether this file is excluded from context
        /// </summary>
        public bool IsExcluded { get; set; }

        /// <summary>
        /// Order index for file presentation
        /// </summary>
        public int OrderIndex { get; set; }

        /// <summary>
        /// Character count of the content
        /// </summary>
        public int CharacterCount => Content?.Length ?? 0;

        /// <summary>
        /// Estimates token count for this file's content
        /// </summary>
        public int EstimateTokens()
        {
            if (IsExcluded || string.IsNullOrEmpty(Content)) return 0;
            
            // File content + metadata overhead (filename, structure)
            var contentTokens = ChatMessage.EstimateTokens(Content);
            var metadataTokens = ChatMessage.EstimateTokens($"File: {DisplayName}") + 10; // Structure overhead
            
            return contentTokens + metadataTokens;
        }
    }
}
