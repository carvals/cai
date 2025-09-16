using System.Collections.Generic;
using System.Threading.Tasks;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Services;

public interface IPromptInstructionService
{
    /// <summary>
    /// Search prompts by title and/or description with optional prompt type filter
    /// </summary>
    /// <param name="searchTerm">Search term to match against title and description</param>
    /// <param name="promptType">Filter by prompt type (null or "all" for no filter)</param>
    /// <returns>List of matching prompt instructions ordered by usage count</returns>
    Task<List<PromptInstruction>> SearchPromptsAsync(string? searchTerm = null, string? promptType = null);

    /// <summary>
    /// Get a specific prompt instruction by ID
    /// </summary>
    /// <param name="id">Prompt instruction ID</param>
    /// <returns>Prompt instruction or null if not found</returns>
    Task<PromptInstruction?> GetPromptByIdAsync(int id);

    /// <summary>
    /// Save a new prompt instruction to the database
    /// </summary>
    /// <param name="prompt">Prompt instruction to save</param>
    /// <returns>The saved prompt with assigned ID</returns>
    Task<PromptInstruction> SavePromptAsync(PromptInstruction prompt);

    /// <summary>
    /// Update an existing prompt instruction
    /// </summary>
    /// <param name="prompt">Prompt instruction to update</param>
    /// <returns>True if successful, false if not found</returns>
    Task<bool> UpdatePromptAsync(PromptInstruction prompt);

    /// <summary>
    /// Delete a prompt instruction
    /// </summary>
    /// <param name="id">ID of prompt to delete</param>
    /// <returns>True if successful, false if not found</returns>
    Task<bool> DeletePromptAsync(int id);

    /// <summary>
    /// Increment usage count for a prompt instruction
    /// </summary>
    /// <param name="id">ID of prompt to increment usage</param>
    /// <returns>True if successful, false if not found</returns>
    Task<bool> IncrementUsageAsync(int id);

    /// <summary>
    /// Get all available prompt types
    /// </summary>
    /// <returns>List of distinct prompt types in the database</returns>
    Task<List<string>> GetPromptTypesAsync();

    /// <summary>
    /// Get all available languages
    /// </summary>
    /// <returns>List of distinct languages in the database</returns>
    Task<List<string>> GetLanguagesAsync();
}
