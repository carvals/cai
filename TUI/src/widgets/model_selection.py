import ollama
from ollama import Client
from textual.app import ComposeResult
from textual.screen import ModalScreen
from textual.widgets import ListView, ListItem, Label
from textual.containers import Vertical

class ModelSelectionScreen(ModalScreen):
    """A modal screen to select an Ollama model."""
    
    def __init__(self):
        super().__init__()
        self.models = []

    def compose(self) -> ComposeResult:
        with Vertical(id="model-selection-container"):
            yield Label("Select an Ollama Model", id="model-selection-title")
            yield ListView(id="model-list")

    def on_mount(self) -> None:
        """Fetch models and populate the list."""
        list_view = self.query_one("#model-list", ListView)
        list_view.clear()
        try:
            # Explicitly create a client to ensure we connect to the default host.
            client = Client(host='http://localhost:11434')
            models = client.list().get("models", [])
            if not models:
                self.dismiss("Error: No Ollama models found. Make sure models are pulled.")
                return

            self.models = []
            for model in models:
                # Handle both dict and Pydantic object entries
                if hasattr(model, "model"):
                    model_name = getattr(model, "model")
                elif isinstance(model, dict):
                    model_name = model.get("model") or model.get("name")
                else:
                    model_name = None

                model_name = model_name or str(model)
                self.models.append(model_name)
                list_view.append(ListItem(Label(model_name)))

        except ollama.RequestError:
            self.dismiss("Error: Could not connect to Ollama. Is it running?")
        except Exception as e:
            self.dismiss(f"Error: An unexpected error occurred: {e}")

    def on_list_view_selected(self, event: ListView.Selected) -> None:
        """Dismiss the screen and return the selected model name."""
        try:
            # Use the selected index to get the model name from our stored list
            selected_index = event.index
            if 0 <= selected_index < len(self.models):
                model_name = self.models[selected_index]
                self.dismiss(model_name)
            else:
                self.dismiss("Error: Invalid model selection")
        except Exception as e:
            self.dismiss(f"Error selecting model: {e}")
