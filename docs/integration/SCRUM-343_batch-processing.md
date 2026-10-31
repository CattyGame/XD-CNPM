# SCRUM-343: Batch processing integration checklist

## Scope

The batch API accepts multipart form data at `POST /api/v1/batches`. Add the same metadata fields used by `POST /api/v1/analyses`: one `patientId`, `modality=FUNDUS`, and `eyeSide` per batch, plus one repeated `images` part for each image. The current upload contract binds one patient to a batch; per-image patient metadata is not supported by this endpoint.

## Local setup

1. Copy `.env.example` to `.env` and set the local database and RabbitMQ credentials, including `RABBITMQ_WORKER_PASSWORD`.
2. Start the development dependencies and initialize PostgreSQL using `scripts/dev/Initialize-AuraDevDatabase.ps1`.
3. Start the Analysis API and worker services. The worker scaling compose file builds `Aura.Worker.Analysis`, uses the RabbitMQ worker account, sets prefetch to 10, and connects to Redis at `redis:6379`.

## Acceptance checks

### 100-image batch

Send one request with a fresh UUID `Idempotency-Key`, valid patient metadata, and 100 supported Fundus images. For PowerShell, build a multipart form with `patientId`, `modality`, `eyeSide`, and 100 repeated `images` parts, then POST it to `$api/api/v1/batches`.

Confirm that the response has one batch ID; PostgreSQL has one `analysis.batches` row and 100 `analysis.images` rows linked through `batch_id`; 100 jobs are available to the RabbitMQ worker queue; and workers consume more than one job while the batch is active.

### Progress

Poll `GET /api/v1/batches/{batchId}` using a user authorized for the patient. The `summary.total`, `active`, `succeeded`, and `failed` counts should match the Redis hash `batch:{batchId}:progress`. A completed batch should report `COMPLETED`, with `processing=0`.

### Partial failure

Submit 100 valid image files, including five valid Fundus images known to be rejected by the analysis quality check. Invalid or corrupt files rejected during upload do not exercise worker partial failure. Confirm that the other 95 images finish, five image results expose sanitized error codes, Redis reports `completed=95`, `failed=5`, `processing=0`, and the batch status is `PARTIAL_COMPLETED`. Confirm one `batch.completed` event arrives on the configured events exchange; its event ID is stable for the batch so consumers can deduplicate redelivery.

## Notes

- Re-send a request only with the same idempotency key when checking replay behavior. A new key creates a different batch.
- Redis counters are idempotent per image ID, so RabbitMQ redelivery must not increment an image twice.
- The Jira target of under 10 ms needs a separately controlled latency run with the API, Profile, PostgreSQL, and Redis running on the same measured network.
