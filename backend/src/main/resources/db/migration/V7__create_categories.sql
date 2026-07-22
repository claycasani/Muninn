-- Per-user, user-editable category list. Until now categories were an implicit
-- set: a fixed AI vocabulary plus whatever strings happened to appear in saves.
-- This table makes categories first-class so they can be added (even empty),
-- renamed, and deleted, and so the AI classifies into each user's own list.

CREATE TABLE IF NOT EXISTS categories (
    id         BIGSERIAL PRIMARY KEY,
    user_id    BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name       VARCHAR(80) NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_categories_user_name UNIQUE (user_id, name)
);

CREATE INDEX IF NOT EXISTS idx_categories_user ON categories(user_id);

-- Seed every existing user with the default vocabulary the AI used before.
INSERT INTO categories (user_id, name, sort_order)
SELECT u.id, d.name, d.ord
FROM users u
CROSS JOIN (VALUES
    ('To read', 0),
    ('Restaurants to try', 1),
    ('To buy', 2),
    ('To watch', 3),
    ('Reference', 4),
    ('Inspiration', 5),
    ('Other', 6)
) AS d(name, ord)
ON CONFLICT (user_id, name) DO NOTHING;

-- Preserve any category already present in a user's saved data (manual override
-- wins over the AI category, matching the app's effectiveCategory() logic), so
-- nothing that currently shows as a chip disappears after this migration.
INSERT INTO categories (user_id, name, sort_order)
SELECT d.user_id, d.cat, 100
FROM (
    SELECT s.user_id AS user_id,
           COALESCE(NULLIF(TRIM(s.manual_category), ''), a.category) AS cat
    FROM saves s
    LEFT JOIN analyses a ON a.save_id = s.id
) d
WHERE d.cat IS NOT NULL
  AND TRIM(d.cat) <> ''
  AND d.cat <> 'Uncategorized'
ON CONFLICT (user_id, name) DO NOTHING;
