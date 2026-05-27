-- Cuenta bancaria del partner para recibir liquidaciones (tipos A y B).
ALTER TABLE core.partners
    ADD COLUMN IF NOT EXISTS bank_name text,
    ADD COLUMN IF NOT EXISTS bank_account_type text,
    ADD COLUMN IF NOT EXISTS bank_account_number text,
    ADD COLUMN IF NOT EXISTS bank_account_holder text,
    ADD COLUMN IF NOT EXISTS bank_account_holder_rut text;

COMMENT ON COLUMN core.partners.bank_name IS 'Nombre del banco donde recibe pagos el partner.';
COMMENT ON COLUMN core.partners.bank_account_type IS 'Tipo de cuenta: checking | vista | savings.';
COMMENT ON COLUMN core.partners.bank_account_number IS 'Número de cuenta (solo panel partner).';
COMMENT ON COLUMN core.partners.bank_account_holder IS 'Titular de la cuenta bancaria.';
COMMENT ON COLUMN core.partners.bank_account_holder_rut IS 'RUT del titular (opcional si coincide con el del negocio).';
