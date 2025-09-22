using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CAI_design_1_chat.Services
{
    /// <summary>
    /// Service to parse JSON context into structured ContextData for AI providers
    /// </summary>
    public class ContextParserService
    {
        private readonly ContextCacheService _contextCacheService;

        public ContextParserService(ContextCacheService contextCacheService)
        {
            _contextCacheService = contextCacheService;
        }

        /// <summary>
        /// Gets structured context data for a session
        /// </summary>
        public async Task<ContextData> GetContextDataAsync(int sessionId)
        {
            try
            {
                // Get JSON context from cache service
                var jsonContext = await _contextCacheService.GetContextAsync(sessionId);
                
                // Parse JSON into structured ContextData
                var contextData = ParseJsonContext(jsonContext);
                
                Console.WriteLine($"Context parsed for session {sessionId}:");
                Console.WriteLine($"  - Files: {contextData.FileCount}");
                Console.WriteLine($"  - Messages: {contextData.MessageCount}");
                Console.WriteLine($"  - Total tokens: ~{contextData.TotalTokens}");
                
                return contextData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing context for session {sessionId}: {ex.Message}");
                return ContextData.Empty;
            }
        }

        /// <summary>
        /// Parses JSON context string into structured ContextData
        /// </summary>
        private ContextData ParseJsonContext(string jsonContext)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonContext);
                var root = document.RootElement;

                var contextData = new ContextData();

                // Parse assistant role
                if (root.TryGetProperty("assistantRole", out var assistantRoleElement) ||
                    root.TryGetProperty("assistant_role", out assistantRoleElement))
                {
                    if (assistantRoleElement.TryGetProperty("description", out var descElement))
                    {
                        contextData.AssistantRole = descElement.GetString() ?? "You are a helpful assistant.";
                    }
                    
                    if (assistantRoleElement.TryGetProperty("contextDate", out var dateElement) ||
                        assistantRoleElement.TryGetProperty("context_date", out dateElement))
                    {
                        if (DateTime.TryParse(dateElement.GetString(), out var contextDate))
                        {
                            contextData.ContextDate = contextDate;
                        }
                    }
                }

                // Parse file context
                if (root.TryGetProperty("fileContext", out var fileContextElement) ||
                    root.TryGetProperty("file_context", out fileContextElement))
                {
                    if (fileContextElement.TryGetProperty("files", out var filesElement))
                    {
                        contextData.Files = ParseFiles(filesElement);
                    }
                }

                // Parse message history
                if (root.TryGetProperty("messageHistory", out var messageHistoryElement) ||
                    root.TryGetProperty("message_history", out messageHistoryElement))
                {
                    if (messageHistoryElement.TryGetProperty("messages", out var messagesElement))
                    {
                        contextData.MessageHistory = ParseMessages(messagesElement);
                    }
                }

                return contextData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing JSON context: {ex.Message}");
                return ContextData.Empty;
            }
        }

        /// <summary>
        /// Parses files array from JSON
        /// </summary>
        private List<FileContext> ParseFiles(JsonElement filesElement)
        {
            var files = new List<FileContext>();

            if (filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileElement in filesElement.EnumerateArray())
                {
                    var fileContext = new FileContext();

                    if (fileElement.TryGetProperty("displayName", out var displayNameElement) ||
                        fileElement.TryGetProperty("display_name", out displayNameElement))
                    {
                        fileContext.DisplayName = displayNameElement.GetString() ?? "";
                    }

                    if (fileElement.TryGetProperty("originalName", out var originalNameElement) ||
                        fileElement.TryGetProperty("original_name", out originalNameElement))
                    {
                        fileContext.OriginalName = originalNameElement.GetString() ?? "";
                    }

                    if (fileElement.TryGetProperty("content", out var contentElement))
                    {
                        fileContext.Content = contentElement.GetString() ?? "";
                    }

                    if (fileElement.TryGetProperty("useSummary", out var useSummaryElement) ||
                        fileElement.TryGetProperty("use_summary", out useSummaryElement))
                    {
                        fileContext.UseSummary = useSummaryElement.GetBoolean();
                    }

                    if (fileElement.TryGetProperty("orderIndex", out var orderIndexElement) ||
                        fileElement.TryGetProperty("order_index", out orderIndexElement))
                    {
                        fileContext.OrderIndex = orderIndexElement.GetInt32();
                    }

                    // Files in the JSON are already filtered (excluded files are not included)
                    fileContext.IsExcluded = false;

                    files.Add(fileContext);
                }
            }

            return files.OrderBy(f => f.OrderIndex).ToList();
        }

        /// <summary>
        /// Parses messages array from JSON
        /// </summary>
        private List<ChatMessage> ParseMessages(JsonElement messagesElement)
        {
            var messages = new List<ChatMessage>();

            if (messagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var messageElement in messagesElement.EnumerateArray())
                {
                    string role = "";
                    string content = "";
                    DateTime timestamp = DateTime.Now;

                    if (messageElement.TryGetProperty("role", out var roleElement))
                    {
                        role = roleElement.GetString() ?? "";
                    }

                    if (messageElement.TryGetProperty("content", out var contentElement))
                    {
                        content = contentElement.GetString() ?? "";
                    }

                    if (messageElement.TryGetProperty("timestamp", out var timestampElement))
                    {
                        if (DateTime.TryParse(timestampElement.GetString(), out var parsedTimestamp))
                        {
                            timestamp = parsedTimestamp;
                        }
                    }

                    var chatMessage = new ChatMessage(role, content)
                    {
                        Timestamp = timestamp
                    };

                    messages.Add(chatMessage);
                }
            }

            return messages;
        }
    }
}
