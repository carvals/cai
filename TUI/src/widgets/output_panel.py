"""Output panel widget to preview and save generated artifacts (e.g., summaries)."""
from __future__ import annotations

from pathlib import Path
from datetime import datetime
from typing import Optional

from textual.app import ComposeResult
from textual.containers import Vertical, Horizontal
from textual.message import Message
from textual.reactive import reactive
from textual.widget import Widget
from textual.widgets import Button, Label, ListItem, ListView, Static, Rule


class OutputPanel(Widget):
    """A side panel that previews the latest artifact and manages saving.

    - Shows current output root (default: CWD/out; created on first save)
    - Buttons: Change Root, Save
    - Recents list of saved artifacts (most recent first)
    - Markdown preview of the current artifact
    """

    DEFAULT_CSS = """
    OutputPanel {
        background: #1e1e1e;
        color: #cccccc;
    }

    OutputPanel > #output-title {
        dock: top;
        height: 3;
        background: #3a3f44;
        color: #ffffff;
        content-align: center middle;
    }

    OutputPanel > #controls {
        height: 3;
        background: #2d2d2d;
        color: #ffffff;
        padding: 0 1;
    }

    OutputPanel > #root-status {
        height: 1;
        background: #252526;
        color: #aaaaaa;
        padding: 0 1;
        content-align: left middle;
    }

    OutputPanel > #recents-title {
        height: 1;
        background: #252526;
        color: #aaaaaa;
        padding: 0 1;
    }

    OutputPanel > #recents {
        background: #1e1e1e;
        height: 10;
    }
    """

    output_root: reactive[Path] = reactive(Path.cwd() / "out")
    _current_title: Optional[str] = None
    _current_markdown: Optional[str] = None

    class ChangeRootRequested(Message):
        """Request to change the output root (handled by the App)."""
        pass

    def compose(self) -> ComposeResult:
        with Vertical():
            yield Static("📤 Output", id="output-title")
            with Horizontal(id="controls"):
                yield Button("Change Root", id="btn-change-root")
                yield Button("Save", id="btn-save")
            yield Static("", id="root-status")
            yield Rule(id="root-divider")
            yield Static("Recent Artifacts", id="recents-title")
            yield ListView(id="recents")

    def on_mount(self) -> None:
        self._update_root_status()

    def _update_root_status(self) -> None:
        status = self.query_one("#root-status", Static)
        status.update(f"Root: {self.output_root}")

    # --- Public API ---
    def set_output_root(self, path: Path) -> None:
        self.output_root = path
        self._update_root_status()

    def get_output_root(self) -> Path:
        return self.output_root

    def display_artifact(self, title: str, markdown: str) -> None:
        """Record the current artifact metadata (no preview content)."""
        self._current_title = title
        self._current_markdown = markdown

    def add_recent(self, path: Path) -> None:
        recents = self.query_one("#recents", ListView)
        # Append to the end; session-only list does not require strict ordering
        recents.append(ListItem(Label(str(path))))

    # --- Button handlers ---
    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "btn-change-root":
            self.post_message(self.ChangeRootRequested())
        elif event.button.id == "btn-save":
            # Save current artifact if any
            if self._current_title and self._current_markdown:
                self.save_current_artifact()

    # --- Saving ---
    def save_current_artifact(self) -> Optional[Path]:
        if not (self._current_title and self._current_markdown):
            return None
        out_dir = self.output_root
        out_dir.mkdir(parents=True, exist_ok=True)
        # Timestamped filename
        ts = datetime.now().strftime("%Y%m%d-%H%M%S")
        slug = Path(self._current_title).stem.replace(" ", "_")
        filename = f"{slug}.summary.{ts}.md"
        out_path = out_dir / filename
        out_path.write_text(self._current_markdown, encoding="utf-8")
        self.add_recent(out_path)
        return out_path
