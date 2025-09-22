-- Migration v4: Move display_name from context_file_links to file_data
-- This migration preserves existing data and ensures consistency

-- Step 1: Add display_name column to file_data table
ALTER TABLE file_data ADD COLUMN display_name TEXT;

-- Step 2: Copy existing display_name data from context_file_links to file_data
-- Use the first non-null display_name found for each file
UPDATE file_data 
SET display_name = (
    SELECT cfl.display_name 
    FROM context_file_links cfl 
    WHERE cfl.file_id = file_data.id 
    AND cfl.display_name IS NOT NULL 
    AND cfl.display_name != ''
    LIMIT 1
);

-- Step 3: For files without display_name, use the original filename
UPDATE file_data 
SET display_name = name 
WHERE display_name IS NULL OR display_name = '';

-- Step 4: Make display_name NOT NULL (now that all rows have values)
-- Note: SQLite doesn't support ALTER COLUMN, so we'll enforce this in application code

-- Step 5: Remove display_name column from context_file_links
-- Note: SQLite doesn't support DROP COLUMN directly, so we need to recreate the table

-- Create new context_file_links table without display_name
CREATE TABLE context_file_links_new (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    context_session_id INTEGER NOT NULL,
    file_id INTEGER NOT NULL,
    use_summary BOOLEAN DEFAULT 0,
    is_excluded BOOLEAN DEFAULT 0,
    order_index INTEGER DEFAULT 0,
    FOREIGN KEY (context_session_id) REFERENCES session(id) ON DELETE CASCADE,
    FOREIGN KEY (file_id) REFERENCES file_data(id) ON DELETE CASCADE
);

-- Copy data from old table to new table (excluding display_name)
INSERT INTO context_file_links_new (
    id, context_session_id, file_id, use_summary, is_excluded, order_index
)
SELECT 
    id, context_session_id, file_id, use_summary, is_excluded, order_index
FROM context_file_links;

-- Drop old table and rename new table
DROP TABLE context_file_links;
ALTER TABLE context_file_links_new RENAME TO context_file_links;

-- Step 6: Update schema version
INSERT INTO schema_version (version, applied_at, description) 
VALUES (4, datetime('now'), 'Move display_name from context_file_links to file_data');

-- Migration completed successfully
-- display_name is now in file_data table and consistent across all contexts
