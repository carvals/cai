using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CAI_design_1_chat.Services
{
    public interface IAIService
    {
        /// <summary>
        /// Gets the name of the AI provider (e.g., "OpenAI", "Ollama", "Anthropic")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Gets whether the service is properly configured and ready to use
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Sends a message to the AI and returns the response
        /// </summary>
        /// <param name="message">The user's message</param>
        /// <param name="conversationHistory">Previous messages in the conversation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The AI's response</returns>
        Task<string> SendMessageAsync(string message, List<ChatMessage>? conversationHistory = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the AI and streams the response word by word
        /// </summary>
        /// <param name="message">The user's message</param>
        /// <param name="onTokenReceived">Callback for each token/word received</param>
        /// <param name="conversationHistory">Previous messages in the conversation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The complete response when streaming is finished</returns>
        Task<string> SendMessageStreamAsync(string message, Action<string> onTokenReceived, List<ChatMessage>? conversationHistory = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests the connection to the AI service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if connection is successful</returns>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available models for this provider
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of available model names</returns>
        Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
    }

    public class ChatMessage
    {
        public string Role { get; set; } // "user", "assistant", "system"
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class AIServiceException : Exception
    {
        public string ProviderName { get; }
        public string ErrorCode { get; }
        public string? StatusCode { get; }

        public AIServiceException(string providerName, string message, string? errorCode = null, Exception? innerException = null) 
            : base(message, innerException)
        {
            ProviderName = providerName;
            ErrorCode = errorCode ?? "UnknownError";
            StatusCode = errorCode;
        }
        
        public AIServiceException(string providerName, string message, string? errorCode, string? statusCode, Exception? innerException = null) 
            : base(message, innerException)
        {
            ProviderName = providerName;
            ErrorCode = errorCode ?? "UnknownError";
            StatusCode = statusCode;
        }
    }
}
