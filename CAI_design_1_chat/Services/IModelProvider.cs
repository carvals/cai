using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CAI_design_1_chat.Models;

namespace CAI_design_1_chat.Services;

public interface IModelProvider
{
    string ProviderName { get; }
    Task<List<AIModel>> FetchAvailableModelsAsync(string apiKey);
    List<AIModel> GetCachedModels();
    void CacheModels(List<AIModel> models);
    bool IsCacheExpired();
    List<AIModel> GetDefaultModels();
}
