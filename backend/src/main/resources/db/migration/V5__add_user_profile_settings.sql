ALTER TABLE users
    ADD COLUMN IF NOT EXISTS display_name VARCHAR(80),
    ADD COLUMN IF NOT EXISTS daily_digest_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS digest_time VARCHAR(5) NOT NULL DEFAULT '07:00',
    ADD COLUMN IF NOT EXISTS notifications_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS auto_archive_days INTEGER NOT NULL DEFAULT 30;

UPDATE users
SET display_name = INITCAP(REPLACE(REPLACE(REPLACE(SPLIT_PART(email, '@', 1), '.', ' '), '_', ' '), '-', ' '))
WHERE display_name IS NULL OR display_name = '';
