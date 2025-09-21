-- CAI Design Chat Application Database Schema
-- Version: 2.0
-- Created: 2025-09-16
-- Updated: 2025-09-21
-- Description: SQLite database schema for file processing and enhanced chat functionality

-- Enable foreign key constraints
PRAGMA foreign_keys = ON;

-- File data storage with context management
CREATE TABLE IF NOT EXISTS file_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    content TEXT,                           -- Original or edited content
    summary TEXT,                           -- AI-generated summary
    owner TEXT,
    date_created DATETIME DEFAULT CURRENT_TIMESTAMP,
    file_type TEXT,                         -- 'pdf', 'markdown', 'text', 'docx'
    file_size INTEGER,
    processing_status TEXT DEFAULT 'pending', -- 'pending', 'processing', 'completed', 'error'
    extraction_method TEXT,                 -- 'ai', 'direct', 'ocr'
    original_file_path TEXT,
    -- Context management fields
    is_in_context BOOLEAN DEFAULT FALSE,
    use_summary_in_context BOOLEAN DEFAULT FALSE,
    context_order INTEGER DEFAULT 0,
    is_excluded_temporarily BOOLEAN DEFAULT FALSE,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Chat sessions with active session management
CREATE TABLE IF NOT EXISTS session (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_name TEXT NOT NULL,
    user TEXT,
    is_active BOOLEAN DEFAULT TRUE,         -- Only one session active at a time
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Link between sessions and file data for context
CREATE TABLE IF NOT EXISTS session_file_data_context (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    fk_file_data INTEGER NOT NULL,
    fk_session_id INTEGER NOT NULL,
    stage TEXT,                             -- 'uploaded', 'extracted', 'summarized'
    user TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (fk_file_data) REFERENCES file_data(id) ON DELETE CASCADE,
    FOREIGN KEY (fk_session_id) REFERENCES session(id) ON DELETE CASCADE
);

-- Prompt instructions for AI processing
CREATE TABLE IF NOT EXISTS prompt_instructions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    prompt_type TEXT NOT NULL,              -- 'summary', 'extraction', 'analysis', 'custom'
    language TEXT NOT NULL,                 -- 'fr', 'en', 'es', etc.
    instruction TEXT NOT NULL,
    title TEXT,                             -- Display name for UI
    description TEXT,                       -- Longer description
    is_system BOOLEAN DEFAULT FALSE,        -- System vs user-created prompts
    created_by TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    usage_count INTEGER DEFAULT 0
);

-- Chat messages storage with enhanced context management
CREATE TABLE IF NOT EXISTS chat_messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    message_type TEXT NOT NULL,             -- 'user', 'assistant', 'system'
    content TEXT NOT NULL,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    ai_provider TEXT,                       -- 'openai', 'ollama', 'anthropic', etc.
    ai_model TEXT,
    prompt_text TEXT,                       -- Actual prompt text used (for chat reproduction)
    active_context_file_list TEXT,          -- JSON array of active file IDs: "[1,2,3]"
    FOREIGN KEY (session_id) REFERENCES session(id) ON DELETE CASCADE
);

-- Processing jobs for tracking file operations
CREATE TABLE IF NOT EXISTS processing_jobs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_id INTEGER NOT NULL,
    job_type TEXT NOT NULL,                 -- 'extraction', 'summary', 'analysis'
    status TEXT DEFAULT 'queued',           -- 'queued', 'processing', 'completed', 'failed'
    ai_provider TEXT,
    ai_model TEXT,
    prompt_used TEXT,
    result TEXT,
    error_message TEXT,
    started_at DATETIME,
    completed_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (file_id) REFERENCES file_data(id) ON DELETE CASCADE
);

