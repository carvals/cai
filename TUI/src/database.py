import duckdb
import os
from datetime import datetime

# Define the path for the database file
DB_FILE = "chat_history.db"

def initialize_database():
    """Connect to DuckDB and create tables if they don't exist."""
    con = duckdb.connect(DB_FILE)
    # Create chat_history table
    con.execute("""
    CREATE TABLE IF NOT EXISTS chat_history (
        id INTEGER PRIMARY KEY, 
        session_id VARCHAR, 
        model VARCHAR, 
        timestamp TIMESTAMP, 
        role VARCHAR, 
        content VARCHAR
    );
    """)
    # Create file_summaries table
    con.execute("""
    CREATE TABLE IF NOT EXISTS file_summaries (
        id INTEGER PRIMARY KEY,
        file_path VARCHAR,
        model VARCHAR,
        timestamp TIMESTAMP,
        summary VARCHAR,
        UNIQUE(file_path, model)
    );
    """)
    con.close()

def add_chat_message(session_id: str, model: str, role: str, content: str):
    """Add a new chat message to the database."""
    con = duckdb.connect(DB_FILE)
    try:
        # Generate the next id explicitly to avoid PRIMARY KEY constraint issues
        next_id = con.execute("SELECT COALESCE(MAX(id), 0) + 1 FROM chat_history").fetchone()[0]
        con.execute(
            "INSERT INTO chat_history (id, session_id, model, timestamp, role, content) VALUES (?, ?, ?, ?, ?, ?)",
            (next_id, session_id, model, datetime.now(), role, content)
        )
    finally:
        con.close()

def get_chat_history(session_id: str):
    """Retrieve chat history for a given session."""
    con = duckdb.connect(DB_FILE)
    result = con.execute(
        "SELECT role, content FROM chat_history WHERE session_id = ? ORDER BY timestamp ASC",
        (session_id,)
    ).fetchall()
    con.close()
    return result

def add_file_summary(file_path: str, model: str, summary: str):
    """Add or update a file summary in the database."""
    con = duckdb.connect(DB_FILE)
    try:
        # Generate next id for insert path
        next_id = con.execute("SELECT COALESCE(MAX(id), 0) + 1 FROM file_summaries").fetchone()[0]
        con.execute(
            (
                "INSERT INTO file_summaries (id, file_path, model, timestamp, summary) "
                "VALUES (?, ?, ?, ?, ?) "
                "ON CONFLICT(file_path, model) DO UPDATE SET timestamp=excluded.timestamp, summary=excluded.summary"
            ),
            (next_id, file_path, model, datetime.now(), summary)
        )
    finally:
        con.close()

def get_file_summary(file_path: str, model: str):
    """Retrieve a file summary from the database."""
    con = duckdb.connect(DB_FILE)
    result = con.execute(
        "SELECT summary FROM file_summaries WHERE file_path = ? AND model = ?",
        (file_path, model)
    ).fetchone()
    con.close()
    return result[0] if result else None
