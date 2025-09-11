"""Chat interface widget for displaying conversation history."""

import asyncio
from typing import Optional

from rich.markdown import Markdown
from textual.app import ComposeResult
from textual.containers import Vertical
from textual.message import Message
from textual.reactive import reactive
from textual.widget import Widget
from textual.widgets import Input, LoadingIndicator, RichLog, Static


class ChatInterface(Widget):
    """A chat interface widget for displaying messages and handling input."""
    
    DEFAULT_CSS = """
    ChatInterface {
        background: #1e1e1e;
    }
    
    ChatInterface > #chat-log {
        background: #1e1e1e;
        color: #cccccc;
        border: none;
        scrollbar-background: #2d2d2d;
        scrollbar-color: #007acc;
    }
    
    ChatInterface > #loading {
        dock: top;
        height: 1;
        background: #007acc;
    }
    
    ChatInterface > #chat-input {
        dock: bottom;
        height: 3;
        background: #2d2d2d;
        color: #ffffff;
        border: solid #444444;
    }
    
    ChatInterface > #chat-input:focus {
        border: solid #007acc;
    }
    """
    
    is_loading: reactive[bool] = reactive(False)
    
    class MessageSubmitted(Message):
        """Message sent when user submits a chat message."""
        def __init__(self, message: str) -> None:
            self.message = message
            super().__init__()
    
    def compose(self) -> ComposeResult:
        """Create the chat interface layout."""
        with Vertical():
            yield RichLog(id="chat-log", highlight=True, markup=True)
            yield LoadingIndicator(id="loading")
            yield Input(placeholder="Type your message...", id="chat-input")
    
    def on_mount(self) -> None:
        """Initialize the chat interface."""
        self.query_one("#loading", LoadingIndicator).display = False
    
    def watch_is_loading(self, is_loading: bool) -> None:
        """Watch for changes to the loading state."""
        self.update_loading_display(is_loading)
    
    def on_input_submitted(self, event: Input.Submitted) -> None:
        """Handle user input submission."""
        if event.input.id == "chat-input" and event.value.strip():
            message = event.value.strip()
            self.post_message(self.MessageSubmitted(message))
            event.input.clear()
    
    def update_loading_display(self, is_loading: bool) -> None:
        """Update the loading indicator display."""
        loading = self.query_one("#loading", LoadingIndicator)
        loading.display = is_loading
    
    def add_system_message(self, message: str) -> None:
        """Add a system message to the chat log."""
        chat_log = self.query_one("#chat-log", RichLog)
        chat_log.write(f"[bold blue]System:[/] {message}")
    
    def add_user_message(self, message: str) -> None:
        """Add a user message to the chat log."""
        chat_log = self.query_one("#chat-log", RichLog)
        chat_log.write(f"[bold green]You:[/] {message}")
    
    def add_assistant_message(self, message: str, use_markdown: bool = True) -> None:
        """Add an assistant message to the chat log."""
        chat_log = self.query_one("#chat-log", RichLog)
        chat_log.write(f"[bold cyan]LLM:[/]")
        if use_markdown:
            chat_log.write(Markdown(message))
        else:
            chat_log.write(message)
    
    def add_error_message(self, message: str) -> None:
        """Add an error message to the chat log."""
        chat_log = self.query_one("#chat-log", RichLog)
        chat_log.write(f"[bold red]Error:[/] {message}")
    
    def add_info_message(self, message: str) -> None:
        """Add an info message to the chat log."""
        chat_log = self.query_one("#chat-log", RichLog)
        chat_log.write(f"[bold yellow]Info:[/] {message}")
    
    def add_file_summary(self, filename: str, summary: str) -> None:
        """Add a file summary to the chat log."""
        chat_log = self.query_one("#chat-log", RichLog)
        chat_log.write(f"[bold magenta]File Summary ({filename}):[/]")
        chat_log.write(Markdown(summary))
    
    def clear_chat(self) -> None:
        """Clear the chat log."""
        chat_log = self.query_one("#chat-log", RichLog)
        chat_log.clear()
    
    def set_loading(self, loading: bool) -> None:
        """Set the loading state."""
        self.is_loading = loading
    
    def focus_input(self) -> None:
        """Focus the chat input."""
        self.query_one("#chat-input", Input).focus()
