using System;
using System.IO;
using System.Threading.Tasks;
using CAI_design_1_chat.Models;
using System.Net.Http;
using System.Text;
using Microsoft.Data.Sqlite;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace CAI_design_1_chat.Services
{
    public class FileProcessingService
    {
        private readonly DatabaseService _databaseService;

        public FileProcessingService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<FileData> ProcessFileAsync(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var fileData = new FileData
            {
                Name = fileInfo.Name,
                FileSize = fileInfo.Length,
                FileType = fileInfo.Extension.ToLowerInvariant(),
                OriginalFilePath = filePath,
                ProcessingStatus = "processing"
            };

            try
            {
                // Extract content based on file type
                switch (fileData.FileType)
                {
                    case ".txt":
                        fileData.Content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                        fileData.ExtractionMethod = "text_read";
                        break;
                    case ".pdf":
                        fileData.Content = await ExtractPdfContentAsync(filePath);
                        fileData.ExtractionMethod = "pdf_extraction";
                        break;
                    case ".docx":
                        fileData.Content = await ExtractDocxContentAsync(filePath);
                        fileData.ExtractionMethod = "docx_extraction";
                        break;
                    default:
                        throw new NotSupportedException($"File type {fileData.FileType} is not supported");
                }

                // Don't generate summary automatically - let user generate it manually with custom instructions
                // fileData.Summary will be set when user clicks "Generate Summary" and then "Save to Database"

                fileData.ProcessingStatus = "completed";
                fileData.UpdatedAt = DateTime.Now;

                // Save to database
                await SaveFileDataAsync(fileData);

                return fileData;
            }
            catch (Exception ex)
            {
                fileData.ProcessingStatus = "failed";
                fileData.UpdatedAt = DateTime.Now;
                
                // Only create processing job record if file was already saved (has valid ID)
                if (fileData.Id > 0)
                {
                    try
                    {
                        await CreateProcessingJobAsync(fileData.Id, "file_processing", "failed", null, ex.Message);
                    }
                    catch (Exception jobEx)
                    {
                        // Log the job creation error but don't let it override the original exception
                        System.Diagnostics.Debug.WriteLine($"Failed to create processing job: {jobEx.Message}");
                    }
                }
                
                throw;
            }
        }

        private async Task<string> ExtractPdfContentAsync(string filePath)
        {
            try
            {
                var extractedText = new StringBuilder();
                
                // Run PDF extraction on background thread to avoid blocking UI
                await Task.Run(() =>
                {
                    using var pdfReader = new PdfReader(filePath);
                    using var pdfDocument = new PdfDocument(pdfReader);
                    
                    var strategy = new SimpleTextExtractionStrategy();
                    
                    for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
                    {
                        var page = pdfDocument.GetPage(pageNum);
                        var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                        
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            extractedText.AppendLine($"=== Page {pageNum} ===");
                            extractedText.AppendLine(pageText.Trim());
                            extractedText.AppendLine();
                        }
                    }
                });
                
                var result = extractedText.ToString().Trim();
                return string.IsNullOrEmpty(result) 
                    ? $"[PDF file '{Path.GetFileName(filePath)}' appears to be empty or contains no extractable text]"
                    : result;
            }
            catch (Exception ex)
            {
                return $"[Error extracting PDF content from '{Path.GetFileName(filePath)}': {ex.Message}]";
            }
        }

        private async Task<string> ExtractDocxContentAsync(string filePath)
        {
            // Placeholder for DOCX extraction
            // In a real implementation, you would use DocumentFormat.OpenXml
            await Task.Delay(100); // Simulate processing time
            return $"[DOCX Content Placeholder for {Path.GetFileName(filePath)}]\n\nDOCX extraction not yet implemented. Please install DocumentFormat.OpenXml package.";
        }

        public async Task<string> GenerateSummaryAsync(string content, string? customInstruction = null)
        {
            if (string.IsNullOrEmpty(content))
                return "No content available for summary.";

            // Use custom instruction or default
            var instruction = string.IsNullOrWhiteSpace(customInstruction) 
                ? "You are an executive assistant. Make a summary of the file and keep the original language of the file."
                : customInstruction;

            // Debug logging
            System.Diagnostics.Debug.WriteLine("=== AI SUMMARIZATION DEBUG ===");
            System.Diagnostics.Debug.WriteLine($"DEBUG: Content length: {content.Length} characters");
            System.Diagnostics.Debug.WriteLine($"DEBUG: Custom instruction: {instruction}");

            // Get AI provider from settings
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var selectedProvider = localSettings.Values["SelectedAIProvider"]?.ToString() ?? "Ollama";
            
            System.Diagnostics.Debug.WriteLine($"DEBUG: Selected AI Provider: {selectedProvider}");

            try
            {
                string summary;
                
                if (selectedProvider == "OpenAI")
                {
                    // OpenAI implementation
                    var aiService = new OpenAIService();
                    
                    // Add detailed debug info about OpenAI configuration
                    var apiKey = localSettings.Values.TryGetValue("OpenAIKey", out var key) ? key?.ToString() : null;
                    var model = localSettings.Values.TryGetValue("OpenAIModel", out var mdl) ? mdl?.ToString() : "gpt-4";
                    
                    System.Diagnostics.Debug.WriteLine($"DEBUG: OpenAI API Key: {(!string.IsNullOrEmpty(apiKey) ? "SET" : "NULL")}");
                    System.Diagnostics.Debug.WriteLine($"DEBUG: OpenAI Model: {model}");
                    System.Diagnostics.Debug.WriteLine($"DEBUG: OpenAI IsConfigured: {aiService.IsConfigured}");

                    if (aiService.IsConfigured)
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Using OpenAI service");
                        
                        // Create AI prompt
                        var prompt = $"{instruction}\n\nContent to summarize:\n{content}";
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Full prompt length: {prompt.Length} characters");
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Prompt preview: {prompt.Substring(0, Math.Min(200, prompt.Length))}...");

                        // Call OpenAI service
                        System.Diagnostics.Debug.WriteLine("DEBUG: Calling OpenAI service...");
                        summary = await aiService.SendMessageAsync(prompt);
                        
                        System.Diagnostics.Debug.WriteLine($"DEBUG: AI Response length: {summary.Length} characters");
                        System.Diagnostics.Debug.WriteLine($"DEBUG: AI Response preview: {summary.Substring(0, Math.Min(200, summary.Length))}...");
                        System.Diagnostics.Debug.WriteLine("=== AI SUMMARIZATION SUCCESS ===");
                        
                        return summary;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG: OpenAI Service not configured");
                        System.Diagnostics.Debug.WriteLine("DEBUG: Please configure OpenAI API key in AI Settings");
                    }
                }
                else if (selectedProvider == "Ollama")
                {
                    // Ollama implementation (same as MainPage.xaml.cs)
                    var serverUrl = localSettings.Values["OllamaUrl"]?.ToString() ?? "http://localhost:11434";
                    var model = localSettings.Values["OllamaModel"]?.ToString() ?? "llama2";
                    
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Ollama Server URL: {serverUrl}");
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Ollama Model: {model}");

                    using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                    
                    // Create AI prompt
                    var prompt = $"{instruction}\n\nContent to summarize:\n{content}";
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Full prompt length: {prompt.Length} characters");
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Prompt preview: {prompt.Substring(0, Math.Min(200, prompt.Length))}...");
                    
                    var requestBody = new
                    {
                        model = model,
                        prompt = prompt,
                        stream = false
                    };

                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var content_http = new System.Net.Http.StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                    
                    System.Diagnostics.Debug.WriteLine("DEBUG: Calling Ollama service...");
                    var response = await httpClient.PostAsync($"{serverUrl}/api/generate", content_http);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Ollama API error: {response.StatusCode} - {response.ReasonPhrase}");
                        throw new Exception($"Ollama API error: {response.StatusCode} - {response.ReasonPhrase}");
                    }

                    var responseText = await response.Content.ReadAsStringAsync();
                    var responseData = System.Text.Json.JsonSerializer.Deserialize<OllamaGenerateResponse>(responseText);
                    summary = responseData?.response ?? "No response from Ollama";
                    
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Ollama Response length: {summary.Length} characters");
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Ollama Response preview: {summary.Substring(0, Math.Min(200, summary.Length))}...");
                    System.Diagnostics.Debug.WriteLine("=== AI SUMMARIZATION SUCCESS ===");
                    
                    return summary;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DEBUG: Unsupported AI provider: {selectedProvider}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: AI Service error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"DEBUG: Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine("=== AI SUMMARIZATION ERROR ===");
            }

            // Fallback to basic summary with debug info
            var basicSummary = GenerateBasicSummary(content);
            System.Diagnostics.Debug.WriteLine("DEBUG: Using fallback basic summary");
            System.Diagnostics.Debug.WriteLine("=== AI SUMMARIZATION FALLBACK ===");
            
            return $"[AI Summary with instruction: \"{instruction}\"]\n\n{basicSummary}\n\nNote: AI service not available. Using basic summary as fallback.";
        }

        private string GenerateBasicSummary(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "No content available for summary.";

            // Basic summary generation - take first 200 characters
            var summary = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
            
            // Count basic stats
            var wordCount = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var lineCount = content.Split('\n').Length;
            
            return $"Document contains {wordCount} words across {lineCount} lines.\n\nPreview: {summary}";
        }

        private async Task SaveFileDataAsync(FileData fileData)
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO file_data (name, content, summary, owner, date_created, file_type, file_size, 
                                     processing_status, extraction_method, original_file_path, is_in_context, 
                                     use_summary_in_context, context_order, is_excluded_temporarily, created_at, updated_at)
                VALUES (@name, @content, @summary, @owner, @dateCreated, @fileType, @fileSize, 
                        @processingStatus, @extractionMethod, @originalFilePath, @isInContext, 
                        @useSummaryInContext, @contextOrder, @isExcludedTemporarily, @createdAt, @updatedAt);
                SELECT last_insert_rowid();";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@name", fileData.Name);
            command.Parameters.AddWithValue("@content", fileData.Content ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@summary", fileData.Summary ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@owner", fileData.Owner ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@dateCreated", fileData.DateCreated);
            command.Parameters.AddWithValue("@fileType", fileData.FileType ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@fileSize", fileData.FileSize);
            command.Parameters.AddWithValue("@processingStatus", fileData.ProcessingStatus);
            command.Parameters.AddWithValue("@extractionMethod", fileData.ExtractionMethod ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@originalFilePath", fileData.OriginalFilePath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isInContext", fileData.IsInContext);
            command.Parameters.AddWithValue("@useSummaryInContext", fileData.UseSummaryInContext);
            command.Parameters.AddWithValue("@contextOrder", fileData.ContextOrder);
            command.Parameters.AddWithValue("@isExcludedTemporarily", fileData.IsExcludedTemporarily);
            command.Parameters.AddWithValue("@createdAt", fileData.CreatedAt);
            command.Parameters.AddWithValue("@updatedAt", fileData.UpdatedAt);

            var result = await command.ExecuteScalarAsync();
            fileData.Id = Convert.ToInt32(result);
        }

        public async Task UpdateFileDataAsync(FileData fileData)
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                UPDATE file_data 
                SET content = @content, summary = @summary, processing_status = @processingStatus, 
                    extraction_method = @extractionMethod, updated_at = @updatedAt
                WHERE id = @id";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", fileData.Id);
            command.Parameters.AddWithValue("@content", fileData.Content ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@summary", fileData.Summary ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@processingStatus", fileData.ProcessingStatus);
            command.Parameters.AddWithValue("@extractionMethod", fileData.ExtractionMethod ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);

            await command.ExecuteNonQueryAsync();
        }

        private async Task CreateProcessingJobAsync(int fileId, string jobType, string status, string? parameters, string? errorMessage)
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO processing_jobs (file_id, job_type, status, error_message, created_at)
                VALUES (@fileId, @jobType, @status, @errorMessage, @createdAt)";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@fileId", fileId);
            command.Parameters.AddWithValue("@jobType", jobType);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@errorMessage", errorMessage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@createdAt", DateTime.Now);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<FileData?> GetFileDataAsync(int fileId)
        {
            using var connection = new SqliteConnection(_databaseService.GetConnectionString());
            await connection.OpenAsync();

            var sql = "SELECT * FROM file_data WHERE id = @fileId";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@fileId", fileId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new FileData
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Content = reader.IsDBNull(reader.GetOrdinal("content")) ? null : reader.GetString(reader.GetOrdinal("content")),
                    Summary = reader.IsDBNull(reader.GetOrdinal("summary")) ? null : reader.GetString(reader.GetOrdinal("summary")),
                    Owner = reader.IsDBNull(reader.GetOrdinal("owner")) ? null : reader.GetString(reader.GetOrdinal("owner")),
                    DateCreated = reader.GetDateTime(reader.GetOrdinal("date_created")),
                    FileType = reader.IsDBNull(reader.GetOrdinal("file_type")) ? null : reader.GetString(reader.GetOrdinal("file_type")),
                    FileSize = reader.GetInt64(reader.GetOrdinal("file_size")),
                    ProcessingStatus = reader.GetString(reader.GetOrdinal("processing_status")),
                    ExtractionMethod = reader.IsDBNull(reader.GetOrdinal("extraction_method")) ? null : reader.GetString(reader.GetOrdinal("extraction_method")),
                    OriginalFilePath = reader.IsDBNull(reader.GetOrdinal("original_file_path")) ? null : reader.GetString(reader.GetOrdinal("original_file_path")),
                    IsInContext = reader.GetBoolean(reader.GetOrdinal("is_in_context")),
                    UseSummaryInContext = reader.GetBoolean(reader.GetOrdinal("use_summary_in_context")),
                    ContextOrder = reader.GetInt32(reader.GetOrdinal("context_order")),
                    IsExcludedTemporarily = reader.GetBoolean(reader.GetOrdinal("is_excluded_temporarily")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                };
            }

            return null;
        }
    }

    // Data model for Ollama response
    public class OllamaGenerateResponse
    {
        public string? response { get; set; }
        public bool done { get; set; }
    }
}
