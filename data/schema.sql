PRAGMA foreign_keys = ON;

CREATE TABLE departments (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL,
    parent_department_id TEXT,
    active INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0, 1)),
    FOREIGN KEY (parent_department_id) REFERENCES departments(id)
);

CREATE TABLE people (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    title TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    department_id TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0, 1)),
    FOREIGN KEY (department_id) REFERENCES departments(id)
);

CREATE TABLE responsibilities (
    id TEXT PRIMARY KEY,
    department_id TEXT NOT NULL,
    person_id TEXT,
    description TEXT NOT NULL,
    keywords TEXT NOT NULL,
    priority INTEGER NOT NULL DEFAULT 3 CHECK (priority BETWEEN 1 AND 5),
    active INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0, 1)),
    FOREIGN KEY (department_id) REFERENCES departments(id),
    FOREIGN KEY (person_id) REFERENCES people(id)
);

CREATE TABLE test_documents (
    id TEXT PRIMARY KEY,
    subject TEXT NOT NULL,
    body TEXT NOT NULL,
    expected_primary_department_id TEXT NOT NULL,
    expected_primary_person_id TEXT,
    expected_cc_department_ids TEXT NOT NULL DEFAULT '[]',
    difficulty TEXT NOT NULL CHECK (difficulty IN ('easy', 'medium', 'hard')),
    notes TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (expected_primary_department_id) REFERENCES departments(id),
    FOREIGN KEY (expected_primary_person_id) REFERENCES people(id)
);

CREATE TABLE dispatch_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    document_subject TEXT NOT NULL,
    document_summary TEXT NOT NULL,
    primary_department_id TEXT NOT NULL,
    primary_person_id TEXT,
    cc_department_ids TEXT NOT NULL DEFAULT '[]',
    accepted INTEGER NOT NULL CHECK (accepted IN (0, 1)),
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (primary_department_id) REFERENCES departments(id),
    FOREIGN KEY (primary_person_id) REFERENCES people(id)
);

CREATE INDEX idx_people_department ON people(department_id, active);
CREATE INDEX idx_responsibilities_department ON responsibilities(department_id, active);
CREATE INDEX idx_responsibilities_person ON responsibilities(person_id, active);
CREATE INDEX idx_history_department ON dispatch_history(primary_department_id);

CREATE VIEW responsibility_search_view AS
SELECT
    r.id AS responsibility_id,
    r.description,
    r.keywords,
    r.priority,
    d.id AS department_id,
    d.name AS department_name,
    p.id AS person_id,
    p.name AS person_name,
    p.title AS person_title
FROM responsibilities AS r
JOIN departments AS d ON d.id = r.department_id
LEFT JOIN people AS p ON p.id = r.person_id
WHERE r.active = 1 AND d.active = 1 AND (p.id IS NULL OR p.active = 1);
