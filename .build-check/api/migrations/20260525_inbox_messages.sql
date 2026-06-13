-- Buzón de mensajes cliente ↔ empresa / profesional
CREATE TABLE IF NOT EXISTS core.inbox_threads (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
    customer_id uuid NOT NULL REFERENCES core.customers(id) ON DELETE CASCADE,
    partner_id uuid NULL REFERENCES core.partners(id) ON DELETE SET NULL,
    professional_id uuid NULL REFERENCES core.professionals(id) ON DELETE SET NULL,
    subject text NOT NULL,
    status text NOT NULL DEFAULT 'open',
    last_message_at timestamptz NOT NULL DEFAULT now(),
    customer_last_read_at timestamptz NULL,
    partner_last_read_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT inbox_threads_recipient_chk CHECK (partner_id IS NOT NULL OR professional_id IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_inbox_threads_tenant ON core.inbox_threads (tenant_id);
CREATE INDEX IF NOT EXISTS ix_inbox_threads_customer ON core.inbox_threads (customer_id);
CREATE INDEX IF NOT EXISTS ix_inbox_threads_partner ON core.inbox_threads (partner_id);
CREATE INDEX IF NOT EXISTS ix_inbox_threads_professional ON core.inbox_threads (professional_id);
CREATE INDEX IF NOT EXISTS ix_inbox_threads_last_message ON core.inbox_threads (last_message_at DESC);
CREATE INDEX IF NOT EXISTS ix_inbox_threads_status ON core.inbox_threads (status);

CREATE TABLE IF NOT EXISTS core.inbox_messages (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    thread_id uuid NOT NULL REFERENCES core.inbox_threads(id) ON DELETE CASCADE,
    sender_role text NOT NULL,
    body text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT inbox_messages_sender_role_chk CHECK (sender_role IN ('customer', 'business'))
);

CREATE INDEX IF NOT EXISTS ix_inbox_messages_thread ON core.inbox_messages (thread_id, created_at);
