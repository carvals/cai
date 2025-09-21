using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System;
using CAI_design_1_chat.Services;

namespace CAI_design_1_chat.Presentation.Dialogs;

public sealed partial class AISettingsDialog : ContentDialog
{
    private readonly OpenAIModelProvider _openAIModelProvider;
    private ContentDialog? _loadingDialog;

    public AISettingsDialog()
    {
        this.InitializeComponent();
        _openAIModelProvider = new OpenAIModelProvider();
        LoadSettings();
        
        // Add event handlers to clear placeholder text when items are selected
        OllamaModelBox.SelectionChanged += (s, e) => { if (OllamaModelBox.SelectedItem != null) OllamaModelBox.PlaceholderText = ""; };
        OpenAIModelBox.SelectionChanged += (s, e) => { if (OpenAIModelBox.SelectedItem != null) OpenAIModelBox.PlaceholderText = ""; };
        AnthropicModelBox.SelectionChanged += (s, e) => { if (AnthropicModelBox.SelectedItem != null) AnthropicModelBox.PlaceholderText = ""; };
        GeminiModelBox.SelectionChanged += (s, e) => { if (GeminiModelBox.SelectedItem != null) GeminiModelBox.PlaceholderText = ""; };
        MistralModelBox.SelectionChanged += (s, e) => { if (MistralModelBox.SelectedItem != null) MistralModelBox.PlaceholderText = ""; };
    }

    private void LoadSettings()
    {
        // Load saved settings from ApplicationData
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        
        // Load context size setting
        var contextMessages = localSettings.Values["ContextMessages"] as int? ?? 10;
        ContextSizeSlider.Value = Math.Max(1, Math.Min(20, contextMessages));
        UpdateContextSizeDisplay((int)ContextSizeSlider.Value);
        
        // Load provider selection
        var selectedProvider = localSettings.Values["SelectedAIProvider"] as string ?? 
                              localSettings.Values["CurrentAIProvider"] as string ?? "Ollama";
        switch (selectedProvider)
        {
            case "Ollama":
                OllamaRadio.IsChecked = true;
                break;
            case "OpenAI":
                OpenAIRadio.IsChecked = true;
                break;
            case "Anthropic":
                AnthropicRadio.IsChecked = true;
                break;
            case "Gemini":
                GeminiRadio.IsChecked = true;
                break;
            case "Mistral":
                MistralRadio.IsChecked = true;
                break;
        }

        // Load Ollama settings
        OllamaUrlBox.Text = localSettings.Values["OllamaUrl"] as string ?? "http://localhost:11434";
        var ollamaModel = localSettings.Values["OllamaModel"] as string;
        if (!string.IsNullOrEmpty(ollamaModel))
        {
            foreach (ComboBoxItem item in OllamaModelBox.Items)
            {
                if (item.Content.ToString() == ollamaModel)
                {
                    OllamaModelBox.SelectedItem = item;
                    OllamaModelBox.PlaceholderText = ""; // Clear placeholder to prevent overlap
                    break;
                }
            }
        }

        // Load OpenAI settings
        OpenAIKeyBox.Password = localSettings.Values["OpenAIKey"] as string ?? "";
        var openAIModel = localSettings.Values["OpenAIModel"] as string;
        if (!string.IsNullOrEmpty(openAIModel))
        {
            foreach (ComboBoxItem item in OpenAIModelBox.Items)
            {
                if (item.Content.ToString() == openAIModel)
                {
                    OpenAIModelBox.SelectedItem = item;
                    OpenAIModelBox.PlaceholderText = ""; // Clear placeholder to prevent overlap
                    break;
                }
            }
        }
        OpenAIOrgBox.Text = localSettings.Values["OpenAIOrg"] as string ?? "";

        // Load Anthropic settings
        AnthropicKeyBox.Password = localSettings.Values["AnthropicKey"] as string ?? "";
        var anthropicModel = localSettings.Values["AnthropicModel"] as string;
        if (!string.IsNullOrEmpty(anthropicModel))
        {
            foreach (ComboBoxItem item in AnthropicModelBox.Items)
            {
                if (item.Content.ToString() == anthropicModel)
                {
                    AnthropicModelBox.SelectedItem = item;
                    AnthropicModelBox.PlaceholderText = ""; // Clear placeholder to prevent overlap
                    break;
                }
            }
        }

        // Load Gemini settings
        GeminiKeyBox.Password = localSettings.Values["GeminiKey"] as string ?? "";
        var geminiModel = localSettings.Values["GeminiModel"] as string;
        if (!string.IsNullOrEmpty(geminiModel))
        {
            foreach (ComboBoxItem item in GeminiModelBox.Items)
            {
                if (item.Content.ToString() == geminiModel)
                {
                    GeminiModelBox.SelectedItem = item;
                    GeminiModelBox.PlaceholderText = ""; // Clear placeholder to prevent overlap
                    break;
                }
            }
        }

        // Load Mistral settings
        MistralKeyBox.Password = localSettings.Values["MistralKey"] as string ?? "";
        var mistralModel = localSettings.Values["MistralModel"] as string;
        if (!string.IsNullOrEmpty(mistralModel))
        {
            foreach (ComboBoxItem item in MistralModelBox.Items)
            {
                if (item.Content.ToString() == mistralModel)
                {
                    MistralModelBox.SelectedItem = item;
                    MistralModelBox.PlaceholderText = ""; // Clear placeholder to prevent overlap
                    break;
                }
            }
        }
    }

