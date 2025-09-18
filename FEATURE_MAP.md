# Feature to File Mapping

This feature map provides a comprehensive overview of the CAI Design 1 Chat application's capabilities, implementation status, and architectural organization. Use this as a reference for understanding the system's current state and planning future enhancements.

## Development Insights and Lessons

### Key Success Factors
1. **Comprehensive Debug Logging**: Essential for troubleshooting AI integration and database operations
2. **Schema-Code Alignment**: Critical for preventing SQLite constraint violations
3. **State Management**: Tracking objects throughout UI workflows prevents data inconsistencies
4. **Defensive Programming**: Input validation and precondition checks prevent cascade failures
5. **Error Recovery**: Graceful handling with user feedback maintains application stability

### Common Pitfalls Avoided
1. **Database Schema Mismatches**: Always verify schema before writing SQL operations
2. **Foreign Key Violations**: Validate IDs before creating dependent records
3. **File Type Inconsistencies**: Ensure UI and service layers support same file types
4. **Data Duplication**: Use update operations for existing records, not insert
5. **Silent Failures**: Comprehensive logging reveals hidden issues during development

### Modern Development Practices Applied
- **Async/Await Patterns**: Non-blocking operations for better UX
- **Resource Management**: Proper disposal of connections and HTTP clients
- **Separation of Concerns**: Clear boundaries between extraction and summarization
- **Configuration Validation**: Runtime checks for AI provider settings
- **Command-Line Debugging**: SQLite CLI for rapid database inspection
- **Structured Error Handling**: Categorized exceptions with specific recovery strategies

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
| File Processing Interface | `/Presentation/FileUploadPage.xaml` | **REDESIGNED**: Consolidated left panel layout with vertical workflow |
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

### ✅ Fully Implemented
- File upload with drag & drop support
- PDF text extraction using iText7
- Text and Markdown file support (.txt, .md)
- AI summarization with OpenAI and Ollama
- Custom prompt instruction system
- SQLite database integration with schema validation
- Material Design UI consistency
- Comprehensive debug logging
- Modal dialog system for prompt management
- State management and data consistency
- Error handling with graceful recovery
- Database operation debugging tools
- **FileUploadPage Layout Redesign (Phase 12)**: Consolidated left panel with vertical workflow, removed redundant elements

### 🔄 Partially Implemented
- DOCX file support (placeholder implementation)
- Additional AI providers (Anthropic, Gemini, Mistral - UI only)

### 📋 Planned Features
- OCR support for scanned documents
- File export functionality
- Advanced prompt template management
- Collaborative features
- Performance optimizations
- Unit testing framework
- Automated schema migration

### 🐛 Recently Fixed Issues
- SQLite schema alignment errors
- Foreign key constraint violations
- File type support inconsistencies
- Summary generation data mismatches
- UI-database state synchronization
- Debug logging and error tracking

## Quick Reference

### Key Files by Function
- **File Processing**: `FileProcessingService.cs`, `FileUploadPage.xaml.cs`
- **AI Integration**: `OpenAIService.cs`, `IAIService.cs`
- **Database**: `DatabaseService.cs`, `schema.sql`
- **Prompts**: `PromptInstructionService.cs`, `PromptInstruction.cs`
- **UI Dialogs**: `PromptSearchDialog.xaml.cs`, `SavePromptDialog.xaml.cs`

### Essential Commands
```bash
# Build and run
dotnet build && dotnet run --project CAI_design_1_chat

# Database inspection
sqlite3 database.db ".schema file_data"
sqlite3 database.db "SELECT id, name, processing_status FROM file_data;"
sqlite3 database.db "SELECT * FROM prompt_instructions ORDER BY usage_count DESC;"

# Debug and troubleshooting
sqlite3 database.db "PRAGMA table_info(processing_jobs);"
sqlite3 database.db "SELECT * FROM processing_jobs WHERE status='failed';"
sqlite3 database.db "PRAGMA foreign_key_check;"

# Watch for debug markers in console:
# - "=== AI SUMMARIZATION DEBUG ==="
# - "DEBUG: Selected AI Provider:"
# - "=== AI SUMMARIZATION SUCCESS ==="
```

### Troubleshooting Checklist
1. **SQLite Errors**: Check column names in INSERT/UPDATE statements
2. **Foreign Key Errors**: Validate file_id > 0 before dependent operations
3. **File Type Errors**: Ensure ProcessFileAsync supports all UI file types
4. **Summary Mismatch**: Use UpdateFileDataAsync for existing files
5. **AI Errors**: Check provider configuration and debug logs

## Core Features

#### 1. AI-Powered File Processing 🤖
- **File Upload**: Drag & drop interface with support for PDF, TXT, MD files (DOCX placeholder)
- **Text Extraction**: Automated content extraction with method tracking
- **AI Summarization**: Custom instruction-based summaries using OpenAI or Ollama
- **Fallback Mechanisms**: Basic summaries when AI services unavailable
- **State Management**: Track FileData objects throughout processing workflow
- **Data Consistency**: Update existing records instead of creating duplicates

