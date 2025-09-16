using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Services;

public class PromptInstructionService : IPromptInstructionService
{
    private readonly DatabaseService _databaseService;

    public PromptInstructionService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<PromptInstruction>> SearchPromptsAsync(string? searchTerm = null, string? promptType = null)
    {
        var prompts = new List<PromptInstruction>();
        
        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                SELECT id, prompt_type, language, instruction, title, description, 
                       is_system, created_by, created_at, updated_at, usage_count
                FROM prompt_instructions 
                WHERE 1=1";

            var parameters = new List<SqliteParameter>();

            // Add search term filter (title OR description)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += " AND (title LIKE @searchTerm OR description LIKE @searchTerm)";
                parameters.Add(new SqliteParameter("@searchTerm", $"%{searchTerm}%"));
            }

            // Add prompt type filter
            if (!string.IsNullOrWhiteSpace(promptType) && !promptType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                sql += " AND prompt_type = @promptType";
                parameters.Add(new SqliteParameter("@promptType", promptType));
            }

            sql += " ORDER BY usage_count DESC, created_at DESC";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddRange(parameters.ToArray());

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                prompts.Add(MapFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            // Log error (in a real app, use proper logging)
            System.Diagnostics.Debug.WriteLine($"Error searching prompts: {ex.Message}");
            throw;
        }

        return prompts;
    }

    public async Task<PromptInstruction?> GetPromptByIdAsync(int id)
    {
        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                SELECT id, prompt_type, language, instruction, title, description, 
                       is_system, created_by, created_at, updated_at, usage_count
                FROM prompt_instructions 
                WHERE id = @id";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapFromReader(reader);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting prompt by ID: {ex.Message}");
            throw;
        }

        return null;
    }

    public async Task<PromptInstruction> SavePromptAsync(PromptInstruction prompt)
    {
        if (!prompt.IsValid())
        {
            throw new ArgumentException("Prompt instruction is not valid");
        }

        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO prompt_instructions 
                (prompt_type, language, instruction, title, description, is_system, created_by, usage_count)
                VALUES (@promptType, @language, @instruction, @title, @description, @isSystem, @createdBy, @usageCount);
                SELECT last_insert_rowid();";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@promptType", prompt.PromptType);
            command.Parameters.AddWithValue("@language", prompt.Language);
            command.Parameters.AddWithValue("@instruction", prompt.Instruction);
            command.Parameters.AddWithValue("@title", prompt.Title ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@description", prompt.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isSystem", prompt.IsSystem);
            command.Parameters.AddWithValue("@createdBy", prompt.CreatedBy ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@usageCount", prompt.UsageCount);

            var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
            prompt.Id = newId;

            return prompt;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving prompt: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> UpdatePromptAsync(PromptInstruction prompt)
    {
        if (!prompt.IsValid())
        {
            throw new ArgumentException("Prompt instruction is not valid");
        }

        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                UPDATE prompt_instructions 
                SET prompt_type = @promptType, language = @language, instruction = @instruction,
                    title = @title, description = @description, is_system = @isSystem,
                    created_by = @createdBy, updated_at = CURRENT_TIMESTAMP, usage_count = @usageCount
                WHERE id = @id";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", prompt.Id);
            command.Parameters.AddWithValue("@promptType", prompt.PromptType);
            command.Parameters.AddWithValue("@language", prompt.Language);
            command.Parameters.AddWithValue("@instruction", prompt.Instruction);
            command.Parameters.AddWithValue("@title", prompt.Title ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@description", prompt.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isSystem", prompt.IsSystem);
            command.Parameters.AddWithValue("@createdBy", prompt.CreatedBy ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@usageCount", prompt.UsageCount);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating prompt: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeletePromptAsync(int id)
    {
        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = "DELETE FROM prompt_instructions WHERE id = @id";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting prompt: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> IncrementUsageAsync(int id)
    {
        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                UPDATE prompt_instructions 
                SET usage_count = usage_count + 1, updated_at = CURRENT_TIMESTAMP 
                WHERE id = @id";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error incrementing usage: {ex.Message}");
            throw;
        }
    }

    public async Task<List<string>> GetPromptTypesAsync()
    {
        var types = new List<string>();

        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = "SELECT DISTINCT prompt_type FROM prompt_instructions ORDER BY prompt_type";

            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                types.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting prompt types: {ex.Message}");
            throw;
        }

        return types;
    }

    public async Task<List<string>> GetLanguagesAsync()
    {
        var languages = new List<string>();

        try
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = "SELECT DISTINCT language FROM prompt_instructions ORDER BY language";

            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                languages.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting languages: {ex.Message}");
            throw;
        }

        return languages;
    }

    private static PromptInstruction MapFromReader(SqliteDataReader reader)
    {
        return new PromptInstruction
        {
            Id = reader.GetInt32(0), // id
            PromptType = reader.GetString(1), // prompt_type
            Language = reader.GetString(2), // language
            Instruction = reader.GetString(3), // instruction
            Title = reader.IsDBNull(4) ? null : reader.GetString(4), // title
            Description = reader.IsDBNull(5) ? null : reader.GetString(5), // description
            IsSystem = reader.GetBoolean(6), // is_system
            CreatedBy = reader.IsDBNull(7) ? null : reader.GetString(7), // created_by
            CreatedAt = reader.GetDateTime(8), // created_at
            UpdatedAt = reader.GetDateTime(9), // updated_at
            UsageCount = reader.GetInt32(10) // usage_count
        };
    }
}
