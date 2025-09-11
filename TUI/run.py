#!/usr/bin/env python3
"""Run script for the Ollama TUI application."""

import sys
import os

# Add the src directory to the Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src'))

from app import OllamaTUI

if __name__ == "__main__":
    app = OllamaTUI()
    app.run()
