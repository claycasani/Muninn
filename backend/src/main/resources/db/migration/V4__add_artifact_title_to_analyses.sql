ALTER TABLE analyses
    ADD COLUMN IF NOT EXISTS artifact_title TEXT;
