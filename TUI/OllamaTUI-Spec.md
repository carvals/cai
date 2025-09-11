# Specification: Ollama TUI Chat Assistant

## 1. Project Overview

Create a sophisticated, visually appealing Text User Interface (TUI) application that functions as a chat assistant powered by a local Ollama instance. The application will be built using the Textual framework.

The goal is to provide a rich, interactive, and "cool" chat experience directly in the terminal, complete with modern UI features like a file browser for context management and stylish presentation.

## 2. Core Features

- **Interactive Chat Interface**: A real-time chat window to send prompts to and receive responses from an LLM.
- **Ollama Integration**: The TUI will communicate with a locally running Ollama server via its REST API.
- **File Context Management**: Users can select files from a built-in file browser. The content of these files can be injected into the chat context, allowing the LLM to answer questions about specific codebases, documents, or logs.
- **Markdown & Syntax Highlighting**: Render the LLM's responses with full Markdown support, including syntax highlighting for code blocks, to improve readability.
- **Asynchronous Operations**: All network requests and file operations will be non-blocking to ensure the UI remains fluid and responsive at all times.

## 3. UI Layout & Design

The interface will be divided into three main panes using a clean, modern layout.

```
+--------------------------------------------------------------------+
| Ollama TUI Chat Assistant          v1.0                            |
+--------------------------------+-----------------------------------+
|                                |                                   |
|  ▼ File Explorer               |  System: Welcome! Ask me anything.|
|  ┌───────────────────────────┐ |                                   |
|  │ ▶ project/                  │ |  You: What is Textual?            |
|  │   ▶ data/                   │ |                                   |
|  │   ▶ src/                    │ |  LLM: Textual is a Python...      |
|  │     - __init__.py           │ |  ```python                        |
|  │     - app.py                │ |  class MyApp(App):                |
|  │     - components.py         │ |      ...                         |
|  │   - README.md               │ |  ```                              |
|  └───────────────────────────┘ |                                   |
|                                |                                   |
+--------------------------------+-----------------------------------+
| > Type your message...         [↑] [↓] [Enter]                     |
+--------------------------------------------------------------------+
```

- **Header**: Displays the application title and version. A loading indicator will appear here during LLM responses.
- **Left Pane (File Explorer)**: A `Tree` widget that displays the file system, starting from the current working directory. Users can navigate, expand/collapse directories, and select files.
- **Right Pane (Chat Window)**: A scrollable container (`RichLog`) that displays the conversation history. User prompts and LLM responses will be clearly distinguished.
- **Footer**: A dynamic area showing the user input `Input` widget and contextual key bindings (e.g., `Ctrl+U` to upload a file, `Ctrl+C` to quit).

## 4. Key Widgets & Styling

- **`Header`**: Static title bar with custom styling.
- **`Footer`**: To display key bindings.
- **`Tree`**: For the file explorer. Will use icons to distinguish between files and directories.
- **`RichLog`**: To display the chat history efficiently.
- **`Markdown`**: To render formatted LLM responses.
- **`Input`**: For user prompts.
- **`LoadingIndicator`**: To provide visual feedback during API calls.

**Styling**: The application will use Textual's CSS capabilities to define a "cool" dark theme. The color palette will be inspired by modern code editors, with attention to contrast and readability.

## 5. Technical Stack

- **Language**: Python 3.10+
- **TUI Framework**: `textual`
- **HTTP Client**: `httpx` (for asynchronous requests to Ollama)
- **Dependencies**: `ollama` Python library (to streamline API interaction).

## 6. Workflow: File Context

1.  The user navigates the **File Explorer**.
2.  By pressing a hotkey (e.g., `Ctrl+U`) on a selected file, the file is marked as "added to context."
3.  The UI will visually indicate which files are currently in context (e.g., with a special icon or color).
4.  When the user sends a new prompt, the application reads the content of all contextually added files.
5.  This content is prepended to the user's prompt in a structured format before being sent to the Ollama API.
    ```json
    {
      "model": "llama3",
      "prompt": "-- FILE: app.py --\n...file content...\n\n-- USER PROMPT --\nHow does this code work?"
    }
    ```
6.  The user can clear the context at any time with another hotkey.

## 7. Refined Features (Based on Feedback)

- **Model Selection**: The application will not use a hardcoded model. Instead, on startup, it will query the Ollama API to get a list of all locally available models. The user will be prompted to select a model from this list to use for the current session.

- **Persistent File Context**: Files added to the context will remain there for the entire session until explicitly cleared by the user. This allows for multi-turn conversations about a fixed set of files.

- **DuckDB for Data Persistence**: All chat history and generated file summaries will be stored in a local DuckDB database (`chat_history.db`). This provides a robust and queryable data store.

- **File Summarization**: A new feature will be added. By selecting a file in the explorer and pressing a hotkey, the user can trigger a summarization task. The application will send the file's content to the LLM and store the resulting summary in a dedicated `file_summaries` table in the DuckDB database.
