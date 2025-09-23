using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Services
{
    public class InstructionShortcutService
    {
        private readonly DatabaseService _databaseService;
        private List<InstructionShortcut> _cachedShortcuts = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;

        public InstructionShortcutService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Get all instruction shortcuts (cached in memory)
        /// </summary>
        public async Task<List<InstructionShortcut>> GetAllShortcutsAsync(bool includeInactive = false)
        {
            await EnsureCacheLoadedAsync();
            
            return _cachedShortcuts
                .Where(s => includeInactive || s.IsActive)
                .OrderBy(s => s.Title)
                .ToList();
        }

        /// <summary>
        /// Get shortcuts filtered by prompt type
        /// </summary>
        public async Task<List<InstructionShortcut>> GetShortcutsByTypeAsync(string promptType, bool includeInactive = false)
        {
            var allShortcuts = await GetAllShortcutsAsync(includeInactive);
            return allShortcuts
                .Where(s => string.Equals(s.PromptType, promptType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Get distinct prompt types for filter dropdown
        /// </summary>
        public async Task<List<string>> GetDistinctPromptTypesAsync()
        {
            await EnsureCacheLoadedAsync();
            
            return _cachedShortcuts
                .Where(s => s.IsActive)
                .Select(s => s.PromptType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        /// <summary>
        /// Search shortcuts by text (for "/" trigger filtering)
        /// </summary>
        public async Task<List<InstructionShortcut>> SearchShortcutsAsync(string searchText)
        {
            await EnsureCacheLoadedAsync();
            
            if (string.IsNullOrWhiteSpace(searchText))
                return _cachedShortcuts.Where(s => s.IsActive && !string.IsNullOrEmpty(s.Shortcut)).ToList();

            var lowerSearch = searchText.ToLower();
            
            return _cachedShortcuts
                .Where(s => s.IsActive && !string.IsNullOrEmpty(s.Shortcut))
                .Where(s => 
                    s.Shortcut!.ToLower().Contains(lowerSearch) ||
                    s.Title.ToLower().Contains(lowerSearch) ||
                    s.Description.ToLower().Contains(lowerSearch))
                .OrderBy(s => s.Shortcut!.ToLower().StartsWith(lowerSearch) ? 0 : 1) // Exact matches first
                .ThenBy(s => s.Shortcut)
                .ToList();
        }

        /// <summary>
        /// Get shortcut by exact shortcut text
        /// </summary>
        public async Task<InstructionShortcut?> GetShortcutByTextAsync(string shortcutText)
        {
            await EnsureCacheLoadedAsync();
            
            return _cachedShortcuts
                .FirstOrDefault(s => s.IsActive && 
                    string.Equals(s.Shortcut, shortcutText, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Save or update instruction shortcut
        /// </summary>
        public async Task<InstructionShortcut> SaveShortcutAsync(InstructionShortcut shortcut)
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            if (shortcut.Id == 0)
            {
                // Insert new shortcut
                var sql = @"
                    INSERT INTO prompt_instructions (prompt_type, language, instruction, title, description, 
                                                   shortcut, is_system, is_active, created_by, created_at, updated_at, usage_count)
                    VALUES (@promptType, @language, @instruction, @title, @description, 
                            @shortcut, @isSystem, @isActive, @createdBy, @createdAt, @updatedAt, @usageCount);
                    SELECT last_insert_rowid();";

                using var command = new SqliteCommand(sql, connection);
                AddShortcutParameters(command, shortcut);
                
                var result = await command.ExecuteScalarAsync();
                shortcut.Id = Convert.ToInt32(result);
            }
            else
            {
                // Update existing shortcut
                var sql = @"
                    UPDATE prompt_instructions 
                    SET prompt_type = @promptType, language = @language, instruction = @instruction, 
                        title = @title, description = @description, shortcut = @shortcut, 
                        is_system = @isSystem, is_active = @isActive, updated_at = @updatedAt
                    WHERE id = @id";

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@id", shortcut.Id);
                AddShortcutParameters(command, shortcut);
                
                await command.ExecuteNonQueryAsync();
            }

            // Reload cache after save
            await ReloadCacheAsync();
            
            Console.WriteLine($"Instruction shortcut saved: {shortcut.Title} ({shortcut.Shortcut})");
            return shortcut;
        }

        /// <summary>
        /// Increment usage count for a shortcut
        /// </summary>
        public async Task IncrementUsageAsync(int shortcutId)
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = "UPDATE prompt_instructions SET usage_count = usage_count + 1 WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", shortcutId);
            
            await command.ExecuteNonQueryAsync();

            // Update cache
            var cachedShortcut = _cachedShortcuts.FirstOrDefault(s => s.Id == shortcutId);
            if (cachedShortcut != null)
            {
                cachedShortcut.UsageCount++;
            }
        }

        /// <summary>
        /// Soft delete shortcut (set is_active = false)
        /// </summary>
        public async Task DeleteShortcutAsync(int shortcutId)
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = "UPDATE prompt_instructions SET is_active = FALSE, updated_at = CURRENT_TIMESTAMP WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", shortcutId);
            
            await command.ExecuteNonQueryAsync();

            // Update cache
            var cachedShortcut = _cachedShortcuts.FirstOrDefault(s => s.Id == shortcutId);
            if (cachedShortcut != null)
            {
                cachedShortcut.IsActive = false;
            }

            Console.WriteLine($"Instruction shortcut deleted: ID {shortcutId}");
        }

        /// <summary>
        /// Reload cache from database (call after save/update operations)
        /// </summary>
        public async Task ReloadCacheAsync()
        {
            _cachedShortcuts.Clear();
            _lastCacheUpdate = DateTime.MinValue;
            await EnsureCacheLoadedAsync();
        }

        private async Task EnsureCacheLoadedAsync()
        {
            if (_cachedShortcuts.Count == 0 || DateTime.Now.Subtract(_lastCacheUpdate).TotalMinutes > 5)
            {
                await LoadCacheFromDatabaseAsync();
            }
        }

        private async Task LoadCacheFromDatabaseAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_databaseService.GetConnectionString());
                await connection.OpenAsync();

                var sql = @"
                    SELECT id, prompt_type, language, instruction, title, description, 
                           shortcut, is_system, is_active, created_by, created_at, updated_at, usage_count
                    FROM prompt_instructions
                    ORDER BY title";

                using var command = new SqliteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                _cachedShortcuts.Clear();
                while (await reader.ReadAsync())
                {
                    var idOrdinal = reader.GetOrdinal("id");
                    var promptTypeOrdinal = reader.GetOrdinal("prompt_type");
                    var languageOrdinal = reader.GetOrdinal("language");
                    var instructionOrdinal = reader.GetOrdinal("instruction");
                    var titleOrdinal = reader.GetOrdinal("title");
                    var descriptionOrdinal = reader.GetOrdinal("description");
                    var shortcutOrdinal = reader.GetOrdinal("shortcut");
                    var isSystemOrdinal = reader.GetOrdinal("is_system");
                    var isActiveOrdinal = reader.GetOrdinal("is_active");
                    var createdByOrdinal = reader.GetOrdinal("created_by");
                    var createdAtOrdinal = reader.GetOrdinal("created_at");
                    var updatedAtOrdinal = reader.GetOrdinal("updated_at");
                    var usageCountOrdinal = reader.GetOrdinal("usage_count");

                    var shortcut = new InstructionShortcut
                    {
                        Id = reader.GetInt32(idOrdinal),
                        PromptType = reader.GetString(promptTypeOrdinal),
                        Language = reader.GetString(languageOrdinal),
                        Instruction = reader.GetString(instructionOrdinal),
                        Title = reader.IsDBNull(titleOrdinal) ? "" : reader.GetString(titleOrdinal),
                        Description = reader.IsDBNull(descriptionOrdinal) ? "" : reader.GetString(descriptionOrdinal),
                        Shortcut = reader.IsDBNull(shortcutOrdinal) ? null : reader.GetString(shortcutOrdinal),
                        IsSystem = reader.GetBoolean(isSystemOrdinal),
                        IsActive = reader.IsDBNull(isActiveOrdinal) ? true : reader.GetBoolean(isActiveOrdinal),
                        CreatedBy = reader.IsDBNull(createdByOrdinal) ? null : reader.GetString(createdByOrdinal),
                        CreatedAt = reader.GetDateTime(createdAtOrdinal),
                        UpdatedAt = reader.GetDateTime(updatedAtOrdinal),
                        UsageCount = reader.GetInt32(usageCountOrdinal)
                    };

                    _cachedShortcuts.Add(shortcut);
                }

                _lastCacheUpdate = DateTime.Now;
                Console.WriteLine($"Loaded {_cachedShortcuts.Count} instruction shortcuts into cache");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading instruction shortcuts cache: {ex.Message}");
                throw;
            }
        }

        private static void AddShortcutParameters(SqliteCommand command, InstructionShortcut shortcut)
        {
            command.Parameters.AddWithValue("@promptType", shortcut.PromptType);
            command.Parameters.AddWithValue("@language", shortcut.Language);
            command.Parameters.AddWithValue("@instruction", shortcut.Instruction);
            command.Parameters.AddWithValue("@title", shortcut.Title);
            command.Parameters.AddWithValue("@description", shortcut.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@shortcut", shortcut.Shortcut ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isSystem", shortcut.IsSystem);
            command.Parameters.AddWithValue("@isActive", shortcut.IsActive);
            command.Parameters.AddWithValue("@createdBy", shortcut.CreatedBy ?? "current_user");
            command.Parameters.AddWithValue("@createdAt", shortcut.CreatedAt);
            command.Parameters.AddWithValue("@updatedAt", DateTime.Now);
            command.Parameters.AddWithValue("@usageCount", shortcut.UsageCount);
        }
    }
}
