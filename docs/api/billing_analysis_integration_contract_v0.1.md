# AURA — Billing–Analysis Integration Contract v0.1

**Status:** DRAFT — review required; not an implementation or runtime test report
**Work item:** SCRUM-307 / W2-M3-03
**Scope:** Contract mapping, error/idempotency semantics, reconciliation, and integration-test expectations.

## 1. Purpose and authority

This document aligns Billing with the existing Analysis contracts while `SCRUM-306` PR #54 is still under review. The Billing OpenAPI referenced here is the draft in `feature/SCRUM-306-billing-schema-api`, blob `dcd145bd0795226c2aa3ec646bda4f145335323c`; it is not assumed to be merged into `develop`. Re-check the approved PR/revision before implementing or accepting a client.

Authoritative inputs:

- Analysis REST OpenAPI: `contracts/analysis-openapi.yaml` (REST 0.6.0 baseline).
- Analysis event schema: `contracts/analysis-events.schema.json` (Event 0.6 baseline).
- Analysis job schema: `contracts/analysis-job.schema.json`.
- Worker–AI Core integration: `docs/api/worker_ai_contract_integration_v1.0.md`.
- Billing candidate contract: `contracts/billing-openapi.yaml` on SCRUM-306 PR #54.
- Billing business policy: SCRUM-305 Confluence design v0.3 and PR #46, both subject to their own review/merge state.

Do not change Analysis REST/Event/Job schema 0.6 or silently add fields to an event. Any approved incompatible/additive schema change must be versioned and reviewed through the project's contract process.

## 2. Business rules to carry across the boundary

1. One accepted, valid FUNDUS analysis consumes one whole-number credit. Billing owner is resolved from trusted Profile/business context; a client must not choose an arbitrary account or clinic wallet.
2. Reserve credit before an analysis is accepted for processing. A reservation is associated with the tenant, analysis identity and generation according to Billing's stored mapping.
3. Capture once when the authoritative Analysis state is `COMPLETED` and a valid result exists. The business policy maps successful and warning-with-valid-result outcomes to `COMPLETED`.
4. Release once on a terminal failure or `LOW_QUALITY` outcome with no valid result. A retryable failure keeps the reservation; it is not a terminal-release signal.
5. Capture and release are mutually exclusive. A conflicting terminal action returns HTTP 409 with `ERR_RESERVATION_STATE_CONFLICT` in the documented Billing policy.
6. A 15-minute reconciliation threshold is not a timer-based release rule. Query Analysis' authoritative state first; release only after an abort is confirmed. If Analysis is unavailable or still `QUEUED`/`PROCESSING`, keep the reservation pending reconciliation.
7. TEST and LIVE are isolated. Mock/test activity must never capture LIVE credit. Payment-provider integration is outside the Sprint 2 acceptance claim.

## 3. Candidate reserve API mapping

The following paths and response codes reflect the Billing OpenAPI draft in PR #54 and must be reconfirmed against the reviewed version before use.

| Operation | Candidate contract | Expected handling |
|---|---|---|
| Reserve credit | `POST /internal/v1/billing/reservations` | `201` for a new reservation; `200` for an idempotent replay. Do not accept work if reserve is denied or unresolved. |
| Read reservation | `GET /internal/v1/billing/reservations/{reservationId}` | Internal authenticated lookup for recovery/reconciliation; confirm the required caller scope in the reviewed contract. |
| Capture | `POST /internal/v1/billing/reservations/{reservationId}/capture` | Billing-domain operation on the completion path; idempotent replay must not debit twice. |
| Release | `POST /internal/v1/billing/reservations/{reservationId}/release` | Release only when the terminal business rule is satisfied; replay must not release twice. |

Candidate reserve request fields are `analysisId`, `tenantId`, `actorAccountId`, `patientId`, `billingOwner` (`type`, `id`), `units` (1), and `generation` (integer >= 1). The candidate contract also specifies a UUID `Idempotency-Key` and `X-Correlation-Id` with a maximum length of 64. These are candidate contract fields, not a claim that the endpoint has been integrated end-to-end.

Before an implementation is accepted, confirm the exact auth audience/issuer, allowed service identity, reserve/read/release scopes, owner-resolution source, correlation behavior, and all required fields against the reviewed OpenAPI and service-token registry. Do not invent scope names or broaden service-token permissions just to make a test pass.

## 4. Capture invocation and event correlation

