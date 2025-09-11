"""Modal prompt to input a new output root path."""
from __future__ import annotations

from textual.app import ComposeResult
from textual.screen import ModalScreen
from textual.widgets import Input, Button, Label
from textual.containers import Vertical, Horizontal


class OutputRootPrompt(ModalScreen[str | None]):
    """Ask the user to enter a new output root path.

    Returns the entered path string on submit, or None on cancel.
    """

    def compose(self) -> ComposeResult:
        with Vertical(id="output-root-prompt"):
            yield Label("Enter output root directory (created if missing):")
            yield Input(placeholder="/path/to/out", id="root-input")
            with Horizontal():
                yield Button("Cancel", id="btn-cancel")
                yield Button("OK", id="btn-ok", variant="primary")

    def on_mount(self) -> None:
        self.query_one("#root-input", Input).focus()

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "btn-cancel":
            self.dismiss(None)
        elif event.button.id == "btn-ok":
            value = self.query_one("#root-input", Input).value.strip()
            self.dismiss(value or None)

    def on_input_submitted(self, event: Input.Submitted) -> None:
        if event.input.id == "root-input":
            value = event.value.strip()
            self.dismiss(value or None)
