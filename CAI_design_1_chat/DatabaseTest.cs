using CAI_design_1_chat.Services;
using System;
using System.Threading.Tasks;

namespace CAI_design_1_chat
{
    public class DatabaseTest
    {
        public static async Task RunDatabaseTestAsync()
        {
            Console.WriteLine("=== Database Test Starting ===");
            
            var dbService = new DatabaseService();
            
            try
            {
                // Initialize database
                Console.WriteLine("1. Initializing database...");
                await dbService.InitializeDatabaseAsync();
                
                // Test connection
                Console.WriteLine("2. Testing database connection...");
                var connectionTest = await dbService.TestConnectionAsync();
                if (!connectionTest)
                {
                    Console.WriteLine("❌ Connection test failed!");
                    return;
                }
                
                // Test write operation
                Console.WriteLine("3. Testing write operation...");
                var sessionId = await dbService.InsertTestSessionAsync($"Test Session {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Test read operation
                Console.WriteLine("4. Testing read operation...");
                var retrievedSessionName = await dbService.GetTestSessionAsync(sessionId);
                
                if (!string.IsNullOrEmpty(retrievedSessionName))
                {
                    Console.WriteLine($"✅ Database test successful!");
                    Console.WriteLine($"   - Database path: {dbService.GetDatabasePath()}");
                    Console.WriteLine($"   - Session ID: {sessionId}");
                    Console.WriteLine($"   - Session name: {retrievedSessionName}");
                }
                else
                {
                    Console.WriteLine("❌ Read operation failed!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Database test failed with error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("=== Database Test Complete ===");
        }
    }
}
