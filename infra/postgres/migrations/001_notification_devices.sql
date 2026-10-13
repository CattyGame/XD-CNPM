BEGIN;

CREATE SCHEMA IF NOT EXISTS notification;

CREATE TABLE IF NOT EXISTS notification.device_registrations (
    project_id varchar(128) NOT NULL,
    device_id uuid NOT NULL,
    firebase_uid varchar(128) NOT NULL,

    token_hash bytea NOT NULL,
    token_ciphertext bytea NOT NULL,
    token_nonce bytea NOT NULL,
    token_tag bytea NOT NULL,
    encryption_key_id varchar(64) NOT NULL,

    platform varchar(16) NOT NULL DEFAULT 'web',
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_notification_device_registrations
        PRIMARY KEY (project_id, device_id),

    CONSTRAINT uq_notification_project_token
        UNIQUE (project_id, token_hash),

    CONSTRAINT ck_notification_project_not_empty
        CHECK (length(trim(project_id)) > 0),

    CONSTRAINT ck_notification_uid_not_empty
        CHECK (length(trim(firebase_uid)) > 0),

    CONSTRAINT ck_notification_token_hash_length
        CHECK (octet_length(token_hash) = 32),

    CONSTRAINT ck_notification_token_ciphertext_length
        CHECK (octet_length(token_ciphertext) BETWEEN 1 AND 4096),

    CONSTRAINT ck_notification_token_nonce_length
        CHECK (octet_length(token_nonce) = 12),

    CONSTRAINT ck_notification_token_tag_length
        CHECK (octet_length(token_tag) = 16),

    CONSTRAINT ck_notification_encryption_key_id
        CHECK (length(trim(encryption_key_id)) > 0),

    CONSTRAINT ck_notification_platform
        CHECK (platform = 'web'),

    CONSTRAINT ck_notification_device_id
        CHECK (device_id <> '00000000-0000-0000-0000-000000000000'::uuid)
);

CREATE INDEX IF NOT EXISTS ix_notification_devices_owner
    ON notification.device_registrations (
        project_id,
        firebase_uid
    );

COMMENT ON TABLE notification.device_registrations IS
    'FCM device registrations. Ownership comes from verified Firebase ID tokens.';

COMMENT ON COLUMN notification.device_registrations.token_hash IS
    'SHA-256 token fingerprint for duplicate detection. Never returned by API.';

COMMENT ON COLUMN notification.device_registrations.token_ciphertext IS
    'FCM token encrypted with AES-256-GCM. Encryption key stored outside database.';

COMMENT ON COLUMN notification.device_registrations.encryption_key_id IS
    'Identifier of the external encryption key; not the encryption key itself.';

COMMIT;