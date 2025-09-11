import asyncio
import uuid
from pathlib import Path
from typing import Optional

import ollama
import httpx
import json
from textual.app import App, ComposeResult
from textual.containers import Horizontal
from textual.reactive import reactive
from textual.widgets import Footer, Header

from database import (
    add_chat_message,
    add_file_summary,
    get_chat_history,
    initialize_database,
)
from widgets.chat_interface import ChatInterface
from widgets.file_browser import FileBrowser
from widgets.model_selection import ModelSelectionScreen
from widgets.output_panel import OutputPanel
from widgets.output_root_prompt import OutputRootPrompt


class OllamaTUI(App):
    """A Textual TUI for interacting with Ollama models."""

    CSS_PATH = "app.tcss"
    BINDINGS = [
        ("ctrl+c", "quit", "Quit"),
        ("ctrl+u", "add_to_context", "Add to Context"),
        ("ctrl+s", "summarize_file", "Summarize File"),
        ("ctrl+r", "clear_context", "Clear Context"),
        ("ctrl+o", "change_output_root", "Change Output Root"),
        ("f5", "refresh_explorer", "Refresh Explorer"),
    ]

    model_name: reactive[Optional[str]] = reactive(None)
    session_id: str = str(uuid.uuid4())
    is_loading: reactive[bool] = reactive(False)

    def compose(self) -> ComposeResult:
        """Create the layout of the application."""
        yield Header()
        with Horizontal():
            yield FileBrowser("./", id="file-browser")
            yield ChatInterface(id="chat-interface")
            yield OutputPanel(id="output-panel")
        yield Footer()

    def on_mount(self) -> None:
        """Initialize the application."""
        initialize_database()
        self.title = "Ollama TUI Chat Assistant"
        self.show_model_selection()

    def show_model_selection(self) -> None:
        """Show the model selection screen."""
        def set_model(model_name: str):
            if model_name and not model_name.startswith("Error:"):
                self.model_name = model_name
                self.sub_title = f"Model: {self.model_name}"
                self.load_chat_history()
            else:
                self.exit(message=model_name or "No model selected")

        self.push_screen(ModelSelectionScreen(), set_model)

    def load_chat_history(self) -> None:
        """Load existing chat history for this session."""
        chat_interface = self.query_one("#chat-interface", ChatInterface)
        history = get_chat_history(self.session_id)
        
        if not history:
            chat_interface.add_system_message("Welcome! Ask me anything.")
        else:
            for role, content in history:
                if role == "user":
                    chat_interface.add_user_message(content)
                else:
                    chat_interface.add_assistant_message(content)

    def on_chat_interface_message_submitted(self, event: ChatInterface.MessageSubmitted) -> None:
        """Handle user message submission from chat interface."""
        asyncio.create_task(self.send_message(event.message))

    async def send_message(self, message: str) -> None:
        """Send message to Ollama and display response."""
        if not self.model_name:
            chat_interface = self.query_one("#chat-interface", ChatInterface)
            chat_interface.add_error_message("No model selected")
            return

        chat_interface = self.query_one("#chat-interface", ChatInterface)
        file_browser = self.query_one("#file-browser", FileBrowser)
        # Early debug line to confirm handler execution
        chat_interface.add_info_message("Debug: entered send_message handler")
        
        # Display user message
        chat_interface.add_user_message(message)
        # Persist user message without blocking the event loop
        try:
            await asyncio.to_thread(add_chat_message, self.session_id, self.model_name, "user", message)
        except Exception as db_err:
            chat_interface.add_error_message(f"DB error (user message): {type(db_err).__name__}: {db_err}")
        
        # Show loading indicator
        chat_interface.set_loading(True)
        # Let UI render the loading indicator
        await asyncio.sleep(0)
        chat_interface.add_info_message(f"Contacting Ollama with model '{self.model_name}'...")
        
        try:
            # Prepare prompt with context
            full_prompt = self.prepare_prompt_with_context(message, file_browser.get_context_files())
            
            # First, ping Ollama HTTP API to verify connectivity
            try:
                async with httpx.AsyncClient(timeout=10.0) as hc:
                    tags_resp = await hc.get('http://127.0.0.1:11434/api/tags')
                    if tags_resp.status_code != 200:
                        chat_interface.add_error_message(
                            f"Ollama /api/tags returned {tags_resp.status_code}: {tags_resp.text[:200]}"
                        )
            except Exception as ping_err:
                chat_interface.add_error_message(f"Could not reach Ollama HTTP API: {type(ping_err).__name__}: {ping_err}")
                # Continue anyway; the Python client might still work
            # Try HTTP API generate with streaming first
            full_text = ""
            streamed_successfully = False
            try:
                async with httpx.AsyncClient(timeout=None) as hc:
                    async with hc.stream(
                        "POST",
                        'http://127.0.0.1:11434/api/generate',
                        json={"model": self.model_name, "prompt": full_prompt, "stream": True},
                    ) as resp:
                        if resp.status_code != 200:
                            raise RuntimeError(f"HTTP {resp.status_code}: {await resp.aread()[:200]}")
                        chat_interface.add_assistant_stream_start()
                        async for line in resp.aiter_lines():
                            if not line:
                                continue
                            try:
                                data = json.loads(line)
                            except json.JSONDecodeError:
                                continue
                            chunk = data.get("response") or ""
                            if chunk:
                                full_text += chunk
                                chat_interface.append_assistant_stream_text(chunk)
                            if data.get("done") is True:
                                break
                        chat_interface.end_assistant_stream()
                        streamed_successfully = True
            except Exception as stream_err:
                chat_interface.add_info_message(f"Streaming failed: {type(stream_err).__name__}: {stream_err}; trying non-stream...")

            if not streamed_successfully:
                # Non-stream fallback via Python client
                client = ollama.Client(host='http://127.0.0.1:11434')
                response = await asyncio.wait_for(
                    asyncio.to_thread(
                        client.generate,
                        model=self.model_name,
                        prompt=full_prompt,
                    ),
                    timeout=120,
                )
                if hasattr(response, 'response'):
                    full_text = response.response
                elif isinstance(response, dict):
                    full_text = response.get('response') or ""

                if full_text:
                    chat_interface.add_assistant_message(full_text)
                else:
                    chat_interface.add_error_message("No response received from Ollama (fallback path).")

            # Save to database if any text was produced
            if full_text:
                try:
                    await asyncio.to_thread(add_chat_message, self.session_id, self.model_name, "assistant", full_text)
                except Exception as db_err:
                    chat_interface.add_error_message(f"DB error (assistant message): {type(db_err).__name__}: {db_err}")
            
        except asyncio.TimeoutError:
            chat_interface.add_error_message("Timed out waiting for Ollama response. Check the model and server logs.")
        except Exception as e:
            chat_interface.add_error_message(f"Error communicating with Ollama: {str(e)}")
        finally:
            # Hide loading indicator
            chat_interface.set_loading(False)

    # --- Output root change flow ---
    def on_output_panel_change_root_requested(self, event: OutputPanel.ChangeRootRequested) -> None:
        """Open a modal prompt to change the output root directory."""
        def set_root(path_str: str | None) -> None:
            if not path_str:
                return
            try:
                new_root = Path(path_str).expanduser().resolve()
                panel = self.query_one("#output-panel", OutputPanel)
                panel.set_output_root(new_root)
                ci = self.query_one("#chat-interface", ChatInterface)
                ci.add_info_message(f"Output root set to: {new_root}")
            except Exception as e:
                ci = self.query_one("#chat-interface", ChatInterface)
                ci.add_error_message(f"Failed to set output root: {e}")

        self.push_screen(OutputRootPrompt(), set_root)

    def action_change_output_root(self) -> None:
        """Keyboard shortcut handler to change output root (Ctrl+O)."""
        def set_root(path_str: str | None) -> None:
            if not path_str:
                return
            try:
                new_root = Path(path_str).expanduser().resolve()
                panel = self.query_one("#output-panel", OutputPanel)
                panel.set_output_root(new_root)
                ci = self.query_one("#chat-interface", ChatInterface)
                ci.add_info_message(f"Output root set to: {new_root}")
            except Exception as e:
                ci = self.query_one("#chat-interface", ChatInterface)
                ci.add_error_message(f"Failed to set output root: {e}")

        self.push_screen(OutputRootPrompt(), set_root)

    def prepare_prompt_with_context(self, message: str, context_files: list[Path]) -> str:
        """Prepare prompt with file context if any."""
        if not context_files:
            return message
        
        context_parts = []
        for file_path in context_files:
            try:
                content = file_path.read_text(encoding='utf-8')
                context_parts.append(f"-- FILE: {file_path} --\n{content}\n")
            except Exception as e:
                context_parts.append(f"-- FILE: {file_path} (Error reading: {e}) --\n")
        
        context_str = "\n".join(context_parts)
        return f"{context_str}\n-- USER PROMPT --\n{message}"

    def on_file_browser_file_selected(self, event: FileBrowser.FileSelected) -> None:
        """Handle file selection in file browser."""
        event.stop()
        # Store the selected file for context operations
        self.selected_file = event.path

    def action_add_to_context(self) -> None:
        """Add selected file to context."""
        if hasattr(self, 'selected_file') and self.selected_file:
            file_browser = self.query_one("#file-browser", FileBrowser)
            chat_interface = self.query_one("#chat-interface", ChatInterface)
            
            if file_browser.add_to_context(self.selected_file):
                chat_interface.add_info_message(f"Added to context: {self.selected_file.name}")

    def action_clear_context(self) -> None:
        """Clear all files from context."""
        file_browser = self.query_one("#file-browser", FileBrowser)
        chat_interface = self.query_one("#chat-interface", ChatInterface)
        
        file_browser.clear_context()
        chat_interface.add_info_message("Context cleared")

    def action_refresh_explorer(self) -> None:
        """Refresh the file explorer tree (F5)."""
        fb = self.query_one("#file-browser", FileBrowser)
        fb.refresh_tree()

    def action_summarize_file(self) -> None:
        """Summarize the selected file."""
        if hasattr(self, 'selected_file') and self.selected_file and self.model_name:
            asyncio.create_task(self.summarize_file(self.selected_file))

    async def summarize_file(self, file_path: Path) -> None:
        """Generate and store a summary of the file."""
        chat_interface = self.query_one("#chat-interface", ChatInterface)
        output_panel = self.query_one("#output-panel", OutputPanel)
        
        try:
            content = file_path.read_text(encoding='utf-8')
            
            # Show loading
            chat_interface.set_loading(True)
            await asyncio.sleep(0)
            chat_interface.add_info_message(f"Summarizing '{file_path.name}' with model '{self.model_name}'...")
            
            # Generate summary
            prompt = f"Please provide a concise summary of this file:\n\n-- FILE: {file_path} --\n{content}"
            
            response = None
            # Try HTTP API first
            try:
                async with httpx.AsyncClient(timeout=180.0) as hc:
                    gen_resp = await hc.post(
                        'http://127.0.0.1:11434/api/generate',
                        json={"model": self.model_name, "prompt": prompt, "stream": False},
                    )
                    if gen_resp.status_code == 200:
                        response = gen_resp.json()
                    else:
                        chat_interface.add_info_message(
                            f"HTTP generate for summary failed with {gen_resp.status_code}; falling back to Python client."
                        )
            except Exception as http_err:
                chat_interface.add_info_message(f"HTTP generate summary error: {http_err}; falling back to client.")

            if response is None:
                client = ollama.Client(host='http://127.0.0.1:11434')
                response = await asyncio.wait_for(
                    asyncio.to_thread(
                        client.generate,
                        model=self.model_name,
                        prompt=prompt,
                    ),
                    timeout=180,
                )
            
            # Extract summary from dict or Pydantic object
            if hasattr(response, 'response'):
                summary = response.response
            elif isinstance(response, dict):
                summary = response.get('response')
            else:
                summary = None
            if not summary:
                chat_interface.add_error_message("No summary received from Ollama.")
                return
            
            # Save summary to database
            add_file_summary(str(file_path), self.model_name, summary)
            
            # Display summary in chat and Output panel
            chat_interface.add_file_summary(file_path.name, summary)
            output_panel.display_artifact(f"{file_path.name}", summary)

            # Auto-save artifact to disk per spec answers
            saved = output_panel.save_current_artifact()
            if saved:
                chat_interface.add_info_message(f"Saved summary to: {saved}")
            
        except asyncio.TimeoutError:
            chat_interface.add_error_message("Timed out waiting for summary from Ollama.")
        except Exception as e:
            chat_interface.add_error_message(f"Error summarizing file: {str(e)}")
        finally:
            chat_interface.set_loading(False)



if __name__ == "__main__":
    app = OllamaTUI()
    app.run()