-- Link files to context sessions (simplified - one context per session)
CREATE TABLE IF NOT EXISTS context_file_links (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    context_session_id INTEGER NOT NULL,   -- References session.id directly
    file_id INTEGER NOT NULL,
    use_summary BOOLEAN DEFAULT FALSE,
    is_excluded BOOLEAN DEFAULT FALSE,
    order_index INTEGER DEFAULT 0,
    added_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (context_session_id) REFERENCES session(id) ON DELETE CASCADE,
    FOREIGN KEY (file_id) REFERENCES file_data(id) ON DELETE CASCADE
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_file_data_context ON file_data(is_in_context);
CREATE INDEX IF NOT EXISTS idx_file_data_type ON file_data(file_type);
CREATE INDEX IF NOT EXISTS idx_file_data_status ON file_data(processing_status);
CREATE INDEX IF NOT EXISTS idx_chat_messages_session ON chat_messages(session_id);
CREATE INDEX IF NOT EXISTS idx_chat_messages_timestamp ON chat_messages(timestamp);
CREATE INDEX IF NOT EXISTS idx_processing_jobs_file ON processing_jobs(file_id);
CREATE INDEX IF NOT EXISTS idx_processing_jobs_status ON processing_jobs(status);
CREATE INDEX IF NOT EXISTS idx_prompt_instructions_type ON prompt_instructions(prompt_type);
CREATE INDEX IF NOT EXISTS idx_prompt_instructions_language ON prompt_instructions(language);

-- Insert default prompt instructions
INSERT OR IGNORE INTO prompt_instructions (id, prompt_type, language, instruction, title, description, is_system) VALUES
(1, 'summary', 'fr', 'Tu es un assistant exécutif. Fais un résumé du fichier en gardant la langue originale du fichier.', 'Résumé Standard (FR)', 'Prompt par défaut pour générer un résumé en français', TRUE),
(2, 'summary', 'en', 'You are an executive assistant. Make a summary of the file and keep the original language of the file.', 'Standard Summary (EN)', 'Default prompt for generating summaries in English', TRUE),
(3, 'extraction', 'fr', 'Extrais le contenu textuel principal de ce fichier en préservant la structure et la mise en forme importantes.', 'Extraction de Texte (FR)', 'Extraction de contenu textuel en français', TRUE),
(4, 'extraction', 'en', 'Extract the main textual content from this file while preserving important structure and formatting.', 'Text Extraction (EN)', 'Text content extraction in English', TRUE),
(5, 'analysis', 'fr', 'Analyse ce document et identifie les points clés, les thèmes principaux et les informations importantes.', 'Analyse de Document (FR)', 'Analyse détaillée de document en français', TRUE),
(6, 'analysis', 'en', 'Analyze this document and identify key points, main themes, and important information.', 'Document Analysis (EN)', 'Detailed document analysis in English', TRUE);

-- Create triggers to update timestamps
CREATE TRIGGER IF NOT EXISTS update_file_data_timestamp 
    AFTER UPDATE ON file_data
    BEGIN
        UPDATE file_data SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id;
    END;

CREATE TRIGGER IF NOT EXISTS update_session_timestamp 
    AFTER UPDATE ON session
    BEGIN
        UPDATE session SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id;
    END;

-- Session activation trigger - ensures only one active session at a time
CREATE TRIGGER IF NOT EXISTS activate_new_session 
    AFTER INSERT ON session
    BEGIN
        -- Deactivate all existing sessions
        UPDATE session SET is_active = FALSE WHERE id != NEW.id;
        -- Activate the new session
        UPDATE session SET is_active = TRUE WHERE id = NEW.id;
    END;

CREATE TRIGGER IF NOT EXISTS update_prompt_instructions_timestamp 
    AFTER UPDATE ON prompt_instructions
    BEGIN
        UPDATE prompt_instructions SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id;
    END;

-- Database version tracking
CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER PRIMARY KEY,
    applied_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    description TEXT
);

INSERT OR REPLACE INTO schema_version (version, description) VALUES 
(2, 'Enhanced chat functionality with single active session, JSON context storage, and direct prompt text storage');