Analysis Event 0.6 documents `analysis.completed` as the signal for Billing capture. It documents `analysis.failed` as a release signal only when `retryable=false`. The consumer must also use the authoritative business state/result validity and the reservation state; an event name alone must not override those checks.

**Important boundary:** the SCRUM-306 OpenAPI draft states that the current `analysis-api` service identity is not authorized for the HTTP capture route; the route is kept as an internal Billing interface for the Billing-domain consumer. Therefore, do not design the Analysis API to call capture over HTTP unless the service registry and reviewed contract are explicitly changed. Prefer the approved Billing-owned event-consumer/domain path.

Event 0.6 does not provide an approved basis here for assuming `reservationId` or `generation` has been added to the event. The implementation must resolve the correct Billing reservation through a durable, unambiguous Billing-side association using only identifiers actually present in the approved event/REST contract. If a duplicate/out-of-order event cannot be matched to one reservation/generation unambiguously, hold for reconciliation and raise a contract gap; do not guess or capture a different generation.

Recommended behavior:

- `analysis.completed` + authoritative `COMPLETED` + valid result: capture the matching reservation exactly once.
- `analysis.failed` + `retryable=true`: keep reservation `RESERVED` and allow the same generation's retry path; do not release on this event alone.
- `analysis.failed` + `retryable=false` + authoritative terminal failure/no valid result: release exactly once.
- `LOW_QUALITY`: release only when Analysis confirms terminal state and no valid result; confirm how this outcome is represented in the approved Analysis state/event contract before wiring it.
- Duplicate event, duplicate consumer delivery or retry after timeout: perform an idempotent state transition and reconcile the result rather than applying a second ledger effect.

## 5. Error and retry handling

| Condition in candidate Billing API | Contract expectation for caller/consumer |
|---|---|
| `401` / `403` | Authentication/authorization or scope problem. Do not retry indefinitely and do not bypass by granting broader scopes without approval. |
| `402` insufficient credit | Reject the analysis before accepted processing; verify balance/ledger did not change except for the documented rejected attempt. |
| `409` conflict | Read the current reservation/operation state and distinguish idempotent replay from payload or terminal-state conflict. Exact error codes for every conflict must be agreed in the reviewed contract. |
| `503` dependency unavailable / retryable dependency error | Keep operation pending and retry safely with the same idempotency identity; do not create a second reservation or release by assumption. |
| Timeout after request | Treat outcome as unknown, query by durable identifiers and replay idempotently; never infer that the operation did not happen. |

The reservation lifecycle is `RESERVED` → one terminal state (`CAPTURED` or `RELEASED`). `CAPTURED` → `RELEASED` and `RELEASED` → `CAPTURED` are not valid transitions under this contract. Refund after capture is a separate business/ledger decision and is not equivalent to release before capture.

## 6. Reconciliation and generation policy

- Use a stable idempotency key and request fingerprint. A retry with the same key and same business request must return the original result; reuse of the key with a different request must be rejected as a conflict. The exact public error code for a fingerprint mismatch remains to be confirmed in the reviewed OpenAPI.
- Enforce uniqueness for the agreed reservation identity, including `(tenantId, analysisId, generation)` where this is the accepted schema key.
- A manual retry after a reservation has already been released must use a new generation and a distinct idempotency identity, according to the approved retry policy. Do not increment generation merely because a transport/event delivery was retried.
- At the 15-minute threshold, query Analysis. If the result is unavailable, the state is still active, or the dependency fails, record/retain pending reconciliation. Release only after an authoritative abort/terminal failure is confirmed.
- Preserve enough correlation/audit data to trace tenant, analysis, generation, reservation, event/operation idempotency key, correlation ID, timestamp and final transition without putting patient-sensitive data into logs unnecessarily.

## 7. Environment and security boundaries

- Resolve tenant and `billingOwner` on the trusted server side; validate that actor, patient, clinic and tenant relationships are authorized by the owning service.
- Treat service JWT validation, audience/issuer, service identity and scope allowlists as enforced server-side controls. Gateway authentication does not replace Billing's own authorization.
- Isolate TEST/LIVE balance and ledgers. A mock flag is not permission to perform LIVE capture.
- Do not log access tokens, secrets, patient images or unnecessary patient-identifying data in billing events/logs.
- Do not claim sandbox/provider payment behavior is available until it is implemented and supported by runtime evidence.

## 8. Integration test matrix — planned, not executed by this document

