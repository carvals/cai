using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Services;

public class OpenAIModelProvider : IModelProvider
{
    public string ProviderName => "OpenAI";
    private const string ModelsEndpoint = "https://api.openai.com/v1/models";
    private readonly HttpClient _httpClient;

    public OpenAIModelProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<List<AIModel>> FetchAvailableModelsAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key is required", nameof(apiKey));

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonSerializer.Deserialize<OpenAIModelsResponse>(jsonContent);

            if (modelsResponse?.data == null)
                return GetDefaultModels();

            // Filter for chat completion models
            var chatModels = modelsResponse.data
                .Where(m => m.id.StartsWith("gpt-") && 
                           !m.id.Contains("instruct") && 
                           !m.id.Contains("embedding") &&
                           !m.id.Contains("tts") &&
                           !m.id.Contains("whisper"))
                .OrderByDescending(m => m.created)
                .Select(m => new AIModel
                {
                    Id = m.id,
                    DisplayName = FormatModelName(m.id),
                    Description = GetModelDescription(m.id),
                    Created = DateTimeOffset.FromUnixTimeSeconds(m.created).DateTime,
                    Provider = ProviderName,
                    Capabilities = GetModelCapabilities(m.id)
                })
                .ToList();

            return chatModels.Any() ? chatModels : GetDefaultModels();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch OpenAI models: {ex.Message}", ex);
        }
    }

    public List<AIModel> GetCachedModels()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var cacheKey = $"{ProviderName}_models_cache";
            var cachedJson = localSettings.Values[cacheKey] as string;

            if (string.IsNullOrEmpty(cachedJson))
                return GetDefaultModels();

            var models = JsonSerializer.Deserialize<List<AIModel>>(cachedJson);
            return models ?? GetDefaultModels();
        }
        catch
        {
            return GetDefaultModels();
        }
    }

    public void CacheModels(List<AIModel> models)
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var cacheKey = $"{ProviderName}_models_cache";
            var expiryKey = $"{ProviderName}_models_cache_expiry";

            var json = JsonSerializer.Serialize(models);
            localSettings.Values[cacheKey] = json;
            localSettings.Values[expiryKey] = DateTime.UtcNow.AddHours(24).ToBinary();
        }
        catch
        {
            // Ignore cache errors
        }
    }

    public bool IsCacheExpired()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var expiryKey = $"{ProviderName}_models_cache_expiry";
            
            if (localSettings.Values[expiryKey] is long expiryBinary)
            {
                var expiryTime = DateTime.FromBinary(expiryBinary);
                return DateTime.UtcNow > expiryTime;
            }
            
            return true; // No expiry set, consider expired
        }
        catch
        {
            return true; // Error reading cache, consider expired
        }
    }

    public List<AIModel> GetDefaultModels()
    {
        return new List<AIModel>
        {
            new("gpt-4", "GPT-4", "Most capable model for complex tasks", ProviderName),
            new("gpt-4-turbo", "GPT-4 Turbo", "Faster GPT-4 with updated knowledge", ProviderName),
            new("gpt-3.5-turbo", "GPT-3.5 Turbo", "Fast and efficient for most tasks", ProviderName),
            new("gpt-3.5-turbo-16k", "GPT-3.5 Turbo 16K", "Extended context window version", ProviderName)
        };
    }

    private static string FormatModelName(string modelId)
    {
        return modelId switch
        {
            var id when id.StartsWith("gpt-4o") => id.Replace("gpt-4o", "GPT-4o").Replace("-", " ").ToUpperInvariant(),
            var id when id.StartsWith("gpt-4") => id.Replace("gpt-4", "GPT-4").Replace("-", " ").ToUpperInvariant(),
            var id when id.StartsWith("gpt-3.5") => id.Replace("gpt-3.5", "GPT-3.5").Replace("-", " ").ToUpperInvariant(),
            _ => modelId.ToUpperInvariant()
        };
    }

    private static string GetModelDescription(string modelId)
    {
        return modelId switch
        {
            var id when id.Contains("gpt-4o") => "Advanced multimodal model with vision capabilities",
            var id when id.Contains("gpt-4-turbo") => "Faster GPT-4 with updated knowledge cutoff",
            var id when id.Contains("gpt-4") => "Most capable model for complex reasoning tasks",
            var id when id.Contains("gpt-3.5-turbo-16k") => "Extended 16K context window for longer conversations",
            var id when id.Contains("gpt-3.5-turbo") => "Fast and efficient for most conversational tasks",
            _ => "OpenAI language model"
        };
    }

    private static string[] GetModelCapabilities(string modelId)
    {
        var capabilities = new List<string> { "chat", "completion" };
        
        if (modelId.Contains("gpt-4o") || modelId.Contains("vision"))
            capabilities.Add("vision");
            
        if (modelId.Contains("gpt-4"))
            capabilities.Add("function_calling");
            
        return capabilities.ToArray();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    // JSON response models
    private class OpenAIModelsResponse
    {
        public OpenAIModelData[]? data { get; set; }
    }

    private class OpenAIModelData
    {
        public string id { get; set; } = string.Empty;
        public long created { get; set; }
        public string owned_by { get; set; } = string.Empty;
    }
}
