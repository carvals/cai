# Feature to File Mapping

This document provides a comprehensive mapping between features and their implementation files, making it easier to navigate the codebase and understand where specific functionality is located.

## Core Architecture

### Data Models
| Feature | File | Key Classes/Functions |
|---------|------|----------------------|
| AI Model Data Structure | `/Models/AIModel.cs` | `AIModel` class with Id, DisplayName, Description, capabilities |
| Application Configuration | `/Models/AppConfig.cs` | Configuration settings |
| Entity Base | `/Models/Entity.cs` | Base entity class |
| File Processing Data | `/Models/FileData.cs` | File metadata, content, processing status |
| Custom Instructions | `/Models/PromptInstruction.cs` | AI prompt templates with metadata |

### Service Interfaces
| Feature | File | Key Classes/Functions |
|---------|------|----------------------|
| AI Service Contract | `/Services/IAIService.cs` | `IAIService` interface - SendMessageAsync, SendMessageStreamAsync |
| Model Provider Contract | `/Services/IModelProvider.cs` | `IModelProvider` interface - FetchAvailableModelsAsync, caching |
| Custom Instructions Contract | `/Services/IPromptInstructionService.cs` | Search, save, update, delete prompt instructions |

### Service Implementations
| Feature | File | Key Classes/Functions |
|---------|------|----------------------|
| OpenAI Integration | `/Services/OpenAIService.cs` | `OpenAIService` class - chat completions, streaming |
| OpenAI Model Provider | `/Services/OpenAIModelProvider.cs` | `OpenAIModelProvider` - dynamic model refresh, caching |
| File Processing & AI Summarization | `/Services/FileProcessingService.cs` | Text extraction, AI summarization with custom instructions |
| Custom Instructions Management | `/Services/PromptInstructionService.cs` | CRUD operations for prompt instructions |
| Database Management | `/Services/DatabaseService.cs` | SQLite operations, schema management |
| Debug Endpoints | `/Services/Endpoints/DebugHandler.cs` | Debug HTTP handlers |

## User Interface

### Main Application
| Feature | File | Key Classes/Functions |
|---------|------|----------------------|
| Main Layout | `/Presentation/MainPage.xaml` | 4-column grid, left panel, chat area |
| Main Logic | `/Presentation/MainPage.xaml.cs` | Panel animations, file upload, chat interface |
| File Processing Interface | `/Presentation/FileUploadPage.xaml` | Three-panel layout, drag & drop, AI summarization |
| File Processing Logic | `/Presentation/FileUploadPage.xaml.cs` | File handling, AI integration, prompt management |
| Application Shell | `/Presentation/Shell.xaml` | Navigation shell |
| Login Page | `/Presentation/LoginPage.xaml` | Authentication UI |

### Dialogs
| Feature | File | Key Classes/Functions |
|---------|------|----------------------|
| AI Settings Dialog | `/Presentation/Dialogs/AISettingsDialog.xaml` | Multi-provider configuration UI |
| AI Settings Logic | `/Presentation/Dialogs/AISettingsDialog.xaml.cs` | `RefreshOpenAIModels`, `RefreshAnthropicModels`, etc. |
| Prompt Search Dialog | `/Presentation/Dialogs/PromptSearchDialog.xaml` | Search existing instructions with filters |
| Prompt Search Logic | `/Presentation/Dialogs/PromptSearchDialog.xaml.cs` | Search, filter, preview, selection handling |
| Save Prompt Dialog | `/Presentation/Dialogs/SavePromptDialog.xaml` | Create new prompt instructions |
| Save Prompt Logic | `/Presentation/Dialogs/SavePromptDialog.xaml.cs` | Form validation, database saving |

## Feature Implementation Status

### ✅ Completed Features

#### AI-Powered File Processing with Custom Instructions
- **Files**: `/Services/FileProcessingService.cs`, `/Models/PromptInstruction.cs`, `/Services/PromptInstructionService.cs`
- **UI**: `/Presentation/FileUploadPage.xaml`, `/Presentation/Dialogs/PromptSearchDialog.xaml`, `/Presentation/Dialogs/SavePromptDialog.xaml`
- **Functions**:
  - `GenerateSummaryAsync()` - AI summarization with custom instructions
  - `SearchPromptsAsync()` - Search saved instructions with filters
  - `SavePromptAsync()` - Create new prompt templates
  - `IncrementUsageAsync()` - Track prompt usage statistics
- **AI Providers**: OpenAI and Ollama integration with comprehensive debug logging
- **Database**: SQLite schema for prompt_instructions table with full CRUD operations

#### Dynamic Model Refresh System
- **Files**: `/Services/IModelProvider.cs`, `/Services/OpenAIModelProvider.cs`, `/Models/AIModel.cs`
- **UI**: `/Presentation/Dialogs/AISettingsDialog.xaml.cs` (RefreshOpenAIModels)
- **Functions**: 
  - `FetchAvailableModelsAsync()` - API integration
  - `GetCachedModels()` - 24-hour caching
  - `RefreshOpenAIModels()` - UI event handler

