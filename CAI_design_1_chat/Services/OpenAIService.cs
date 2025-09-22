using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using OpenAI.Chat;

namespace CAI_design_1_chat.Services
{
    public class OpenAIService : IAIService
    {
        private ChatClient? _chatClient;
        private string? _apiKey;
        private string? _model;
        private string? _organizationId;

        public string ProviderName => "OpenAI";

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_model);

        public OpenAIService()
        {
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            // Use the same keys as AISettingsDialog saves
            _apiKey = settings.TryGetValue("OpenAIKey", out var apiKey) ? apiKey?.ToString() : null;
            _model = settings.TryGetValue("OpenAIModel", out var model) ? model?.ToString() : "gpt-4";
            _organizationId = settings.TryGetValue("OpenAIOrg", out var org) ? org?.ToString() : null;

            // Debug logging
            System.Diagnostics.Debug.WriteLine($"OpenAI LoadConfiguration: ApiKey={(!string.IsNullOrEmpty(_apiKey) ? "SET" : "NULL")}, Model={_model}");
            System.Diagnostics.Debug.WriteLine($"OpenAI IsConfigured: {IsConfigured}");

            InitializeChatClient();
        }

        private void InitializeChatClient()
        {
            if (!string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_model))
            {
                _chatClient = new ChatClient(_model, _apiKey);
            }
        }

        public async Task<string> SendMessageAsync(string message, List<ChatMessage>? conversationHistory = null, CancellationToken cancellationToken = default)
        {
            // Convert to ContextData for backward compatibility
            var context = ContextData.FromMessages(conversationHistory ?? new List<ChatMessage>());
            return await SendMessageWithContextAsync(message, context, cancellationToken);
        }

        public async Task<string> SendMessageWithContextAsync(string message, ContextData context, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured || _chatClient == null)
            {
                throw new AIServiceException(ProviderName, "OpenAI service is not configured. Please set API key and model in settings.");
            }

            try
            {
                var messages = BuildChatMessagesWithContext(message, context);
                
                Console.WriteLine($"OpenAI request prepared:");
                Console.WriteLine($"  - Total messages: {messages.Count}");
                Console.WriteLine($"  - Context files: {context.FileCount}");
                Console.WriteLine($"  - Estimated tokens: ~{context.TotalTokens}");
                
                var completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
                var responseText = completion.Value.Content[0].Text ?? "No response from OpenAI";
                
                Console.WriteLine($"OpenAI response received: {responseText.Length} characters");
                return responseText;
            }
            catch (Exception ex)
            {
                throw new AIServiceException(ProviderName, $"OpenAI API error: {ex.Message}", "API_ERROR", ex);
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
            System.Diagnostics.Debug.WriteLine($"SendMessageStreamWithContextAsync called: IsConfigured={IsConfigured}, _chatClient={(_chatClient != null ? "SET" : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"ApiKey length: {(_apiKey?.Length ?? 0)}, Model: {_model}");
            
            if (!IsConfigured || _chatClient == null)
            {
                throw new AIServiceException(ProviderName, "OpenAI service is not configured. Please set API key and model in settings.");
            }

            try
            {
                var messages = BuildChatMessagesWithContext(message, context);
                
                Console.WriteLine($"OpenAI streaming request prepared:");
                Console.WriteLine($"  - Total messages: {messages.Count}");
                Console.WriteLine($"  - Context files: {context.FileCount}");
                Console.WriteLine($"  - Estimated tokens: ~{context.TotalTokens}");
                
                var completionUpdates = _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken);
                
                var fullResponse = new StringBuilder();
                
                await foreach (var completionUpdate in completionUpdates)
                {
                    if (completionUpdate.ContentUpdate.Count > 0)
                    {
                        var token = completionUpdate.ContentUpdate[0].Text;
                        if (!string.IsNullOrEmpty(token))
                        {
                            fullResponse.Append(token);
                            onTokenReceived?.Invoke(token);
                        }
                    }
                }

                var responseText = fullResponse.ToString();
                Console.WriteLine($"OpenAI streaming response completed: {responseText.Length} characters");
                return responseText;
            }
            catch (Exception ex)
            {
                throw new AIServiceException(ProviderName, $"OpenAI streaming error: {ex.Message}", "STREAMING_ERROR", ex);
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured || _chatClient == null)
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

        public Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            // Return common OpenAI models - the official client doesn't expose model listing easily
            var models = new List<string> 
            { 
                "gpt-4", 
                "gpt-4-turbo", 
                "gpt-4o", 
                "gpt-4o-mini", 
                "gpt-3.5-turbo" 
            };
            return Task.FromResult(models);
        }

        private List<OpenAI.Chat.ChatMessage> BuildChatMessages(string userMessage, List<ChatMessage> conversationHistory)
        {
            var messages = new List<OpenAI.Chat.ChatMessage>();
            
            // Add conversation history if provided
            foreach (var historyMessage in conversationHistory)
            {
                if (historyMessage.Role == "user")
                {
                    messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(historyMessage.Content));
                }
                else if (historyMessage.Role == "assistant")
                {
                    messages.Add(OpenAI.Chat.ChatMessage.CreateAssistantMessage(historyMessage.Content));
                }
                else if (historyMessage.Role == "system")
                {
                    messages.Add(OpenAI.Chat.ChatMessage.CreateSystemMessage(historyMessage.Content));
                }
            }
            
            // Add the current user message
            messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(userMessage));
            
            return messages;
        }

        private List<OpenAI.Chat.ChatMessage> BuildChatMessagesWithContext(string userMessage, ContextData context)
        {
            var messages = new List<OpenAI.Chat.ChatMessage>();
            
            // Add system message with assistant role and file context
            if (context.FileCount > 0 || !string.IsNullOrEmpty(context.AssistantRole))
            {
                var systemMessage = BuildSystemMessage(context);
                messages.Add(OpenAI.Chat.ChatMessage.CreateSystemMessage(systemMessage));
            }
            
            // Add conversation history
            foreach (var historyMessage in context.MessageHistory)
            {
                if (historyMessage.Role == "user")
                {
                    messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(historyMessage.Content));
                }
                else if (historyMessage.Role == "assistant")
                {
                    messages.Add(OpenAI.Chat.ChatMessage.CreateAssistantMessage(historyMessage.Content));
                }
                else if (historyMessage.Role == "system")
                {
                    messages.Add(OpenAI.Chat.ChatMessage.CreateSystemMessage(historyMessage.Content));
                }
            }
            
            // Add the current user message
            messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(userMessage));
            
            return messages;
        }

        private string BuildSystemMessage(ContextData context)
        {
            var systemMessage = new StringBuilder();
            
            // Add assistant role
            systemMessage.AppendLine(context.AssistantRole);
            
            // Add file context if available
            if (context.FileCount > 0)
            {
                systemMessage.AppendLine();
                systemMessage.AppendLine("You have access to the following files for context:");
                systemMessage.AppendLine();
                
                foreach (var file in context.Files.Where(f => !f.IsExcluded).OrderBy(f => f.OrderIndex))
                {
                    systemMessage.AppendLine($"## File: {file.DisplayName}");
                    if (!string.IsNullOrEmpty(file.OriginalName) && file.OriginalName != file.DisplayName)
                    {
                        systemMessage.AppendLine($"Original filename: {file.OriginalName}");
                    }
                    if (file.UseSummary)
                    {
                        systemMessage.AppendLine("(Summary provided)");
                    }
                    systemMessage.AppendLine();
                    systemMessage.AppendLine(file.Content);
                    systemMessage.AppendLine();
                    systemMessage.AppendLine("---");
                    systemMessage.AppendLine();
                }
                
                systemMessage.AppendLine("Please reference these files when relevant to answer the user's questions.");
            }
            
            return systemMessage.ToString();
        }

        public void ReloadConfiguration()
        {
            LoadConfiguration();
        }

        public void UpdateConfiguration(string apiKey, string model, string? organizationId = null)
        {
            _apiKey = apiKey;
            _model = model;
            _organizationId = organizationId;

            // Save to settings using the same keys as AISettingsDialog
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings["OpenAIKey"] = apiKey;
            settings["OpenAIModel"] = model;
            if (!string.IsNullOrEmpty(organizationId))
            {
                settings["OpenAIOrg"] = organizationId;
            }

            InitializeChatClient();
        }

        public void Dispose()
        {
            // ChatClient doesn't implement IDisposable in the current version
            _chatClient = null;
        }
    }
}
