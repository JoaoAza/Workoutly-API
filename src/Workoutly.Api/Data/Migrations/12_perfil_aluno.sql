ALTER TABLE users
    ADD COLUMN first_name VARCHAR(120),
    ADD COLUMN last_name VARCHAR(120),
    ADD COLUMN body_weight_kg NUMERIC(6,2) CHECK (body_weight_kg > 0 AND body_weight_kg <= 1000),
    ADD COLUMN height_cm INTEGER CHECK (height_cm BETWEEN 1 AND 300);

INSERT INTO schema_migrations (version, name) VALUES (12, 'perfil_aluno');
