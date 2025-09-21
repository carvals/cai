-- CAI Design Chat Application Database Migration
-- Version: 1.0 to 2.0
-- Created: 2025-09-21
-- Description: Chat functionality enhancement with simplified context management

-- Enable foreign key constraints
PRAGMA foreign_keys = ON;

-- ========================================
-- PHASE 1: Session Management Enhancement
-- ========================================

-- Add is_active column to session table
ALTER TABLE session ADD COLUMN is_active BOOLEAN DEFAULT TRUE;

-- Create trigger to ensure only one active session at a time
CREATE TRIGGER IF NOT EXISTS activate_new_session 
    AFTER INSERT ON session
    BEGIN
        -- Deactivate all existing sessions
        UPDATE session SET is_active = FALSE WHERE id != NEW.id;
        -- Activate the new session
        UPDATE session SET is_active = TRUE WHERE id = NEW.id;
    END;

-- ========================================
-- PHASE 2: Chat Messages Structure Update
-- ========================================

-- Step 1: Create backup of existing data before migration
CREATE TABLE IF NOT EXISTS chat_messages_backup AS 
SELECT * FROM chat_messages;

-- Step 2: Create new chat_messages table with updated structure
CREATE TABLE IF NOT EXISTS chat_messages_new (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    message_type TEXT NOT NULL,             -- 'user', 'assistant', 'system'
    content TEXT NOT NULL,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    ai_provider TEXT,                       -- 'openai', 'ollama', 'anthropic', etc.
    ai_model TEXT,
    prompt_text TEXT,                       -- Store actual prompt text used
    active_context_file_list TEXT,          -- JSON array: "[1,2,3]" of active file IDs
    FOREIGN KEY (session_id) REFERENCES session(id) ON DELETE CASCADE
);

-- Step 3: Migrate existing data
INSERT INTO chat_messages_new (
    id, session_id, message_type, content, timestamp, ai_provider, ai_model, prompt_text, active_context_file_list
)
SELECT 
    id, 
    session_id, 
    message_type, 
    content, 
    timestamp, 
    ai_provider, 
    ai_model,
    CASE 
        WHEN prompt_instruction_id IS NOT NULL THEN 
            (SELECT instruction FROM prompt_instructions WHERE id = prompt_instruction_id)
        ELSE NULL 
    END as prompt_text,
    CASE 
        WHEN file_context_id IS NOT NULL THEN 
            '[' || file_context_id || ']'
        ELSE NULL 
    END as active_context_file_list
FROM chat_messages;

-- Step 4: Replace old table with new structure
DROP TABLE chat_messages;
ALTER TABLE chat_messages_new RENAME TO chat_messages;

-- ========================================
-- PHASE 3: Context Management Simplification
-- ========================================

-- Remove context_sessions table (no longer needed)
DROP TABLE IF EXISTS context_sessions;

-- ========================================
-- PHASE 4: Index Recreation
-- ========================================

-- Recreate indexes for chat_messages table
CREATE INDEX IF NOT EXISTS idx_chat_messages_session ON chat_messages(session_id);
CREATE INDEX IF NOT EXISTS idx_chat_messages_timestamp ON chat_messages(timestamp);

-- ========================================
-- PHASE 5: Schema Version Update
-- ========================================

-- Update schema version
UPDATE schema_version 
SET version = 2, 
    applied_at = CURRENT_TIMESTAMP,
    description = 'Chat enhancement with simplified context management - Single active session, JSON context storage, direct prompt text storage'
WHERE version = 1;

-- Insert new version record if update didn't work
INSERT OR IGNORE INTO schema_version (version, description) VALUES 
(2, 'Chat enhancement with simplified context management - Single active session, JSON context storage, direct prompt text storage');

-- ========================================
-- VERIFICATION QUERIES (for testing)
-- ========================================

-- Check session table structure
-- SELECT sql FROM sqlite_master WHERE name = 'session';

-- Check chat_messages table structure  
-- SELECT sql FROM sqlite_master WHERE name = 'chat_messages';

-- Verify schema version
-- SELECT * FROM schema_version ORDER BY version DESC LIMIT 1;

-- Test session activation (should show only one active)
-- SELECT id, session_name, is_active FROM session;
