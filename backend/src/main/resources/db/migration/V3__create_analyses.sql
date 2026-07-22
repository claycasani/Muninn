CREATE TABLE IF NOT EXISTS analyses (
    save_id       BIGINT PRIMARY KEY REFERENCES saves(id) ON DELETE CASCADE,
    inferred_intent  TEXT,
    category         VARCHAR(255),
    suggested_action TEXT,
    extracted_text   TEXT,
    confidence       NUMERIC(4, 3),
    created_at       TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);