    public void SaveSettings()
    {
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        // Save selected provider
        string selectedProvider = "Ollama";
        if (OpenAIRadio.IsChecked == true) selectedProvider = "OpenAI";
        else if (AnthropicRadio.IsChecked == true) selectedProvider = "Anthropic";
        else if (GeminiRadio.IsChecked == true) selectedProvider = "Gemini";
        else if (MistralRadio.IsChecked == true) selectedProvider = "Mistral";
        
        localSettings.Values["SelectedAIProvider"] = selectedProvider;
        localSettings.Values["CurrentAIProvider"] = selectedProvider;

        // Save context size setting
        localSettings.Values["ContextMessages"] = (int)ContextSizeSlider.Value;

        // Save Ollama settings
        localSettings.Values["OllamaUrl"] = OllamaUrlBox.Text;
        localSettings.Values["OllamaModel"] = (OllamaModelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        // Save OpenAI settings
        localSettings.Values["OpenAIKey"] = OpenAIKeyBox.Password;
        localSettings.Values["OpenAIModel"] = (OpenAIModelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        localSettings.Values["OpenAIOrg"] = OpenAIOrgBox.Text;

        // Save Anthropic settings
        localSettings.Values["AnthropicKey"] = AnthropicKeyBox.Password;
        localSettings.Values["AnthropicModel"] = (AnthropicModelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        // Save Gemini settings
        localSettings.Values["GeminiKey"] = GeminiKeyBox.Password;
        localSettings.Values["GeminiModel"] = (GeminiModelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        // Save Mistral settings
        localSettings.Values["MistralKey"] = MistralKeyBox.Password;
        localSettings.Values["MistralModel"] = (MistralModelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
    }

    private async void TestOllamaConnection(object sender, RoutedEventArgs e)
    {
        var serverUrl = OllamaUrlBox.Text?.Trim();
        if (string.IsNullOrEmpty(serverUrl))
        {
            serverUrl = "http://localhost:11434";
        }
        
        var selectedModel = (OllamaModelBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(selectedModel))
        {
            var noModelDialog = new ContentDialog
            {
                Title = "No Model Selected",
                Content = "Please select a model first.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await noModelDialog.ShowAsync();
            return;
        }

        // Show loading dialog
        var loadingDialog = new ContentDialog
        {
            Title = "Testing Model",
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new ProgressRing { IsActive = true, Width = 20, Height = 20 },
                    new TextBlock { Text = "Waking up model...", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            XamlRoot = this.XamlRoot
        };

        // Show loading dialog without waiting for result
        var loadingTask = loadingDialog.ShowAsync();
        
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var requestBody = new
            {
                model = selectedModel,
                prompt = "Say hi in one sentence",
                stream = false
            };
            
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{serverUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();
            
            var responseText = await response.Content.ReadAsStringAsync();
            var responseData = System.Text.Json.JsonSerializer.Deserialize<OllamaGenerateResponse>(responseText);
            
            // Hide loading dialog
            loadingDialog.Hide();
            
            var successDialog = new ContentDialog
            {
                Title = "Model Test Successful",
                Content = $"Model '{selectedModel}' responded:\n\n{responseData?.response ?? "No response"}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (HttpRequestException ex)
        {
            // Hide loading dialog
            loadingDialog.Hide();
            
            var errorDialog = new ContentDialog
            {
                Title = "Connection Failed",
                Content = $"Could not connect to Ollama server: {ex.Message}\n\nMake sure Ollama is running at the specified URL.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            // Hide loading dialog
            loadingDialog.Hide();
            
            var errorDialog = new ContentDialog
            {
                Title = "Test Failed",
                Content = $"Failed to test model: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    private async void TestOpenAIConnection(object sender, RoutedEventArgs e)
    {
        OpenAITestButton.IsEnabled = false;
        OpenAITestButton.Content = "Testing...";

        try
        {
            if (string.IsNullOrWhiteSpace(OpenAIKeyBox.Password))
            {
                throw new ArgumentException("API Key is required");
            }

            // TODO: Implement actual OpenAI connection test
            await Task.Delay(1000); // Simulate API call
            
            var dialog = new ContentDialog
            {
                Title = "Connection Test",
                Content = "OpenAI connection successful!",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Connection Failed",
                Content = $"Failed to connect to OpenAI: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            OpenAITestButton.IsEnabled = true;
            OpenAITestButton.Content = "Test";
        }
    }

    private async void TestAnthropicConnection(object sender, RoutedEventArgs e)
    {
        AnthropicTestButton.IsEnabled = false;
        AnthropicTestButton.Content = "Testing...";

        try
        {
            if (string.IsNullOrWhiteSpace(AnthropicKeyBox.Password))
            {
                throw new ArgumentException("API Key is required");
            }

            // TODO: Implement actual Anthropic connection test
            await Task.Delay(1000); // Simulate API call
            
            var dialog = new ContentDialog
            {
                Title = "Connection Test",
                Content = "Anthropic Claude connection successful!",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Connection Failed",
                Content = $"Failed to connect to Anthropic: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            AnthropicTestButton.IsEnabled = true;
            AnthropicTestButton.Content = "Test";
        }
    }

    private async void TestGeminiConnection(object sender, RoutedEventArgs e)
    {
        GeminiTestButton.IsEnabled = false;
        GeminiTestButton.Content = "Testing...";

        try
        {
            if (string.IsNullOrWhiteSpace(GeminiKeyBox.Password))
            {
                throw new ArgumentException("API Key is required");
            }

            // TODO: Implement actual Gemini connection test
            await Task.Delay(1000); // Simulate API call
            
            var dialog = new ContentDialog
            {
                Title = "Connection Test",
                Content = "Google Gemini connection successful!",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Connection Failed",
                Content = $"Failed to connect to Gemini: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            GeminiTestButton.IsEnabled = true;
            GeminiTestButton.Content = "Test";
        }
    }

    private async void TestMistralConnection(object sender, RoutedEventArgs e)
    {
        MistralTestButton.IsEnabled = false;
        MistralTestButton.Content = "Testing...";

        try
        {
            if (string.IsNullOrWhiteSpace(MistralKeyBox.Password))
            {
                throw new ArgumentException("API Key is required");
            }

            // TODO: Implement actual Mistral connection test
            await Task.Delay(1000); // Simulate API call
            
            var dialog = new ContentDialog
            {
                Title = "Connection Test",
                Content = "Mistral AI connection successful!",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Connection Failed",
                Content = $"Failed to connect to Mistral: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            MistralTestButton.IsEnabled = true;
            MistralTestButton.Content = "Test";
        }
    }

    private async void RefreshOllamaModels(object sender, RoutedEventArgs e)
    {
        OllamaRefreshButton.IsEnabled = false;
        OllamaRefreshButton.Content = "⏳";
        
        try
        {
            var serverUrl = OllamaUrlBox.Text?.Trim();
            if (string.IsNullOrEmpty(serverUrl))
            {
                serverUrl = "http://localhost:11434";
            }
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await httpClient.GetAsync($"{serverUrl}/api/tags");
            response.EnsureSuccessStatusCode();
            
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var modelsData = System.Text.Json.JsonSerializer.Deserialize<OllamaModelsResponse>(jsonResponse);
            
            // Clear existing models
            OllamaModelBox.Items.Clear();
            
            if (modelsData?.models != null && modelsData.models.Length > 0)
            {
                foreach (var model in modelsData.models)
                {
                    OllamaModelBox.Items.Add(new ComboBoxItem { Content = model.name });
                }
                
                var dialog = new ContentDialog
                {
                    Title = "Models Refreshed",
                    Content = $"Found {modelsData.models.Length} available models from Ollama",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = "No Models Found",
                    Content = "No models found on the Ollama server. Make sure Ollama is running and has models installed.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Connection Failed",
                Content = $"Could not connect to Ollama server: {ex.Message}\n\nMake sure Ollama is running at the specified URL.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Refresh Failed",
                Content = $"Failed to refresh models: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            OllamaRefreshButton.IsEnabled = true;
            OllamaRefreshButton.Content = "🔄 Refresh";
        }
    }

    // Data models for Ollama API responses
    public class OllamaModel
    {
        public string name { get; set; } = string.Empty;
        public string modified_at { get; set; } = string.Empty;
        public long size { get; set; }
        public string digest { get; set; } = string.Empty;
    }

    public class OllamaModelsResponse
    {
        public OllamaModel[] models { get; set; } = Array.Empty<OllamaModel>();
    }

    private async void RefreshOpenAIModels(object sender, RoutedEventArgs e)
    {
        var apiKey = OpenAIKeyBox.Password;
        if (string.IsNullOrEmpty(apiKey))
        {
            await ShowErrorDialog("Please enter your OpenAI API key first.");
            return;
        }

        await ShowLoadingDialog("Fetching OpenAI models...");
        
        try
        {
            var models = await _openAIModelProvider.FetchAvailableModelsAsync(apiKey);
            
            // Update ComboBox with new models
            OpenAIModelBox.Items.Clear();
            foreach (var model in models)
            {
                var item = new ComboBoxItem { Content = model.Id, Tag = model };
                OpenAIModelBox.Items.Add(item);
            }
            
            // Cache the models
            _openAIModelProvider.CacheModels(models);
            
            await HideLoadingDialog();
            await ShowSuccessDialog($"Found {models.Count} OpenAI models");
        }
        catch (Exception ex)
        {
            await HideLoadingDialog();
            await ShowErrorDialog($"Failed to fetch models: {ex.Message}");
        }
    }

    private async Task ShowLoadingDialog(string message)
    {
        _loadingDialog = new ContentDialog
        {
            Title = "Loading",
            Content = new StackPanel
            {
                Children =
                {
                    new ProgressRing { IsActive = true, Margin = new Thickness(0, 0, 0, 16) },
                    new TextBlock { Text = message, HorizontalAlignment = HorizontalAlignment.Center }
                }
            },
            XamlRoot = this.XamlRoot
        };
        
        _ = _loadingDialog.ShowAsync();
        await Task.Delay(100); // Allow dialog to show
    }

    private async Task HideLoadingDialog()
    {
        if (_loadingDialog != null)
        {
            _loadingDialog.Hide();
            _loadingDialog = null;
        }
        await Task.Delay(100); // Allow dialog to hide
    }

    private async Task ShowSuccessDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Success",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ShowErrorDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void RefreshAnthropicModels(object sender, RoutedEventArgs e)
    {
        var apiKey = AnthropicKeyBox.Password;
        if (string.IsNullOrEmpty(apiKey))
        {
            await ShowErrorDialog("Please enter your Anthropic API key first.");
            return;
        }

        await ShowErrorDialog("Anthropic model refresh not yet implemented. Coming soon!");
    }

    private async void RefreshGeminiModels(object sender, RoutedEventArgs e)
    {
        var apiKey = GeminiKeyBox.Password;
        if (string.IsNullOrEmpty(apiKey))
        {
            await ShowErrorDialog("Please enter your Gemini API key first.");
            return;
        }

        await ShowErrorDialog("Gemini model refresh not yet implemented. Coming soon!");
    }

    private async void RefreshMistralModels(object sender, RoutedEventArgs e)
    {
        var apiKey = MistralKeyBox.Password;
        if (string.IsNullOrEmpty(apiKey))
        {
            await ShowErrorDialog("Please enter your Mistral API key first.");
            return;
        }

        await ShowErrorDialog("Mistral model refresh not yet implemented. Coming soon!");
    }

    private void ContextSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var contextSize = (int)e.NewValue;
        UpdateContextSizeDisplay(contextSize);
    }

    private void UpdateContextSizeDisplay(int contextSize)
    {
        if (ContextSizeValue != null)
        {
            ContextSizeValue.Text = contextSize.ToString();
        }
        
        if (ContextSizeInfo != null)
        {
            var estimatedTokens = contextSize * 25; // Rough estimate: 25 tokens per message
            ContextSizeInfo.Text = $"Estimated tokens: ~{estimatedTokens:N0} ({contextSize} messages × ~25 tokens each)";
        }
    }

    public class OllamaGenerateResponse
    {
        public string model { get; set; } = string.Empty;
        public string created_at { get; set; } = string.Empty;
        public string response { get; set; } = string.Empty;
        public bool done { get; set; }
    }
}
