using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CAI_design_1_chat.Services
{
    public class ChatContextService
    {
        private readonly Dictionary<int, List<ChatMessage>> _sessionCache = new();
        private readonly DatabaseService _databaseService;
        
        private const int MAX_MEMORY_MESSAGES = 15; // Keep more in memory than we send
        private const int CONTEXT_MESSAGES = 10;   // Send fewer to AI
        
        public ChatContextService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }
        
        /// <summary>
        /// Get optimized context for AI requests (hybrid: memory + database)
        /// </summary>
        public async Task<List<ChatMessage>> GetContextForAIAsync(int sessionId)
        {
            try
            {
                // Load from cache or database
                if (!_sessionCache.ContainsKey(sessionId))
                {
                    Console.WriteLine($"Loading chat history from database for session {sessionId}");
                    var messages = await LoadMessagesFromDatabase(sessionId);
                    _sessionCache[sessionId] = messages;
                }
                else
                {
                    Console.WriteLine($"Using cached chat history for session {sessionId}");
                }
                
                // Return last messages for AI context
                var contextMessages = _sessionCache[sessionId].TakeLast(CONTEXT_MESSAGES).ToList();
                Console.WriteLine($"Providing {contextMessages.Count} messages as context to AI");
                
                return contextMessages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting context for AI: {ex.Message}");
                return new List<ChatMessage>(); // Return empty context on error
            }
        }
        
        /// <summary>
        /// Add a new message to both cache and database
        /// </summary>
        public async Task AddMessageAsync(int sessionId, string role, string content)
        {
            try
            {
                // 1. Save to database (persistence)
                await _databaseService.SaveChatMessageAsync(sessionId, role, content);
                
                // 2. Add to memory cache (performance)
                var message = new ChatMessage(role, content);
                AddMessageToCache(sessionId, message);
                
                Console.WriteLine($"Message added: {role} (session {sessionId})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Add message to memory cache only (for loading from database)
        /// </summary>
        public void AddMessageToCache(int sessionId, ChatMessage message)
        {
            if (!_sessionCache.ContainsKey(sessionId))
                _sessionCache[sessionId] = new List<ChatMessage>();
                
            _sessionCache[sessionId].Add(message);
            
            // Keep only recent messages in memory to prevent memory bloat
            if (_sessionCache[sessionId].Count > MAX_MEMORY_MESSAGES)
            {
                _sessionCache[sessionId] = _sessionCache[sessionId].TakeLast(MAX_MEMORY_MESSAGES).ToList();
                Console.WriteLine($"Trimmed cache for session {sessionId} to {MAX_MEMORY_MESSAGES} messages");
            }
        }
        
        /// <summary>
        /// Clear session cache (useful for "Clear Session" functionality)
        /// </summary>
        public void ClearSessionCache(int sessionId)
        {
            _sessionCache.Remove(sessionId);
            Console.WriteLine($"Cleared cache for session {sessionId}");
        }
        
        /// <summary>
        /// Load messages from database and convert to ChatMessage format
        /// </summary>
        private async Task<List<ChatMessage>> LoadMessagesFromDatabase(int sessionId)
        {
            var messages = new List<ChatMessage>();
            
            try
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_databaseService.GetConnectionString());
                await connection.OpenAsync();
                
                using var command = new Microsoft.Data.Sqlite.SqliteCommand(
                    @"SELECT message_type, content, timestamp 
                      FROM chat_messages 
                      WHERE session_id = @sessionId 
                      ORDER BY timestamp ASC 
                      LIMIT @limit", 
                    connection);
                
                command.Parameters.AddWithValue("@sessionId", sessionId);
                command.Parameters.AddWithValue("@limit", MAX_MEMORY_MESSAGES);
                
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var messageType = reader.GetString(0); // message_type
                    var content = reader.GetString(1);     // content
                    var timestamp = reader.GetDateTime(2); // timestamp
                    
                    messages.Add(new ChatMessage(messageType, content) { Timestamp = timestamp });
                }
                
                Console.WriteLine($"Loaded {messages.Count} messages from database for session {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading messages from database: {ex.Message}");
            }
            
            return messages;
        }
        
        /// <summary>
        /// Get current context token count for a session
        /// </summary>
        public async Task<int> GetContextTokenCountAsync(int sessionId)
        {
            try
            {
                var contextMessages = await GetContextForAIAsync(sessionId);
                return ChatMessage.EstimateTokens(contextMessages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting context token count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get cache statistics for debugging
        /// </summary>
        public string GetCacheStats()
        {
            var totalMessages = _sessionCache.Values.Sum(cache => cache.Count);
            return $"Cache: {_sessionCache.Count} sessions, {totalMessages} total messages";
        }
    }
    
    // Using existing ChatMessage class from IAIService.cs
}
