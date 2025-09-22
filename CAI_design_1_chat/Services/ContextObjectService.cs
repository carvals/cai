using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace CAI_design_1_chat.Services
{
    public class ContextObjectService
    {
        private readonly DatabaseService _databaseService;
        private readonly ChatContextService _chatContextService;

        public ContextObjectService()
        {
            _databaseService = new DatabaseService();
            _chatContextService = new ChatContextService(_databaseService);
        }

        public async Task<string> BuildContextJsonAsync(int sessionId)
        {
            try
            {
                var contextObject = new
                {
                    assistant_role = new
                    {
                        description = "you must be a clear assistant, if a specific role is better for you ask in the chat and check the answer in the chat history section below",
                        context_date = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")
                    },
                    file_context = await BuildFileContextAsync(sessionId),
                    message_history = await BuildMessageHistoryAsync(sessionId)
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(contextObject, options);
                
                Console.WriteLine($"Context JSON generated for session {sessionId}: {json.Length} characters");
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building context JSON: {ex.Message}");
                throw;
            }
        }

        private async Task<object> BuildFileContextAsync(int sessionId)
        {
            var files = await GetContextFilesAsync(sessionId);
            var totalCharacters = 0;

            var fileList = new List<object>();
            foreach (var file in files)
            {
                if (!file.IsExcluded)
                {
                    var content = file.UseSummary && !string.IsNullOrEmpty(file.Summary) 
                        ? file.Summary 
                        : file.Content;
                    
                    totalCharacters += content?.Length ?? 0;

                    fileList.Add(new
                    {
                        order_index = file.OrderIndex,
                        display_name = file.DisplayName,
                        original_name = file.OriginalName,
                        character_count = content?.Length ?? 0,
                        use_summary = file.UseSummary,
                        content = content ?? ""
                    });
                }
            }

            return new
            {
                total_files = fileList.Count,
                total_characters = totalCharacters,
                files = fileList
            };
        }

        private async Task<object> BuildMessageHistoryAsync(int sessionId)
        {
            try
            {
                var messages = await _chatContextService.GetContextForAIAsync(sessionId);
                var messageList = new List<object>();

                foreach (var message in messages)
                {
                    messageList.Add(new
                    {
                        timestamp = message.Timestamp.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                        role = message.Role.ToString().ToLower(),
                        content = message.Content
                    });
                }

                return new
                {
                    total_messages = messageList.Count,
                    messages = messageList
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building message history: {ex.Message}");
                return new
                {
                    total_messages = 0,
                    messages = new List<object>()
                };
            }
        }

        private async Task<List<ContextFileInfo>> GetContextFilesAsync(int sessionId)
        {
            var files = new List<ContextFileInfo>();

            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    cfl.id,
                    fd.display_name,
                    cfl.use_summary,
                    cfl.is_excluded,
                    cfl.order_index,
                    fd.name as original_name,
                    fd.content,
                    fd.summary,
                    LENGTH(COALESCE(fd.content, '')) as character_count
                FROM context_file_links cfl
                JOIN file_data fd ON cfl.file_id = fd.id
                WHERE cfl.context_session_id = @sessionId
                ORDER BY cfl.order_index, cfl.id";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var contentOrdinal = reader.GetOrdinal("content");
                var summaryOrdinal = reader.GetOrdinal("summary");

                files.Add(new ContextFileInfo
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
                    OriginalName = reader.GetString(reader.GetOrdinal("original_name")),
                    UseSummary = reader.GetBoolean(reader.GetOrdinal("use_summary")),
                    IsExcluded = reader.GetBoolean(reader.GetOrdinal("is_excluded")),
                    OrderIndex = reader.GetInt32(reader.GetOrdinal("order_index")),
                    Content = reader.IsDBNull(contentOrdinal) ? "" : reader.GetString(contentOrdinal),
                    Summary = reader.IsDBNull(summaryOrdinal) ? "" : reader.GetString(summaryOrdinal),
                    CharacterCount = reader.GetInt32(reader.GetOrdinal("character_count"))
                });
            }

            return files;
        }
    }

    public class ContextFileInfo
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = "";
        public string OriginalName { get; set; } = "";
        public bool UseSummary { get; set; }
        public bool IsExcluded { get; set; }
        public int OrderIndex { get; set; }
        public string Content { get; set; } = "";
        public string Summary { get; set; } = "";
        public int CharacterCount { get; set; }
    }
}
