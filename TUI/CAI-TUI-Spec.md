# Specification: CAI TUI (Conversational AI) Assistant

## 1. Project Overview

Build a sophisticated, visually appealing Text User Interface (TUI) application to interact with Large Language Models (LLMs). The app is called CAI TUI and provides an industrial-grade workflow to control LLM execution and outputs with strong reproducibility. 

Initial provider is a local Ollama server (via REST API). The architecture must remain provider-agnostic to support additional backends (e.g., OpenAI-compatible, Mistral, etc.) through an adapter layer.

## 2. Core Features

- **Interactive Chat Interface**: Real-time chat to send prompts and receive responses.
- **Streaming Responses**: Incremental token/phrase streaming into the chat window with a compact status bar (no UI overlay).
- **Provider Adapters**: Pluggable backends. v1 ships with an Ollama adapter; future adapters will be added.
- **File Context Management**: Select files in a file browser and inject their contents into prompts.
- **Markdown & Syntax Highlighting**: Render LLM responses with Markdown/code highlighting.
- **Asynchronous Operations**: Keep UI responsive for all I/O and network operations.
- **Industrial-Grade Reproducibility**: Persist history and generated artifacts for audit and replay.

## 3. UI Layout & Design

```
+------------------------------------------------------------------------------------+
| CAI TUI Assistant                             v1.0                                |
+------------------------------+------------------------------+---------------------+
|                              |                              |                     |
|  ▼ File Explorer             |  Chat Window                 |  Output Panel       |
|  ┌────────────────────────┐  |  System: Welcome! ...       |  (Markdown view)    |
|  │ ▶ project/             │  |  You: What is Textual?      |                     |
|  │   ▶ src/               │  |  LLM: Textual is a Python…  |  Save root: ./out   |
|  │     - app.py           │  |  [streaming appears here]   |  [Change root] [🖫]  |
|  └────────────────────────┘  |                              |                     |
|                              |                              |                     |
+------------------------------+------------------------------+---------------------+
| > Type your message...                         [↑] [↓] [Enter]                    |
+------------------------------------------------------------------------------------+
```

- **Header**: Title/version + compact status (“Generating…”) during responses; no overlay.
- **Left Pane (File Explorer)**: `DirectoryTree` for browsing. Selected files can be added to context.
- **Center Pane (Chat Window)**: `RichLog` for chat history with streaming and final Markdown render.
- **Right Pane (Output Panel)**: Displays last artifact (e.g., file summary) as Markdown and offers save controls.
- **Footer**: Input widget and key bindings.

## 4. Key Widgets & Styling

- `Header`, `Footer`
- `DirectoryTree` (file explorer)
- `RichLog` (chat history)
- `Markdown` (render answers)
- `Input` (user messages)
- `LoadingIndicator` (API calls)

Use Textual TCSS for a modern dark theme.

## 5. Technical Stack

- **Language**: Python 3.10+
- **TUI Framework**: `textual`
- **HTTP Client**: `httpx`
- **Providers**:
  - **Ollama**: local server via REST API (initial adapter)
  - Future: additional API adapters (OpenAI-compatible, Mistral, etc.)
- **Persistence**: DuckDB for chat history and generated summaries

## 6. Workflow: File Context

1. Navigate files in the **File Explorer**.
2. Add files to the "context" via a hotkey.
3. When sending a prompt, read all context files and prepend them to the user prompt in a structured format:
   ```json
   {
     "model": "<provider-specific-model>",
     "prompt": "-- FILE: app.py --\n...file content...\n\n-- USER PROMPT --\nHow does this code work?"
   }
   ```
4. Clear context with a hotkey.

## 7. Refined Features (Based on Feedback)

- **Model/Provider Selection**: On startup, list available models from the active provider (Ollama in v1). Provide a command to change later.
- **Persistent File Context**: Context persists until cleared.
- **DuckDB Persistence**: Store all chat history and file summaries in `chat_history.db`.
- **File Summarization**: Generate file summaries; store in `file_summaries` table. Mirror to an Output panel and support saving to Markdown files.

## 7b. Proposed Output Panel Design

- **Purpose**: Provide a “results surface” for generated artifacts (e.g., summaries), distinct from the conversational flow.
- **Presentation**: Markdown rendering of the most recent artifact, synchronized with what appears in chat.
- **Controls**:
  - "Change root" button to set the output directory (default `./out/`).
  - "Save" button to persist the current artifact to `<root>/<slug>.md`.
  - Status line showing current output root and save path.
- **File naming**: `<filename>.summary.md` by default; allow timestamp variant `…YYYYMMDD-HHMM.md` via settings.
- **Persistence**: On save, write Markdown file and (optionally) record a manifest row in DuckDB linking chat message → artifact path.
- **Resizing**: Left/Center/Right panes resizable via drag handles or keybindings.

## 7c. Output-Related Commands (Palette)

- "Summarize File (with instructions...)" → prompts for custom instruction text.
- "Change Output Root" → updates the Output Panel’s save root.
- "Change Model/Provider" → reopens the selection flow.

## 8. Industrial-Grade Objectives

- Deterministic logging of prompts, context, provider, and model.
- Reproducible runs with persistent artifacts (summaries/outputs on disk).
- Clear separation between provider-agnostic app logic and provider-specific adapters.

## 9. Provider Adapter Guidelines

- Uniform interface for `list_models`, `generate(prompt, stream={True|False})`.
- Support streaming and non-streaming.
- Translate provider-native responses into unified structures for the UI (strings, events, etc.).
- Centralize error handling and diagnostics at the adapter boundary.

## 11. Open Questions (to finalize Output Panel scope)

1. Default output root: use `./out/` at repo root, or the current working directory?  default current working directory with a subforlder /out (to be created at the first saved if it does not exist).
2. Overwrite vs versioning: if a file exists, should we append a timestamp or overwrite? append with timestamp
3. Auto-save behavior: save automatically on every summarize, or only when user clicks Save? on every summarize
4. Additional artifact types beyond summaries we should plan for (e.g., code fixes, reports)? yes we will have additional artifact
5. Should the Output panel keep a list/history of recent artifacts, or only the latest? list of recent arfifacts

## 10. Initial Keyboard Shortcuts (subject to expansion)

- Ctrl+C: Quit
- Enter: Send chat message
- Ctrl+U: Add selected file to context
- Ctrl+S: Summarize selected file
- Ctrl+R: Clear all files from context
- Palette commands: Change Model, Summarize with Instructions, Set Output Root (future)