#### 2. Prompt Instruction Management 📝
- **Custom Prompts**: User-defined instructions for different document types
- **Search & Filter**: Find existing prompts by type, language, or content
- **Usage Tracking**: Monitor prompt popularity and effectiveness
- **System Prompts**: Pre-defined templates for common use cases
- **CRUD Operations**: Complete create, read, update, delete functionality

#### 3. Multi-Provider AI Integration 🔗
- **OpenAI Support**: GPT models with API key configuration
- **Ollama Integration**: Local LLM support with custom server URLs
- **Provider Switching**: Dynamic selection between AI services
- **Debug Logging**: Comprehensive request/response tracking with structured markers
- **Error Handling**: Graceful fallback with detailed error reporting

#### 4. Database Management 🗄️
- **SQLite Integration**: Lightweight, embedded database solution with schema validation
- **File Metadata**: Complete tracking of processing history
- **Prompt Library**: Persistent storage of custom instructions
- **Usage Analytics**: Track prompt effectiveness and file processing stats
- **Foreign Key Management**: Proper constraint handling and validation
- **Debug Tools**: Command-line inspection and troubleshooting capabilities

## Recent Updates and Bug Fixes

### Phase 12 Completion - FileUploadPage Layout Redesign ✅
- **Left Panel Consolidation**: Moved all file upload and processing actions to a single vertical workflow
- **UI Simplification**: Removed AI model indicator, robot icon, and redundant AI Settings button
- **Visual Hierarchy**: Added visible dividers (2px MaterialOutlineBrush) between major sections
- **Button Optimization**: Reduced processing button size and spacing for better UX
- **Layout Reorganization**: Upload File → Browse Button → File Info → Processing Actions → Summary Instructions → Processing Status
- **Right Panel Preparation**: Emptied right panel for future content preview functionality
- **Code-Behind Alignment**: Updated event handlers and removed references to deleted UI elements

### Phase 13 Completion - FileUpload Overlay Integration ✅
- **Contextual Overlay Architecture**: Transformed FileUploadPage into overlay within MainPage chat area
- **State Preservation**: Chat state remains intact during file operations, no page navigation overhead
- **Cross-Platform File Picker**: Platform-specific initialization with `#if WINDOWS` for window handles
- **Smart UI Enhancements**: Auto-switch to summary view, file selection feedback, text wrapping fixes
- **Overlay Positioning**: Anchored within chat Border using `Grid.RowSpan="3"` and `Canvas.ZIndex="1000"`
- **UX Improvements**: Back button navigation, solid background overlay, responsive layout integration
- **Technical Fixes**: Resolved Uno Platform XAML binding issues, prevented horizontal text overflow

### Phase 9 Completion - AI Summarization Integration ✅
- **Prompt Instruction System**: Complete CRUD operations with search and save functionality
- **AI Provider Support**: OpenAI and Ollama integration with comprehensive debug logging
- **Custom Instructions**: User-defined prompts with reusable templates
- **Modal Dialog System**: Search existing prompts and save new ones
- **Database Integration**: Full SQLite integration with usage tracking

### Critical Bug Fixes - Post-Implementation Debugging ✅

#### 1. SQLite Schema Alignment Issues
- **Issue**: `processing_jobs` table missing columns (`parameters`, `priority`, `retry_count`, `max_retries`)
- **Fix**: Removed non-existent columns from INSERT statements
- **Files**: `FileProcessingService.cs` - `CreateProcessingJobAsync` method
- **Impact**: Eliminated SQLite constraint violations during file processing

#### 2. Foreign Key Constraint Violations
- **Issue**: Attempting to create processing jobs with invalid file IDs (0)
- **Fix**: Added ID validation before foreign key operations
- **Files**: `FileProcessingService.cs` - Exception handling in `ProcessFileAsync`
- **Pattern**: `if (fileData.Id > 0)` checks before dependent operations

#### 3. File Type Support Inconsistency
- **Issue**: `.md` files supported in UI but not in `ProcessFileAsync`
- **Fix**: Added `.md` case to file type switch statement
- **Files**: `FileProcessingService.cs` - `ProcessFileAsync` method
- **Pattern**: Group similar file types (`.txt` and `.md`) in same case

#### 4. Summary Generation Data Inconsistency
- **Issue**: AI-generated summaries not saved to database - UI showed different summary than database
- **Root Cause**: Automatic basic summary conflicted with manual AI summary generation
- **Fix**: Removed automatic summary generation, implemented state tracking with `_currentFileData`
- **Files**: `FileProcessingService.cs`, `FileUploadPage.xaml.cs`
- **Solution**: Use `UpdateFileDataAsync` for existing files instead of creating duplicates

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
