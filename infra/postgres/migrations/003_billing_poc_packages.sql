BEGIN;

SET LOCAL ROLE aura_billing_migrator;

CREATE TABLE IF NOT EXISTS billing_poc.service_packages (
    id integer PRIMARY KEY,
    name varchar(100) NOT NULL,
    price numeric(12, 2) NOT NULL CHECK (price >= 0),
    credits integer NOT NULL CHECK (credits > 0),
    updated_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO billing_poc.service_packages
    (id, name, price, credits)
VALUES
    (1, 'Basic', 100000, 10),
    (2, 'Standard', 250000, 30),
    (3, 'Clinic Demo', 700000, 100)
ON CONFLICT (id) DO NOTHING;

GRANT USAGE ON SCHEMA billing_poc TO aura_billing_app;

GRANT SELECT, INSERT, UPDATE, DELETE
ON billing_poc.service_packages
TO aura_billing_app;

COMMIT;