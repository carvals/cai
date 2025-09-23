using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CAI_design_1_chat.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;
        private ContextCacheService? _contextCacheService;

        public DatabaseService()
        {
            // Store database in app data folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "CAI_design_1_chat");
            Directory.CreateDirectory(appFolder);
            
            _databasePath = Path.Combine(appFolder, "cai_chat.db");
            _connectionString = $"Data Source={_databasePath}";
        }

        /// <summary>
        /// Set the context cache service for automatic invalidation
        /// </summary>
        public void SetContextCacheService(ContextCacheService contextCacheService)
        {
            _contextCacheService = contextCacheService;
            Console.WriteLine("Context cache service connected to DatabaseService");
        }

        public ContextCacheService? GetContextCacheService()
        {
            return _contextCacheService;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // Read schema from embedded resource
                var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "schema.sql");
                if (!File.Exists(schemaPath))
                {
                    throw new FileNotFoundException($"Schema file not found at: {schemaPath}");
                }

                var schema = await File.ReadAllTextAsync(schemaPath);
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Execute the entire schema as one command to handle triggers properly
                using var sqlCommand = new SqliteCommand(schema, connection);
                await sqlCommand.ExecuteNonQueryAsync();
                
                Console.WriteLine($"Database initialized successfully at: {_databasePath}");
                
                // Run migrations after initial schema setup
                await RunMigrationsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
                throw;
            }
        }

        private async Task RunMigrationsAsync()
        {
            try
            {
                // Check current schema version
                var currentVersion = await GetSchemaVersionAsync();
                Console.WriteLine($"Current database schema version: {currentVersion}");
                
                // Run migration v3 if needed
                if (currentVersion < 3)
                {
                    await ExecuteMigrationV3Async();
                }
                
                // Run migration v4 if needed
                if (currentVersion < 4)
                {
                    await ExecuteMigrationV4Async();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running migrations: {ex.Message}");
                throw;
            }
        }

        private async Task<int> GetSchemaVersionAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(
                    "SELECT MAX(version) FROM schema_version", 
                    connection);
                
                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting schema version: {ex.Message}");
                return 1; // Default to version 1 if table doesn't exist
            }
        }

        private async Task ExecuteMigrationV3Async()
        {
            try
            {
                var migrationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "migration_v3.sql");
                if (!File.Exists(migrationPath))
                {
                    Console.WriteLine($"Migration v3 file not found at: {migrationPath}");
                    return;
                }

                var migrationSql = await File.ReadAllTextAsync(migrationPath);
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(migrationSql, connection);
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine("✅ Database migration v3 completed successfully - Context Handling Panel ready");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing migration v3: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteMigrationV4Async()
        {
            try
            {
                var migrationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "migration_v4.sql");
                if (!File.Exists(migrationPath))
                {
                    Console.WriteLine($"Migration v4 file not found at: {migrationPath}");
                    return;
                }

                var migrationSql = await File.ReadAllTextAsync(migrationPath);
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(migrationSql, connection);
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine("✅ Database migration v4 completed successfully - display_name moved to file_data table");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing migration v4: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Test read
                using var command = new SqliteCommand("SELECT COUNT(*) FROM schema_version", connection);
                var result = await command.ExecuteScalarAsync();
                
                Console.WriteLine($"Database connection test successful. Schema version count: {result}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection test failed: {ex.Message}");
                return false;
            }
        }

        public async Task<int> InsertTestSessionAsync(string sessionName)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(
                    "INSERT INTO session (session_name, user) VALUES (@sessionName, @user); SELECT last_insert_rowid();", 
                    connection);
                
                command.Parameters.AddWithValue("@sessionName", sessionName);
                command.Parameters.AddWithValue("@user", "test_user");
                
                var result = await command.ExecuteScalarAsync();
                var sessionId = Convert.ToInt32(result);
                
                Console.WriteLine($"Test session inserted with ID: {sessionId}");
                return sessionId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting test session: {ex.Message}");
                throw;
            }
        }

        public async Task<string?> GetTestSessionAsync(int sessionId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(
                    "SELECT session_name FROM session WHERE id = @sessionId", 
                    connection);
                
                command.Parameters.AddWithValue("@sessionId", sessionId);
                
                var result = await command.ExecuteScalarAsync();
                var sessionName = result?.ToString();
                
                Console.WriteLine($"Retrieved session name: {sessionName}");
                return sessionName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving test session: {ex.Message}");
                throw;
            }
        }

        public string GetDatabasePath() => _databasePath;
        
        public string GetConnectionString() => _connectionString;

        // Get the most recent session ID (for now, we'll use the latest one)
        public async Task<int> GetCurrentSessionIdAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(
                    "SELECT id FROM session ORDER BY id DESC LIMIT 1", 
                    connection);
                
                var result = await command.ExecuteScalarAsync();
                if (result != null)
                {
                    var sessionId = Convert.ToInt32(result);
                    Console.WriteLine($"Using session ID: {sessionId}");
                    return sessionId;
                }
                
                // If no session exists, create a default session
                Console.WriteLine("No session found, creating default session");
                var newSessionId = await CreateDefaultSessionAsync(connection);
                Console.WriteLine($"Created default session with ID: {newSessionId}");
                return newSessionId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session ID: {ex.Message}");
                return 1; // Fallback
            }
        }

        private async Task<int> CreateDefaultSessionAsync(SqliteConnection connection)
        {
            try
            {
                var insertSql = "INSERT INTO session (session_name, user, is_active) VALUES ('Default Session', 'user', 1)";
                using var insertCommand = new SqliteCommand(insertSql, connection);
                await insertCommand.ExecuteNonQueryAsync();
                
                // Get the newly created session ID
                using var selectCommand = new SqliteCommand("SELECT last_insert_rowid()", connection);
                var result = await selectCommand.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default session: {ex.Message}");
                throw;
            }
        }

        // Enhanced method to save chat messages with context invalidation
        public async Task SaveChatMessageAsync(int sessionId, string messageType, string content)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqliteCommand(
                    "INSERT INTO chat_messages (session_id, message_type, content) VALUES (@sessionId, @messageType, @content)", 
                    connection);
                
                command.Parameters.AddWithValue("@sessionId", sessionId);
                command.Parameters.AddWithValue("@messageType", messageType);
                command.Parameters.AddWithValue("@content", content);
                
                await command.ExecuteNonQueryAsync();
                Console.WriteLine($"Chat message saved: {messageType} (session {sessionId}) - {content.Substring(0, Math.Min(50, content.Length))}...");
                
                // Trigger context invalidation
                if (_contextCacheService != null)
                {
                    await _contextCacheService.InvalidateContextAsync(sessionId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving chat message: {ex.Message}");
            }
        }

        // Context-aware file operations with automatic cache invalidation

        /// <summary>
        /// Update file display name with context invalidation
        /// </summary>
        public async Task UpdateContextFileDisplayNameAsync(int contextLinkId, string newDisplayName)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Get session ID first
                var sessionId = await GetSessionIdForContextLinkAsync(connection, contextLinkId);
                
                // Update display_name in file_data table
                var sql = @"
                    UPDATE file_data 
                    SET display_name = @displayName 
                    WHERE id = (
                        SELECT file_id 
                        FROM context_file_links 
                        WHERE id = @contextLinkId
                    )";
                
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@displayName", newDisplayName);
                command.Parameters.AddWithValue("@contextLinkId", contextLinkId);
                
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"Database updated: context link ID {contextLinkId} → file display_name = '{newDisplayName}'");
                
                // Trigger context invalidation with specific change type
                if (_contextCacheService != null && sessionId > 0)
                {
                    await _contextCacheService.InvalidateContextAsync(sessionId, ContextChangeTypes.FileRenamed, contextLinkId, $"File display name changed to '{newDisplayName}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating file display name: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Update file visibility with context invalidation
        /// </summary>
        public async Task UpdateContextFileVisibilityAsync(int contextLinkId, bool isExcluded)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Get session ID first
                var sessionId = await GetSessionIdForContextLinkAsync(connection, contextLinkId);
                
                var sql = "UPDATE context_file_links SET is_excluded = @isExcluded WHERE id = @contextLinkId";
                
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@isExcluded", isExcluded);
                command.Parameters.AddWithValue("@contextLinkId", contextLinkId);
                
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"Database updated: context link ID {contextLinkId} → is_excluded = {isExcluded}");
                
                // Trigger context invalidation with specific change type
                if (_contextCacheService != null && sessionId > 0)
                {
                    var changeType = isExcluded ? ContextChangeTypes.FileExcluded : ContextChangeTypes.FileIncluded;
                    await _contextCacheService.InvalidateContextAsync(sessionId, changeType, contextLinkId, $"File visibility changed to {(isExcluded ? "excluded" : "included")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating file visibility: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Update file summary usage with context invalidation
        /// </summary>
        public async Task UpdateContextFileSummaryUsageAsync(int contextLinkId, bool useSummary)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Get session ID first
                var sessionId = await GetSessionIdForContextLinkAsync(connection, contextLinkId);
                
                var sql = "UPDATE context_file_links SET use_summary = @useSummary WHERE id = @contextLinkId";
                
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@useSummary", useSummary);
                command.Parameters.AddWithValue("@contextLinkId", contextLinkId);
                
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"Database updated: context link ID {contextLinkId} → use_summary = {useSummary}");
                
                // Trigger context invalidation with specific change type
                if (_contextCacheService != null && sessionId > 0)
                {
                    await _contextCacheService.InvalidateContextAsync(sessionId, ContextChangeTypes.SummaryToggled, contextLinkId, $"Summary usage changed to {(useSummary ? "enabled" : "disabled")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating file summary usage: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Remove file from context with context invalidation
        /// </summary>
        public async Task RemoveContextFileAsync(int contextLinkId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Get session ID first
                var sessionId = await GetSessionIdForContextLinkAsync(connection, contextLinkId);
                
                var sql = "DELETE FROM context_file_links WHERE id = @contextLinkId";
                
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@contextLinkId", contextLinkId);
                
                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    Console.WriteLine($"Database updated: context link ID {contextLinkId} removed from context_file_links");
                }
                else
                {
                    Console.WriteLine($"Warning: No rows affected when removing context link ID {contextLinkId}");
                }
                
                // Trigger context invalidation with specific change type
                if (_contextCacheService != null && sessionId > 0)
                {
                    await _contextCacheService.InvalidateContextAsync(sessionId, ContextChangeTypes.FileDeleted, contextLinkId, "File removed from context");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing file from context: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Helper method to get session ID for a context link
        /// </summary>
        private async Task<int> GetSessionIdForContextLinkAsync(SqliteConnection connection, int contextLinkId)
        {
            try
            {
                var sql = "SELECT context_session_id FROM context_file_links WHERE id = @contextLinkId";
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@contextLinkId", contextLinkId);
                
                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session ID for context link {contextLinkId}: {ex.Message}");
                return 0;
            }
        }

        #region File Search Methods

        public async Task<List<Models.FileSearchResult>> SearchFilesAsync(string searchTerm, int currentSessionId, bool searchNameOnly = false)
        {
            var results = new List<Models.FileSearchResult>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT 
                        fd.id, fd.name, fd.display_name, fd.created_at, fd.summary, fd.content,
                        LENGTH(fd.content) as file_size,
                        CASE 
                            WHEN fd.name LIKE '%.pdf' THEN 'PDF'
                            WHEN fd.name LIKE '%.txt' THEN 'TXT'
                            WHEN fd.name LIKE '%.md' THEN 'MD'
                            WHEN fd.name LIKE '%.docx' THEN 'DOCX'
                            ELSE 'OTHER'
                        END as file_type,
                        CASE WHEN cfl.file_id IS NOT NULL THEN 1 ELSE 0 END as in_context
                    FROM file_data fd
                    LEFT JOIN context_file_links cfl ON fd.id = cfl.file_id 
                        AND cfl.context_session_id = @currentSessionId
                    WHERE (
                        LOWER(fd.name) LIKE LOWER(@search) OR 
                        LOWER(fd.display_name) LIKE LOWER(@search)" + 
                        (searchNameOnly ? "" : @" OR 
                        LOWER(fd.summary) LIKE LOWER(@search) OR
                        LOWER(fd.content) LIKE LOWER(@search)") + @"
                    )
                    ORDER BY fd.created_at DESC
                    LIMIT 50";

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@search", $"%{searchTerm}%");
                command.Parameters.AddWithValue("@currentSessionId", currentSessionId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var result = new Models.FileSearchResult
                    {
                        Id = reader.GetInt32(0), // id
                        Name = reader.GetString(1), // name
                        DisplayName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2), // display_name
                        CreatedAt = DateTime.Parse(reader.GetString(3)), // created_at
                        Summary = reader.IsDBNull(4) ? string.Empty : reader.GetString(4), // summary
                        Content = reader.IsDBNull(5) ? string.Empty : reader.GetString(5), // content
                        FileSize = reader.GetInt64(6), // file_size
                        FileType = reader.GetString(7), // file_type
                        InContext = reader.GetInt32(8) == 1 // in_context
                    };

                    results.Add(result);
                }

                Console.WriteLine($"DatabaseService: SearchFilesAsync found {results.Count} results for '{searchTerm}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DatabaseService: Error in SearchFilesAsync: {ex.Message}");
                throw;
            }

            return results;
        }

        public async Task<bool> IsFileInContextAsync(int fileId, int sessionId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT COUNT(*) FROM context_file_links WHERE file_id = @fileId AND context_session_id = @sessionId";
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@fileId", fileId);
                command.Parameters.AddWithValue("@sessionId", sessionId);

                var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                return count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DatabaseService: Error checking if file is in context: {ex.Message}");
                return false;
            }
        }

        public async Task AddFileToContextAsync(int fileId, int sessionId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                // Get the next order index
                var orderSql = "SELECT COALESCE(MAX(order_index), 0) + 1 FROM context_file_links WHERE context_session_id = @sessionId";
                using var orderCommand = new SqliteCommand(orderSql, connection);
                orderCommand.Parameters.AddWithValue("@sessionId", sessionId);
                var nextOrder = Convert.ToInt32(await orderCommand.ExecuteScalarAsync());

                // Insert the file into context
                var insertSql = @"
                    INSERT INTO context_file_links (context_session_id, file_id, use_summary, is_excluded, order_index)
                    VALUES (@sessionId, @fileId, 0, 0, @orderIndex)";

                using var insertCommand = new SqliteCommand(insertSql, connection);
                insertCommand.Parameters.AddWithValue("@sessionId", sessionId);
                insertCommand.Parameters.AddWithValue("@fileId", fileId);
                insertCommand.Parameters.AddWithValue("@orderIndex", nextOrder);

                await insertCommand.ExecuteNonQueryAsync();

                // Invalidate context cache
                if (_contextCacheService != null)
                {
                    await _contextCacheService.InvalidateContextAsync(sessionId, "FileAddedToContext", fileId);
                }

                Console.WriteLine($"DatabaseService: Added file {fileId} to context for session {sessionId} at order {nextOrder}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DatabaseService: Error adding file to context: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}
