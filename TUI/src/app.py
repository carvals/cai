import asyncio
import uuid
from pathlib import Path
from typing import Optional

import ollama
import httpx
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


class OllamaTUI(App):
    """A Textual TUI for interacting with Ollama models."""

    CSS_PATH = "app.tcss"
    BINDINGS = [
        ("ctrl+c", "quit", "Quit"),
        ("ctrl+u", "add_to_context", "Add to Context"),
        ("ctrl+s", "summarize_file", "Summarize File"),
        ("ctrl+r", "clear_context", "Clear Context"),
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

            # Try HTTP API generate first (non-streaming)
            response = None
            try:
                async with httpx.AsyncClient(timeout=120.0) as hc:
                    gen_resp = await hc.post(
                        'http://127.0.0.1:11434/api/generate',
                        json={"model": self.model_name, "prompt": full_prompt, "stream": False},
                    )
                    if gen_resp.status_code == 200:
                        data = gen_resp.json()
                        response = data
                    else:
                        chat_interface.add_info_message(
                            f"HTTP generate failed with {gen_resp.status_code}; falling back to Python client."
                        )
            except Exception as http_err:
                chat_interface.add_info_message(f"HTTP generate error: {http_err}; falling back to Python client.")

            if response is None:
                # Fallback to Python client
                client = ollama.Client(host='http://127.0.0.1:11434')
                response = await asyncio.wait_for(
                    asyncio.to_thread(
                        client.generate,
                        model=self.model_name,
                        prompt=full_prompt,
                    ),
                    timeout=120,
                )
            
            # Display response (supports dict and Pydantic object types)
            if hasattr(response, 'response'):
                response_text = response.response  # Pydantic model attribute
            elif isinstance(response, dict) and 'response' in response:
                response_text = response['response']
            else:
                response_text = None

            if response_text:
                chat_interface.add_assistant_message(response_text)
                
                # Save to database
                try:
                    await asyncio.to_thread(add_chat_message, self.session_id, self.model_name, "assistant", response_text)
                except Exception as db_err:
                    chat_interface.add_error_message(f"DB error (assistant message): {type(db_err).__name__}: {db_err}")
            else:
                chat_interface.add_error_message(
                    f"No response received from Ollama. Type: {type(response)}"
                )
            
        except asyncio.TimeoutError:
            chat_interface.add_error_message("Timed out waiting for Ollama response. Check the model and server logs.")
        except Exception as e:
            chat_interface.add_error_message(f"Error communicating with Ollama: {str(e)}")
        finally:
            # Hide loading indicator
            chat_interface.set_loading(False)

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

    def action_summarize_file(self) -> None:
        """Summarize the selected file."""
        if hasattr(self, 'selected_file') and self.selected_file and self.model_name:
            asyncio.create_task(self.summarize_file(self.selected_file))

    async def summarize_file(self, file_path: Path) -> None:
        """Generate and store a summary of the file."""
        chat_interface = self.query_one("#chat-interface", ChatInterface)
        
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
            
            # Display summary
            chat_interface.add_file_summary(file_path.name, summary)
            
        except asyncio.TimeoutError:
            chat_interface.add_error_message("Timed out waiting for summary from Ollama.")
        except Exception as e:
            chat_interface.add_error_message(f"Error summarizing file: {str(e)}")
        finally:
            chat_interface.set_loading(False)



if __name__ == "__main__":
    app = OllamaTUI()
    app.run()
