-- CAI Design Chat Application Database Migration
-- Version: 2.0 to 3.0
-- Created: 2025-09-22
-- Description: Context Handling Panel - Add display_name column to context_file_links

-- Enable foreign key constraints
PRAGMA foreign_keys = ON;

-- ========================================
-- PHASE 17: Context Handling Panel Enhancement
-- ========================================

-- Step 1: Add display_name column to context_file_links table
-- This allows users to customize file names in the context panel
ALTER TABLE context_file_links ADD COLUMN display_name TEXT;

-- Step 2: Set default display_name to original filename for existing records
-- This ensures backward compatibility for existing context files
UPDATE context_file_links 
SET display_name = (
    SELECT name FROM file_data WHERE file_data.id = context_file_links.file_id
)
WHERE display_name IS NULL;

-- Step 3: Create index for better performance on display_name lookups
CREATE INDEX IF NOT EXISTS idx_context_file_links_display_name ON context_file_links(display_name);

-- Step 4: Create index for session-based context queries (optimization)
CREATE INDEX IF NOT EXISTS idx_context_file_links_session ON context_file_links(context_session_id);

-- ========================================
-- SCHEMA VERSION UPDATE
-- ========================================

-- Update schema version
UPDATE schema_version 
SET version = 3, 
    applied_at = CURRENT_TIMESTAMP,
    description = 'Context Handling Panel - Add display_name column for custom file naming in context management'
WHERE version = 2;

-- Insert new version record if update didn't work
INSERT OR IGNORE INTO schema_version (version, description) VALUES 
(3, 'Context Handling Panel - Add display_name column for custom file naming in context management');

-- ========================================
-- VERIFICATION QUERIES (for testing)
-- ========================================

-- Check context_file_links table structure
-- SELECT sql FROM sqlite_master WHERE name = 'context_file_links';

-- Verify display_name column exists and is populated
-- PRAGMA table_info(context_file_links);

-- Test context files with display names
-- SELECT cfl.id, cfl.display_name, fd.name as original_name, cfl.use_summary, cfl.is_excluded, cfl.order_index
-- FROM context_file_links cfl 
-- JOIN file_data fd ON cfl.file_id = fd.id 
-- ORDER BY cfl.context_session_id, cfl.order_index;

-- Verify schema version
-- SELECT * FROM schema_version ORDER BY version DESC LIMIT 1;

-- Test duplicate display_name detection (should be handled by application logic)
-- SELECT context_session_id, display_name, COUNT(*) as count
-- FROM context_file_links 
-- GROUP BY context_session_id, display_name 
-- HAVING COUNT(*) > 1;
