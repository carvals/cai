"""Enhanced file browser widget with context management."""

from pathlib import Path
from typing import List, Set

from textual.app import ComposeResult
from textual.containers import Vertical, Horizontal
from textual.message import Message
from textual.reactive import reactive
from textual.widget import Widget
from textual.widgets import DirectoryTree, Static, Button


class FileBrowser(Widget):
    """A file browser widget with context management capabilities."""
    
    DEFAULT_CSS = """
    FileBrowser {
        background: #252526;
        color: #cccccc;
    }
    
    FileBrowser > #file-browser-title {
        dock: top;
        height: 3;
        background: #007acc;
        color: #ffffff;
        text-align: center;
        content-align: center middle;
    }
    
    FileBrowser > #context-status {
        dock: bottom;
        height: 3;
        background: #3c3c3c;
        color: #ffffff;
        text-align: center;
        content-align: center middle;
    }
    
    FileBrowser > DirectoryTree {
        background: #252526;
        color: #cccccc;
    }
    """
    
    context_files: reactive[Set[Path]] = reactive(set())
    
    class FileSelected(Message):
        """Message sent when a file is selected."""
        def __init__(self, path: Path) -> None:
            self.path = path
            super().__init__()
    
    class FileAddedToContext(Message):
        """Message sent when a file is added to context."""
        def __init__(self, path: Path) -> None:
            self.path = path
            super().__init__()
    
    class FileRemovedFromContext(Message):
        """Message sent when a file is removed from context."""
        def __init__(self, path: Path) -> None:
            self.path = path
            super().__init__()
    
    class RefreshRequested(Message):
        """Message sent when user requests a refresh of the file explorer."""
        pass
    
    def __init__(self, root_path: str = "./", **kwargs):
        super().__init__(**kwargs)
        self.root_path = root_path
        self.selected_file: Path | None = None
    
    def compose(self) -> ComposeResult:
        """Create the file browser layout."""
        with Vertical():
            yield Static("📁 File Explorer", id="file-browser-title")
            yield DirectoryTree(self.root_path, id="file-tree")
            yield Static("Context: 0 files", id="context-status")
    
    def on_mount(self) -> None:
        """Initialize the file browser."""
        pass

    def refresh_tree(self) -> None:
        """Rebuild the DirectoryTree widget to reflect filesystem changes."""
        try:
            old = self.query_one("#file-tree", DirectoryTree)
            parent = old.parent
            # Remove old tree and mount a new instance
            old.remove()
            if parent is not None:
                parent.mount(DirectoryTree(self.root_path, id="file-tree"))
        except Exception:
            # Best-effort: if anything goes wrong, emit a refresh message for the app
            self.post_message(self.RefreshRequested())
    
    def watch_context_files(self, context_files: Set[Path]) -> None:
        """Watch for changes to the context files."""
        self.update_context_display(context_files)
    
    def on_directory_tree_file_selected(self, event: DirectoryTree.FileSelected) -> None:
        """Handle file selection in directory tree."""
        event.stop()
        self.selected_file = Path(event.path)
        self.post_message(self.FileSelected(self.selected_file))
    
    def add_to_context(self, file_path: Path) -> bool:
        """Add a file to the context."""
        if file_path.is_file() and file_path not in self.context_files:
            new_context = self.context_files.copy()
            new_context.add(file_path)
            self.context_files = new_context
            self.post_message(self.FileAddedToContext(file_path))
            return True
        return False
    
    def remove_from_context(self, file_path: Path) -> bool:
        """Remove a file from the context."""
        if file_path in self.context_files:
            new_context = self.context_files.copy()
            new_context.remove(file_path)
            self.context_files = new_context
            self.post_message(self.FileRemovedFromContext(file_path))
            return True
        return False
    
    def clear_context(self) -> None:
        """Clear all files from context."""
        self.context_files = set()
    
    def get_context_files(self) -> List[Path]:
        """Get list of files in context."""
        return list(self.context_files)
    
    def update_context_display(self, context_files: Set[Path]) -> None:
        """Update the context status display."""
        status = self.query_one("#context-status", Static)
        count = len(context_files)
        
        if count == 0:
            status.update("Context: 0 files")
        else:
            # Show up to 3 file names
            file_names = [f.name for f in sorted(context_files)]
            display_names = file_names[:3]
            
            if count > 3:
                files_text = ", ".join(display_names) + f" (+{count-3} more)"
            else:
                files_text = ", ".join(display_names)
            
            status.update(f"Context: {count} files - {files_text}")
    
    def get_selected_file(self) -> Path | None:
        """Get the currently selected file."""
        return self.selected_file

    # Refresh button removed to test spacing; F5 still calls refresh_tree() via the App
