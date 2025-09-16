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
                
                // Execute schema commands
                var commands = schema.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var command in commands)
                {
                    var trimmedCommand = command.Trim();
                    if (!string.IsNullOrEmpty(trimmedCommand) && !trimmedCommand.StartsWith("--"))
                    {
                        using var sqlCommand = new SqliteCommand(trimmedCommand, connection);
                        await sqlCommand.ExecuteNonQueryAsync();
                    }
                }
                
                Console.WriteLine($"Database initialized successfully at: {_databasePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
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
    }
}