| ID | Scenario | Expected evidence | Initial status |
|---|---|---|---|
| S307-BILL-01 | New reserve request | `201`; one `RESERVED` record and one reservation/ledger effect according to schema | PLANNED / NOT RUN |
| S307-BILL-02 | Exact replay with same idempotency key and payload | `200` replay; same reservation; no duplicate debit/reserve effect | PLANNED / NOT RUN |
| S307-BILL-03 | Reuse key with changed payload | Conflict; no second reservation; exact error code confirmed with contract owner | PLANNED / NOT RUN |
| S307-BILL-04 | Insufficient credit | `402`; Analysis not accepted; no unintended balance/ledger mutation | PLANNED / NOT RUN |
| S307-BILL-05 | Missing/wrong service identity, tenant, owner or scope | `401`/`403`; no cross-tenant access or wallet selection by caller | PLANNED / NOT RUN |
| S307-BILL-06 | Concurrent duplicate reserve | One logical reservation under the agreed uniqueness constraint | PLANNED / NOT RUN |
| S307-BILL-07 | Completion event replay/out of order | Only the correct generation can be captured; duplicate event has no duplicate ledger effect | PLANNED / NOT RUN |
| S307-BILL-08 | Retryable `analysis.failed` | Reservation remains pending/reserved; no premature release | PLANNED / NOT RUN |
| S307-BILL-09 | Confirmed terminal failure / LOW_QUALITY | Release once; valid `COMPLETED` result must not be released | PLANNED / NOT RUN |
| S307-BILL-10 | Capture then release, or release then capture | `409 ERR_RESERVATION_STATE_CONFLICT`; terminal state unchanged | PLANNED / NOT RUN |
| S307-BILL-11 | Reconciliation threshold while Analysis is processing/unavailable | Reservation remains pending; no timer-only release | PLANNED / NOT RUN |
| S307-BILL-12 | Confirmed aborted analysis after reconciliation | Release once with traceable evidence | PLANNED / NOT RUN |
| S307-BILL-13 | TEST/mock request reaches LIVE boundary | LIVE ledger/capture remains unchanged | PLANNED / NOT RUN |
| S307-BILL-14 | Analysis API tries HTTP capture with current service registry | Denied as currently configured; Billing-owned consumer/domain path is used unless an approved contract change says otherwise | PLANNED / NOT RUN |
| S307-BILL-15 | Analysis REST/Event/Job schema baseline and event-to-reservation correlation | Keep approved schema version 0.6 unchanged; if existing identifiers cannot map one reservation/generation unambiguously, block the transition and record a contract gap | PLANNED / NOT RUN |

Record actual results only after execution. Every run should record commit SHA, environment, command/test name, result, relevant logs with secrets/PHI redacted, and an evidence location. `PLANNED` or `NOT RUN` is not a pass. Existing frontend/mock tests do not establish service-to-service authorization, event-consumer, reconciliation or Billing end-to-end acceptance.

## 9. Open decisions / dependencies

1. **M2 / Analysis owner:** authoritative state-commit, terminal outcome and reconciliation query/interface; exact event correlation identifiers and LOW_QUALITY representation.
2. **M6 / Profile owner:** trusted owner resolution and caller/tenant/patient/clinic relationship authorization contract.
3. **Billing / SCRUM-306 owner:** final OpenAPI version, reserve/read/release scopes, capture consumer placement, key fingerprint behavior, idempotency retention and exact conflict/error codes.
4. **M1 / architecture owner:** confirm service/event delivery and ownership boundaries without changing Analysis schema 0.6 informally.
5. **M3:** update test plan/RTM with review outcomes and executed evidence; do not mark planned cases passed merely because the documentation exists.

## 10. References

- Analysis baseline: https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/1736808/Analysis+Contract+Baseline+W1+v0.2+REST+0.6.0+Event+Job+0.6
- Worker–AI Core contract: https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/4718651/c+t+h+p+ng+t+ch+h+p+Worker+AI+Core+v1.0+SCRUM-310
- Billing schema/API candidate PR #54: https://github.com/CattyGame/XD-PMHDT/pull/54
- Billing policy SCRUM-305 v0.3: https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/5406727/SCRUM-305+Ch+nh+s+ch+v+thi+t+k+Billing+v0.3

---
**Change note v0.1:** Drafted as contract/test alignment only. Approval, runtime implementation and integration-test execution are separate activities.
