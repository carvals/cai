using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace CAI_design_1_chat.Services
{
    public class OllamaService : IAIService
    {
        private string? _serverUrl;
        private string? _model;

        public string ProviderName => "Ollama";

        public bool IsConfigured => !string.IsNullOrEmpty(_serverUrl) && !string.IsNullOrEmpty(_model);

        public OllamaService()
        {
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            _serverUrl = settings.TryGetValue("OllamaUrl", out var url) ? url?.ToString() : "http://localhost:11434";
            
            // Get model from settings, but handle empty/null values
            var modelFromSettings = settings.TryGetValue("OllamaModel", out var model) ? model?.ToString() : null;
            _model = string.IsNullOrWhiteSpace(modelFromSettings) ? "llama3.2:latest" : modelFromSettings;

            Console.WriteLine($"Ollama LoadConfiguration: ServerUrl={_serverUrl}, Model={_model}");
            Console.WriteLine($"Ollama IsConfigured: {IsConfigured}");
        }

        public async Task<string> SendMessageAsync(string message, List<ChatMessage>? conversationHistory = null, CancellationToken cancellationToken = default)
        {
            // Convert to ContextData for backward compatibility
            var context = ContextData.FromMessages(conversationHistory ?? new List<ChatMessage>());
            return await SendMessageWithContextAsync(message, context, cancellationToken);
        }

        public async Task<string> SendMessageWithContextAsync(string message, ContextData context, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                throw new AIServiceException(ProviderName, "Ollama service is not configured. Please set server URL and model in settings.");
            }

            try
            {
                var prompt = BuildOllamaPromptWithContext(message, context);
                
                Console.WriteLine($"Ollama request prepared:");
                Console.WriteLine($"  - Server: {_serverUrl}");
                Console.WriteLine($"  - Model: {_model}");
                Console.WriteLine($"  - Context files: {context.FileCount}");
                Console.WriteLine($"  - Messages: {context.MessageCount}");
                Console.WriteLine($"  - Total tokens: ~{context.TotalTokens}");
                Console.WriteLine($"  - Prompt length: {prompt.Length} characters");

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var requestBody = new
                {
                    model = _model,
                    prompt = prompt,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{_serverUrl}/api/generate", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new AIServiceException(ProviderName, $"HTTP {response.StatusCode}: {errorContent}", "HTTP_ERROR");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent);
                
                if (responseJson.RootElement.TryGetProperty("response", out var responseElement))
                {
                    var responseText = responseElement.GetString() ?? "No response from Ollama";
                    Console.WriteLine($"Ollama response received: {responseText.Length} characters");
                    return responseText;
                }

                throw new AIServiceException(ProviderName, "Invalid response format from Ollama", "PARSE_ERROR");
            }
            catch (Exception ex) when (!(ex is AIServiceException))
            {
                throw new AIServiceException(ProviderName, $"Ollama API error: {ex.Message}", "API_ERROR", ex);
            }
        }

        public async Task<string> SendMessageStreamAsync(string message, Action<string> onTokenReceived, List<ChatMessage>? conversationHistory = null, CancellationToken cancellationToken = default)
        {
            // Convert to ContextData for backward compatibility
            var context = ContextData.FromMessages(conversationHistory ?? new List<ChatMessage>());
            return await SendMessageStreamWithContextAsync(message, context, onTokenReceived, cancellationToken);
        }

        public async Task<string> SendMessageStreamWithContextAsync(string message, ContextData context, Action<string> onTokenReceived, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                throw new AIServiceException(ProviderName, "Ollama service is not configured. Please set server URL and model in settings.");
            }

            try
            {
                var prompt = BuildOllamaPromptWithContext(message, context);
                
                Console.WriteLine($"Ollama streaming request prepared:");
                Console.WriteLine($"  - Server: {_serverUrl}");
                Console.WriteLine($"  - Model: {_model}");
                Console.WriteLine($"  - Context files: {context.FileCount}");
                Console.WriteLine($"  - Messages: {context.MessageCount}");
                Console.WriteLine($"  - Total tokens: ~{context.TotalTokens}");
                Console.WriteLine($"  - Prompt length: {prompt.Length} characters");

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                var requestBody = new
                {
                    model = _model,
                    prompt = prompt,
                    stream = true
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{_serverUrl}/api/generate", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new AIServiceException(ProviderName, $"HTTP {response.StatusCode}: {errorContent}", "HTTP_ERROR");
                }

                var fullResponse = new StringBuilder();
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(line);
                        if (jsonDoc.RootElement.TryGetProperty("response", out var responseElement))
                        {
                            var token = responseElement.GetString();
                            if (!string.IsNullOrEmpty(token))
                            {
                                fullResponse.Append(token);
                                onTokenReceived?.Invoke(token);
                            }
                        }

                        // Check if streaming is done
                        if (jsonDoc.RootElement.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean())
                        {
                            break;
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip invalid JSON lines
                        continue;
                    }
                }

                var responseText = fullResponse.ToString();
                Console.WriteLine($"Ollama streaming response completed: {responseText.Length} characters");
                return responseText;
            }
            catch (Exception ex) when (!(ex is AIServiceException))
            {
                throw new AIServiceException(ProviderName, $"Ollama streaming error: {ex.Message}", "STREAMING_ERROR", ex);
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
                return false;

            try
            {
                var testMessage = "Hello, this is a connection test.";
                var response = await SendMessageAsync(testMessage, null, cancellationToken);
                return !string.IsNullOrEmpty(response);
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_serverUrl))
                return new List<string> { "llama2", "codellama", "mistral" }; // Default models

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var response = await httpClient.GetAsync($"{_serverUrl}/api/tags", cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(content);
                    
                    var models = new List<string>();
                    if (json.RootElement.TryGetProperty("models", out var modelsArray))
                    {
                        foreach (var model in modelsArray.EnumerateArray())
                        {
                            if (model.TryGetProperty("name", out var nameElement))
                            {
                                var name = nameElement.GetString();
                                if (!string.IsNullOrEmpty(name))
                                {
                                    models.Add(name);
                                }
                            }
                        }
                    }
                    
                    return models.Count > 0 ? models : new List<string> { "llama2", "codellama", "mistral" };
                }
            }
            catch
            {
                // Fall back to default models on error
            }

            return new List<string> { "llama2", "codellama", "mistral" };
        }

        private string BuildOllamaPromptWithContext(string userMessage, ContextData context)
        {
            var prompt = new StringBuilder();

            // Add system context and file information
            if (context.FileCount > 0 || !string.IsNullOrEmpty(context.AssistantRole))
            {
                prompt.AppendLine("SYSTEM CONTEXT:");
                prompt.AppendLine(context.AssistantRole);
                prompt.AppendLine();
            }

            // Add file context if available
            if (context.FileCount > 0)
            {
                prompt.AppendLine("CONTEXT FILES:");
                prompt.AppendLine();
                
                foreach (var file in context.Files.Where(f => !f.IsExcluded).OrderBy(f => f.OrderIndex))
                {
                    prompt.AppendLine($"=== File: {file.DisplayName} ===");
                    if (!string.IsNullOrEmpty(file.OriginalName) && file.OriginalName != file.DisplayName)
                    {
                        prompt.AppendLine($"Original filename: {file.OriginalName}");
                    }
                    if (file.UseSummary)
                    {
                        prompt.AppendLine("(Summary provided)");
                    }
                    prompt.AppendLine();
                    prompt.AppendLine(file.Content);
                    prompt.AppendLine();
                    prompt.AppendLine("===");
                    prompt.AppendLine();
                }
            }

            // Add conversation history
            if (context.MessageCount > 0)
            {
                prompt.AppendLine("CONVERSATION HISTORY:");
                prompt.AppendLine();
                
                foreach (var msg in context.MessageHistory)
                {
                    var roleLabel = msg.Role switch
                    {
                        "user" => "Human",
                        "assistant" => "Assistant",
                        "system" => "System",
                        _ => msg.Role
                    };
                    
                    prompt.AppendLine($"{roleLabel}: {msg.Content}");
                }
                prompt.AppendLine();
            }

            // Add current message
            prompt.AppendLine("CURRENT QUESTION:");
            prompt.AppendLine($"Human: {userMessage}");
            prompt.AppendLine();
            prompt.AppendLine("Please answer based on the context information provided above.");

            return prompt.ToString();
        }

        public void ReloadConfiguration()
        {
            LoadConfiguration();
        }

        public void UpdateConfiguration(string serverUrl, string model)
        {
            _serverUrl = serverUrl;
            _model = model;

            // Save to settings
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings["OllamaUrl"] = serverUrl;
            settings["OllamaModel"] = model;
        }

        public void Dispose()
        {
            // Nothing to dispose for Ollama service
        }
    }
}
