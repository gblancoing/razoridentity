CREATE TABLE IF NOT EXISTS core.professional_follows (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    follower_professional_id UUID NOT NULL,
    followed_type VARCHAR(20) NOT NULL CHECK (followed_type IN ('professional', 'partner')),
    followed_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (follower_professional_id, followed_type, followed_id)
);

CREATE INDEX IF NOT EXISTS ix_professional_follows_follower ON core.professional_follows (follower_professional_id);
CREATE INDEX IF NOT EXISTS ix_professional_follows_followed ON core.professional_follows (followed_type, followed_id);
