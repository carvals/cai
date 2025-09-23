-- Migration v5: Add shortcut and is_active columns to prompt_instructions table
-- Date: 2025-09-23
-- Description: Adds instruction shortcuts functionality with soft delete capability

-- Add shortcut column for instruction shortcuts (e.g., "sum", "trans", "debug")
ALTER TABLE prompt_instructions ADD COLUMN shortcut TEXT;

-- Add is_active column for soft delete functionality
ALTER TABLE prompt_instructions ADD COLUMN is_active BOOLEAN DEFAULT TRUE;

-- Create unique index on shortcut to prevent duplicates (excluding NULL values)
CREATE UNIQUE INDEX IF NOT EXISTS idx_prompt_instructions_shortcut_unique 
ON prompt_instructions(shortcut) WHERE shortcut IS NOT NULL;

-- Add index on is_active for efficient filtering
CREATE INDEX IF NOT EXISTS idx_prompt_instructions_active ON prompt_instructions(is_active);

-- Update existing system prompts to have shortcuts
UPDATE prompt_instructions SET shortcut = 'sum-fr' WHERE id = 1 AND shortcut IS NULL;
UPDATE prompt_instructions SET shortcut = 'sum-en' WHERE id = 2 AND shortcut IS NULL;
UPDATE prompt_instructions SET shortcut = 'extract-fr' WHERE id = 3 AND shortcut IS NULL;

-- Ensure all existing records are active by default
UPDATE prompt_instructions SET is_active = TRUE WHERE is_active IS NULL;

-- Update database version
UPDATE database_version SET version = 5, updated_at = CURRENT_TIMESTAMP;
