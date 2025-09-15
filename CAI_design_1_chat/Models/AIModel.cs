using System;

namespace CAI_design_1_chat.Models;

public class AIModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public bool IsDeprecated { get; set; }
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public string Provider { get; set; } = string.Empty;
    
    public AIModel() { }
    
    public AIModel(string id, string displayName, string description = "", string provider = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Provider = provider;
        Created = DateTime.UtcNow;
        Capabilities = Array.Empty<string>();
    }
}
