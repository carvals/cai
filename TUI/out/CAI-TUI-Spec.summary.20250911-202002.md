This is a detailed specification document for a Conversational AI TUI (Text User Interface) application called CAI TUI. The app aims to provide an industrial-grade workflow to interact with Large Language Models (LLMs). Here's a concise summary:

**Key Features:**

1. Interactive Chat Interface
2. Streaming Responses
3. Provider Adapters for different LLMs (Ollama, OpenAI-compatible, Mistral)
4. File Context Management
5. Markdown and Syntax Highlighting
6. Asynchronous Operations
7. Industrial-Grade Reproducibility

**UI Layout:**

The app features a modern dark theme with a clear separation between the left pane for browsing files, the center pane for chat history with streaming responses, and the right pane for displaying last artifacts (e.g., file summaries) as Markdown.

**Technical Stack:**

* Language: Python 3.10+
* TUI Framework: `textual`
* HTTP Client: `httpx`

**Workflow: File Context**

The app allows users to navigate files in a file browser, add them to the context via a hotkey, and send prompts with the context files prepended.

**Refined Features:**

1. Model/Provider Selection on startup
2. Persistent File Context
3. DuckDB Persistence for chat history and generated summaries
4. File Summarization and saving to Markdown files

The output panel design includes a "results surface" for generated artifacts, with controls for changing the output directory and saving artifacts. Additional commands will be added later.

**Industrial-Grade Objectives:**

1. Deterministic logging of prompts and provider information
2. Reproducible runs with persistent artifacts (summaries/outputs on disk)
3. Clear separation between provider-agnostic app logic and provider-specific adapters