#### OpenAI Integration
- **Files**: `/Services/OpenAIService.cs`, `/Services/IAIService.cs`
- **Functions**:
  - `SendMessageAsync()` - Non-streaming chat
  - `SendMessageStreamAsync()` - Streaming chat with token callbacks
  - `ReloadConfiguration()` - Dynamic config updates

#### AI Settings Dialog
- **Files**: `/Presentation/Dialogs/AISettingsDialog.xaml`, `/Presentation/Dialogs/AISettingsDialog.xaml.cs`
- **Functions**:
  - `RefreshOpenAIModels()` - OpenAI model refresh (fully functional)
  - `RefreshAnthropicModels()` - Placeholder ("Coming soon")
  - `RefreshGeminiModels()` - Placeholder ("Coming soon")
  - `RefreshMistralModels()` - Placeholder ("Coming soon")
  - `ShowLoadingDialog()`, `ShowSuccessDialog()`, `ShowErrorDialog()` - UX feedback

#### Main Application Layout
- **Files**: `/Presentation/MainPage.xaml`, `/Presentation/MainPage.xaml.cs`
- **Functions**:
  - `ToggleLeftPanelButton_Click()` - Panel collapse/expand
  - `AnimateLeftPanelTo()` - Smooth animations
  - `BtnUpload_Click()` - File upload dialog
  - `AISettingsButton_Click()` - Settings dialog launcher

#### Professional File Processing Interface
- **Files**: `/Presentation/FileUploadPage.xaml`, `/Presentation/FileUploadPage.xaml.cs`
- **Layout**: Three-panel Material Design interface (33%-50%-33%)
- **Functions**:
  - `ExtractTextButton_Click()` - Multi-format text extraction
  - `GenerateSummaryButton_Click()` - AI summarization with custom instructions
  - `SearchInstructionsButton_Click()` - Open prompt search modal
  - `SaveInstructionButton_Click()` - Save new prompt templates
  - `SummaryInstructionTextBox_TextChanged()` - Enable/disable save button
- **Features**: Drag & drop, live preview, raw/summary toggle, database integration

### Partially Implemented Features

#### AI Provider Support
- **OpenAI**: Fully implemented (chat + file summarization)
- **Ollama**: Fully implemented (chat + file summarization)
- **Anthropic**: UI ready, API integration pending
- **Gemini**: UI ready, API integration pending  
- **Mistral**: UI ready, API integration pending

### Pending Features

#### Chat Interface Enhancement
- **Files**: `/Presentation/MainPage.xaml.cs` (to be enhanced)
- **Missing Functions**:
  - Message bubble components
  - Conversation history display
  - Auto-scroll functionality
  - Copy message buttons

#### Data Models
- **Missing Files**:
  - `/Models/ChatMessage.cs` - User/Assistant message structure
  - `/Models/ChatSession.cs` - Conversation management
  - `/Models/AISettings.cs` - Settings persistence

#### Additional Services
- **Missing Files**:
  - `/Services/OllamaService.cs` - Local AI integration
  - `/Services/AnthropicService.cs` - Claude API
  - `/Services/GeminiService.cs` - Google AI
  - `/Services/MistralService.cs` - Mistral AI
  - `/Services/ChatPersistenceService.cs` - Message storage
  - `/Services/SettingsService.cs` - Configuration management

## Quick Navigation Guide

### To Add a New AI Provider:
1. Create service: `/Services/{Provider}Service.cs` implementing `IAIService`
2. Create model provider: `/Services/{Provider}ModelProvider.cs` implementing `IModelProvider`
3. Update UI: `/Presentation/Dialogs/AISettingsDialog.xaml.cs` - implement `Refresh{Provider}Models()`
4. Add configuration fields in `/Presentation/Dialogs/AISettingsDialog.xaml`

### To Enhance Chat Interface:
1. Update: `/Presentation/MainPage.xaml` - add message display components
2. Enhance: `/Presentation/MainPage.xaml.cs` - add message handling logic
3. Create: `/Presentation/Controls/MessageBubble.xaml` - message UI component

### To Add Data Persistence:
1. Create: `/Models/ChatMessage.cs` and `/Models/ChatSession.cs`
2. Implement: `/Services/ChatPersistenceService.cs`
3. Integrate with: `/Presentation/MainPage.xaml.cs`

## Architecture Patterns

### Interface-First Design
- All AI providers implement `IAIService` for consistency
- Model providers implement `IModelProvider` for extensibility
- Clear separation of concerns between UI and business logic

### Caching Strategy
- 24-hour model cache in `ApplicationData.Current.LocalSettings`
- JSON serialization for complex objects
- Graceful fallback to default models on cache miss

### Error Handling
- Comprehensive try-catch with user-friendly messages
- Loading states with progress indicators
- Fallback mechanisms for network failures

### UI Patterns
- Modal dialogs for long-running operations
- Progressive disclosure of information
- Consistent button layouts across providers
- Proper async/await patterns for non-blocking UI

This mapping serves as a living document that should be updated as new features are implemented or existing ones are modified.
