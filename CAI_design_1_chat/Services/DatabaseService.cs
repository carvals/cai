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

        public DatabaseService()
        {
            // Store database in app data folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "CAI_design_1_chat");
            Directory.CreateDirectory(appFolder);
            
            _databasePath = Path.Combine(appFolder, "cai_chat.db");
            _connectionString = $"Data Source={_databasePath}";
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
                
                // If no session exists, return 1 as fallback
                Console.WriteLine("No session found, using session ID: 1");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session ID: {ex.Message}");
                return 1; // Fallback
            }
        }

        // Simple method to save chat messages
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving chat message: {ex.Message}");
            }
        }
    }
}
