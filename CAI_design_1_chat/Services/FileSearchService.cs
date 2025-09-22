using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Services
{
    public class FileSearchService
    {
        private readonly DatabaseService _databaseService;

        public FileSearchService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        public async Task<List<FileSearchResult>> SearchFilesAsync(string searchTerm, int currentSessionId)
        {
            try
            {
                Console.WriteLine($"FileSearchService: Searching for '{searchTerm}' in session {currentSessionId}");

                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 3)
                {
                    Console.WriteLine("FileSearchService: Search term too short or empty");
                    return new List<FileSearchResult>();
                }

                var results = await _databaseService.SearchFilesAsync(searchTerm, currentSessionId);
                Console.WriteLine($"FileSearchService: Found {results.Count} results");

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchService: Error searching files: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> AddFileToContextAsync(int fileId, int sessionId)
        {
            try
            {
                Console.WriteLine($"FileSearchService: Adding file {fileId} to context for session {sessionId}");

                // Check if file is already in context
                var isInContext = await _databaseService.IsFileInContextAsync(fileId, sessionId);
                if (isInContext)
                {
                    Console.WriteLine($"FileSearchService: File {fileId} already in context for session {sessionId}");
                    return false;
                }

                // Add file to context
                await _databaseService.AddFileToContextAsync(fileId, sessionId);
                Console.WriteLine($"FileSearchService: Successfully added file {fileId} to context");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSearchService: Error adding file to context: {ex.Message}");
                throw;
            }
        }
    }
}
