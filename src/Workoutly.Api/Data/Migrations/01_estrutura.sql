-- Workoutly: instalação inicial em banco vazio. Não apaga tabelas existentes.
SET LOCAL TIME ZONE 'UTC';

CREATE TABLE schema_migrations (
    version INT NOT NULL PRIMARY KEY,
    name VARCHAR(120) NOT NULL,
    applied_at TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
);

CREATE TABLE users (
    id UUID NOT NULL PRIMARY KEY,
    name VARCHAR(120) NOT NULL,
    email VARCHAR(254) NOT NULL,
    normalized_email VARCHAR(254) NOT NULL UNIQUE,
    password_hash VARCHAR(512) NOT NULL,
    created_at TIMESTAMPTZ(6) NOT NULL
);

CREATE TABLE user_sessions (
    token_hash CHAR(64) NOT NULL PRIMARY KEY,
    user_id UUID NOT NULL,
    created_at TIMESTAMPTZ(6) NOT NULL,
    expires_at TIMESTAMPTZ(6) NOT NULL,
    CONSTRAINT fk_sessions_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE exercises (
    id UUID NOT NULL PRIMARY KEY,
    owner_id UUID NULL,
    name VARCHAR(120) NOT NULL,
    muscle_group VARCHAR(60) NOT NULL,
    equipment VARCHAR(80) NOT NULL,
    description VARCHAR(1000) NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_exercises_owner FOREIGN KEY (owner_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE account_roles (
    user_id UUID NOT NULL PRIMARY KEY,
    role VARCHAR(20) NOT NULL,
    CONSTRAINT ck_account_role CHECK (role IN ('student', 'teacher')),
    CONSTRAINT fk_role_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE student_teachers (
    student_id UUID NOT NULL PRIMARY KEY,
    teacher_id UUID NOT NULL,
    created_at TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT ck_distinct_student_teacher CHECK (student_id <> teacher_id),
    CONSTRAINT fk_enrollment_student FOREIGN KEY (student_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_enrollment_teacher FOREIGN KEY (teacher_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE teacher_invitations (
    teacher_id UUID NOT NULL PRIMARY KEY,
    code CHAR(12) NOT NULL UNIQUE,
    expires_at TIMESTAMPTZ(6) NOT NULL,
    CONSTRAINT fk_invitation_teacher FOREIGN KEY (teacher_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE workout_plans (
    id UUID NOT NULL PRIMARY KEY,
    student_id UUID NOT NULL,
    current_version INT NOT NULL,
    deleted_at TIMESTAMPTZ(6) NULL,
    CONSTRAINT fk_plan_student FOREIGN KEY (student_id) REFERENCES users(id),
    CONSTRAINT ck_plan_version CHECK (current_version > 0)
);

CREATE TABLE workout_plan_versions (
    plan_id UUID NOT NULL,
    version INT NOT NULL,
    name VARCHAR(120) NOT NULL,
    notes VARCHAR(1000) NOT NULL,
    author_id UUID NOT NULL,
    created_at TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (plan_id, version),
    CONSTRAINT fk_version_plan FOREIGN KEY (plan_id) REFERENCES workout_plans(id),
    CONSTRAINT fk_version_author FOREIGN KEY (author_id) REFERENCES users(id)
);

CREATE TABLE workout_plan_items (
    plan_id UUID NOT NULL,
    version INT NOT NULL,
    position INT NOT NULL,
    exercise_id UUID NOT NULL,
    name VARCHAR(120) NOT NULL,
    muscle_group VARCHAR(60) NOT NULL,
    equipment VARCHAR(80) NOT NULL,
    description VARCHAR(1000) NOT NULL,
    set_count INT NOT NULL,
    repetitions INT NOT NULL,
    PRIMARY KEY (plan_id, version, position),
    CONSTRAINT uq_plan_exercise UNIQUE (plan_id, version, exercise_id),
    CONSTRAINT fk_item_version FOREIGN KEY (plan_id, version) REFERENCES workout_plan_versions(plan_id, version),
    CONSTRAINT fk_item_exercise FOREIGN KEY (exercise_id) REFERENCES exercises(id),
    CONSTRAINT ck_plan_sets CHECK (set_count BETWEEN 1 AND 20),
    CONSTRAINT ck_plan_reps CHECK (repetitions BETWEEN 1 AND 200),
    CONSTRAINT ck_plan_position CHECK (position BETWEEN 0 AND 49)
);

CREATE TABLE workout_sessions (
    id UUID NOT NULL PRIMARY KEY,
    student_id UUID NOT NULL,
    plan_id UUID NOT NULL,
    plan_version INT NOT NULL,
    started_at TIMESTAMPTZ(6) NOT NULL,
    duration_seconds INT NOT NULL,
    notes VARCHAR(1000) NOT NULL,
    request_hash CHAR(64) NOT NULL,
    created_at TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT uq_session_revision UNIQUE (id, plan_id, plan_version),
    CONSTRAINT fk_session_student FOREIGN KEY (student_id) REFERENCES users(id),
    CONSTRAINT fk_session_plan_version FOREIGN KEY (plan_id, plan_version) REFERENCES workout_plan_versions(plan_id, version),
    CONSTRAINT ck_session_duration CHECK (duration_seconds BETWEEN 1 AND 86400)
);

CREATE TABLE workout_session_sets (
    session_id UUID NOT NULL,
    plan_id UUID NOT NULL,
    plan_version INT NOT NULL,
    exercise_id UUID NOT NULL,
    set_position INT NOT NULL,
    repetitions INT NOT NULL,
    weight_kg DECIMAL(6,2) NOT NULL,
    PRIMARY KEY (session_id, exercise_id, set_position),
    CONSTRAINT fk_set_session_revision FOREIGN KEY (session_id, plan_id, plan_version) REFERENCES workout_sessions(id, plan_id, plan_version),
    CONSTRAINT fk_set_plan_exercise FOREIGN KEY (plan_id, plan_version, exercise_id) REFERENCES workout_plan_items(plan_id, version, exercise_id),
    CONSTRAINT ck_session_set_position CHECK (set_position BETWEEN 0 AND 19),
    CONSTRAINT ck_session_repetitions CHECK (repetitions BETWEEN 1 AND 200),
    CONSTRAINT ck_session_weight CHECK (weight_kg BETWEEN 0 AND 2000)
);

CREATE TABLE workout_session_changes (
    session_id UUID NOT NULL,
    version INT NOT NULL,
    operation_id UUID NOT NULL UNIQUE,
    request_hash CHAR(64) NOT NULL,
    changed_at TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (session_id, version),
    CONSTRAINT fk_change_session FOREIGN KEY (session_id) REFERENCES workout_sessions(id),
    CONSTRAINT ck_change_version CHECK (version > 0)
);

CREATE INDEX ix_sessions_user ON user_sessions (user_id);
CREATE INDEX ix_sessions_expiry ON user_sessions (expires_at);
CREATE INDEX ix_exercises_owner_name ON exercises (owner_id, name);
CREATE INDEX ix_student_teacher ON student_teachers (teacher_id);
CREATE INDEX ix_plans_student ON workout_plans (student_id, deleted_at);
CREATE INDEX ix_sessions_student_started ON workout_sessions (student_id, started_at, id);
