# Ollama TUI Tutorial

This tutorial provides complete instructions on how to set up, run, and use the Ollama TUI project.

## 1. Project Overview

The Ollama TUI is a sophisticated terminal-based chat assistant that connects to a local Ollama instance. It features:

- **Interactive Chat Interface**: Real-time conversation with LLMs
- **File Browser**: Navigate and select files for context
- **File Context Management**: Add files to chat context for code/document analysis
- **File Summarization**: Generate summaries of selected files
- **Model Selection**: Choose from available Ollama models
- **Persistent History**: Chat history stored in DuckDB database
- **Modern UI**: Dark theme with syntax highlighting and Markdown rendering

## 2. Prerequisites

### Install Ollama
1. Download and install Ollama from [https://ollama.ai](https://ollama.ai)
2. Start the Ollama service:
   ```bash
   ollama serve
   ```
3. Pull at least one model (e.g., llama3):
   ```bash
   ollama pull llama3
   ```

### Verify Ollama is Running
Test that Ollama is accessible:
```bash
curl http://localhost:11434/api/tags
```

## 3. Project Setup

### Clone and Navigate
```bash
cd /path/to/your/projects
# If cloning: git clone <repository-url>
cd textualize
```

### Create Virtual Environment
```bash
python -m venv .venv
source .venv/bin/activate  # On Windows: .venv\Scripts\activate
```

### Install Dependencies
```bash
pip install -r requirements.txt
```

## 4. Running the Application

### Method 1: Using the Run Script (Recommended)
```bash
source .venv/bin/activate
python run.py
```

### Method 2: Direct Module Execution
```bash
source .venv/bin/activate
cd src
python -m app
```

## 5. Using the Application

### Initial Setup
1. **Model Selection**: On first launch, select an Ollama model from the list
2. **Interface Layout**: 
   - Left pane: File browser with context status
   - Right pane: Chat interface with input at bottom

### Basic Chat
1. Type your message in the input field at the bottom
2. Press Enter to send
3. Wait for the LLM response (loading indicator will show)
4. Continue the conversation

### File Context Management
1. **Navigate Files**: Use the file browser on the left to explore directories
2. **Select File**: Click on a file to select it
3. **Add to Context**: Press `Ctrl+U` to add the selected file to context
4. **View Context**: Check the context status at the bottom of the file browser
5. **Clear Context**: Press `Ctrl+R` to clear all files from context

### File Summarization
1. **Select File**: Click on a file in the browser
2. **Summarize**: Press `Ctrl+S` to generate a summary
3. **View Summary**: The summary appears in the chat with special formatting

### Keyboard Shortcuts
- `Ctrl+C`: Quit the application
- `Ctrl+U`: Add selected file to context
- `Ctrl+S`: Summarize selected file
- `Ctrl+R`: Clear all files from context
- `Enter`: Send chat message
- `Tab`: Navigate between interface elements

## 6. Project Structure

```
textualize/
├── src/
│   ├── app.py              # Main application
│   ├── app.tcss            # Textual CSS styling
│   ├── database.py         # DuckDB operations
│   └── widgets/
│       ├── __init__.py
│       ├── chat_interface.py    # Chat UI component
│       ├── file_browser.py      # File browser component
│       └── model_selection.py   # Model selection screen
├── run.py                  # Application launcher
├── requirements.txt        # Python dependencies
├── tutorial.md            # This file
├── job_tracker.md         # Development progress
└── OllamaTUI-Spec.md      # Project specification
```

## 7. Technical Details

### Dependencies
- **textual==6.1.0**: TUI framework
- **httpx==0.28.1**: HTTP client for async requests
- **ollama==0.5.3**: Ollama Python client
- **duckdb==1.3.2**: Embedded database for persistence

### Key Features Implementation
- **Async Operations**: All Ollama API calls are non-blocking
- **Reactive UI**: Real-time updates using Textual's reactive system
- **Modular Design**: Separate widgets for different UI components
- **Error Handling**: Comprehensive error handling for network and file operations
- **Data Persistence**: Chat history and file summaries stored in DuckDB

### CSS vs TCSS
The application uses `.tcss` files (Textual CSS) instead of standard `.css`. TCSS supports Textual-specific properties like:
- `dock`: Position widgets (top, bottom, left, right)
- `content-align`: Align content within widgets
- `scrollbar-background`: Customize scrollbar appearance

## 8. Troubleshooting

### Common Issues

**"Could not connect to Ollama"**
- Ensure Ollama is running: `ollama serve`
- Check if port 11434 is accessible
- Verify with: `curl http://localhost:11434/api/tags`

**"No Ollama models found"**
- Pull a model: `ollama pull llama3`
- List available models: `ollama list`

**"Module not found" errors**
- Activate virtual environment: `source .venv/bin/activate`
- Install dependencies: `pip install -r requirements.txt`

**Application crashes or UI issues**
- Update Textual: `pip install --upgrade textual`
- Check terminal compatibility
- Try running with: `textual run --dev run.py` for detailed error info

### Performance Tips
- Large files in context may slow down responses
- Clear context regularly with `Ctrl+R`
- Use file summarization for large codebases
- Restart application if memory usage becomes high

## 9. Development

### Running in Development Mode
```bash
source .venv/bin/activate
textual run --dev run.py
```

### Key Commands Used During Development
```bash
# Create virtual environment
python -m venv .venv

# Activate environment
source .venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Run application
python run.py

# Test Ollama connection
curl http://localhost:11434/api/tags
```

This tutorial covers everything needed to reproduce and use the Ollama TUI project successfully.

---

## 10. Updates & Troubleshooting Addendum (2025-09)

The following improvements were made to address connectivity and persistence edge cases:

- __Message dispatch fix__: The chat submit handler is `on_chat_interface_message_submitted(...)` in `src/app.py`. This ensures messages are sent when you press Enter in the input.
- __Pydantic response support__: The Ollama Python client (0.5.x) returns typed objects (e.g., `GenerateResponse`). The app now reads `response.response` and falls back to dict shape if needed.
- __HTTP fallback__: If the Python client is slow or mismatched, the app tries direct HTTP requests to the Ollama REST API first: `POST /api/generate` with `stream=false`.
- __Host selection__: All connections target `http://127.0.0.1:11434` to avoid IPv4/IPv6 localhost quirks.
- __DuckDB primary key__: The schema uses `id INTEGER PRIMARY KEY` without autoincrement. The app now computes the next `id` in code for `chat_history` and uses `ON CONFLICT(file_path, model) DO UPDATE` for `file_summaries`.

### Quick Connectivity Checks

```bash
# Verify models list
curl http://127.0.0.1:11434/api/tags | jq

# Test a small model non-streaming
curl -s -X POST http://127.0.0.1:11434/api/generate \
  -H 'Content-Type: application/json' \
  -d '{"model":"llama3.2:latest","prompt":"Say hello!","stream":false}' | jq -r '.response'
```

### Python Client vs HTTP

```python
# Python client (returns a Pydantic object in 0.5.x)
import ollama
client = ollama.Client(host='http://127.0.0.1:11434')
r = client.generate(model='llama3.2:latest', prompt='Say hello!')
print(getattr(r, 'response', None))

# HTTP (bypasses the Python client)
import httpx
resp = httpx.post('http://127.0.0.1:11434/api/generate',
                  json={"model":"llama3.2:latest","prompt":"Say hello!","stream":False}, timeout=60)
print(resp.json().get('response'))
```

### DuckDB Notes

- If you previously saw `ConstraintException` on inserts, pull latest code and re-run. The app now computes `id` values for inserts.
- Database file: `src/chat_history.db`.

### Dev Commands Used

```bash
# Run app
source .venv/bin/activate
python run.py

# Development mode with live reload
textual run --dev run.py

# Test Ollama connectivity
curl http://127.0.0.1:11434/api/tags
```
