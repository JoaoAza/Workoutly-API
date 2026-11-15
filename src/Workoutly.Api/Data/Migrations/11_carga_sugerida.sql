ALTER TABLE workout_plan_items
    ADD COLUMN suggested_weight_kg NUMERIC(6,2)
    CONSTRAINT ck_plan_suggested_weight CHECK (suggested_weight_kg BETWEEN 0 AND 2000);

INSERT INTO schema_migrations (version, name) VALUES (11, 'carga_sugerida');
