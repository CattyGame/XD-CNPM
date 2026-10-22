# AURA — Notification and Consultation/Chat Business Design v0.1

**Status:** DRAFT — service ownership and persistence decisions are open
**Work item:** SCRUM-307 / W2-M3-03
**Purpose:** Separate notification delivery from consultation-chat business behavior and define reviewable acceptance rules.

## 1. Context and decision status

AURA's ADRs say Consultation/chat remains a design decision to be confirmed by M1/M2/M3 before implementation. The Week-1 service catalog has proposed a `notification-chat-svc` using .NET, SignalR and FCM, and the Week-2 plan includes follow-up design work. This is a proposal, not a final ownership decision. The M1 architecture owner and relevant M2/M3 owners must confirm the service boundary, storage and delivery responsibilities.

Existing architecture principles remain binding: services communicate through approved APIs/events rather than cross-service database access; Firebase Cloud Messaging (FCM) is a push-delivery mechanism, not Firebase Authentication; chat messages must be backend-persisted and access-controlled. MongoDB is not in the current baseline and must not be assumed without an explicit Architecture Decision Record (ADR).

## 2. Business scope and boundaries

### 2.1 Consultation / Chat

Supports a patient and an authorized doctor exchanging consultation messages. A conversation and its messages are business records; a real-time channel is only a transport. The backend is authoritative for persistence, participant authorization and history access.

Proposed responsibilities, pending ownership confirmation:

- Create/open a consultation conversation only for an authorized Patient–Doctor relationship.
- Persist an accepted message before reporting it as sent; return a durable message identifier and server timestamp once the storage contract is agreed.
- Deliver new-message notifications over an authenticated real-time channel (SignalR is the current proposal) without treating successful socket delivery as proof that the message was persisted.
- Support reconnect/history retrieval, duplicate-send protection and observable delivery failure without duplicating messages.
- Re-check authorization on conversation/history/message operations, not just when a page or socket first opens.

### 2.2 Notification

Notification is a separate user-facing alert/inbox and delivery concern. Its proposed responsibilities include notification records, recipients, delivery attempts/status, device registrations, templates/versions and retry policy, consistent with the existing service contract's limited device-registration/test-push PoC scope.

FCM should signal that something changed; it must not be the system of record for chat history. A notification delivery failure must not undo a persisted chat message or an Analysis result. An inbox/history API and Analysis-event-to-notification consumer are not considered implemented or accepted by this draft.

## 3. Proposed responsibility map — for review

| Capability | Proposed owner | Notes / decision needed |
|---|---|---|
| Conversation, participant and message authorization | Chat domain/backend (owner TBD) | Must validate tenant and current Patient–Doctor assignment; not frontend-only. |
| Durable message/conversation history | Chat domain/backend (storage TBD) | Select the datastore through ADR; do not assume MongoDB. No cross-service DB access. |
| Live message delivery | Chat backend + SignalR proposal | Socket connection and group membership must be authorized and refreshed/revoked safely. |
| Push alert to offline user | Notification delivery using FCM proposal | FCM is not chat persistence or identity. Push payload contains no patient name, image or personal risk. |
| Notification inbox, recipient rules and delivery records | Notification domain | Existing device-registration/test-push PoC is not proof that event-to-notification is complete. |
| Analysis completed/failed/reviewed events | Analysis publishes approved event schema; Notification consumes after integration approval | Keep Analysis Event 0.6 unchanged unless a reviewed/versioned change is approved. |
| Service ownership, storage and retention policy | M1 architecture owner with M2/M3 input | Open decision; capture in ADR before implementation scope is treated as final. |

## 4. Access-control and lifecycle rules

1. **Relationship authorization:** patient access is limited to their own conversation. Doctor access requires an active, authorized assignment/relationship to that patient and the appropriate tenant context. A clinic membership alone must not implicitly create a DoctorAssignment.
2. **Tenant isolation:** cross-tenant, wrong-patient, unassigned-doctor and revoked-access attempts must be denied on the backend. A guessed conversation/message ID is not authorization.
3. **Revocation:** the service must re-check current access on subsequent history/message requests and invalidate or update real-time group membership when access is revoked. Whether historical messages remain visible after assignment revocation is a business decision still requiring approval; do not assume either permanent access or deletion.
4. **Message persistence:** commit the message before returning a successful send acknowledgement or publishing the message to recipients. A failed real-time delivery is retriable and must not create a second message.
5. **Idempotency:** network retry/reconnect should not duplicate an accepted message or notification. Define a client message/request id and retention window in the detailed API contract; exact wire format remains TBD.
6. **Notification minimization:** push body/title must be generic and must not include patient name, retinal images, diagnosis, risk score or other sensitive health details. Open/deep links must re-authorize the current user on the backend.
7. **Device tokens:** bind FCM device registrations to the authenticated account/device lifecycle; FCM token is not a user identifier. Define update, rotation, logout and invalid-token cleanup behavior in the device API contract.
8. **Separation of outcomes:** a Notification/FCM/SignalR failure does not change Analysis success/failure or Billing capture/release decisions. Domain status comes from its owning service.
9. **Audit and data minimization:** log authorization decisions, message/notification identifiers and delivery outcomes with minimum necessary metadata. Never log access tokens, raw FCM secrets, retinal images or unnecessary health data.

## 5. Proposed conceptual entities (not a physical schema)

| Concept | Purpose | Candidate fields (to be refined by owner) |
|---|---|---|
| Conversation | Consultation thread | conversation ID, tenant ID, patient profile ID, status, created/updated timestamps |
| ConversationParticipant / AccessGrant | Records allowed participants and relationship context | conversation ID, account ID, participant role, source assignment ID, active/revoked timestamps |
| ChatMessage | Durable message | message ID, conversation ID, sender account ID, client request ID, content/body reference, created timestamp, moderation/deletion state if required |
| Notification | In-app notification record | notification ID, type, minimal payload/reference, created/read timestamps, source event/reference |
| NotificationRecipient | Recipient-specific state | notification ID, recipient account ID, read/dismissed timestamps |
| NotificationDelivery | Per-channel delivery attempts | notification ID, channel, attempt count, status, provider response category, next retry/last attempt time |
| DeviceRegistration | Push endpoint binding | registration ID, account ID, device/platform, token reference, last seen, revoked/invalid timestamps |
| NotificationTemplateVersion | Reviewed template version | template key/version, locale, generic title/body pattern, active dates/status |

These are logical concepts for discussion only. Names, field types, indexes, retention and datastore are not approved schema. Do not create migrations or choose MongoDB solely because this draft lists candidate entities.

## 6. Suggested end-to-end flows

### 6.1 Send consultation message

1. Authenticated sender submits a message request to the Chat backend.
2. Backend verifies service token/session, tenant, participant role, patient relationship, conversation state and current access.
3. Backend validates payload/size and idempotency identity; invalid/unauthorized requests are rejected without exposing another patient's records.
4. Backend persists one message durably, then publishes the accepted message to the real-time channel using an authorized recipient set.
5. If recipient delivery fails or is offline, the message remains stored. Notification may enqueue a generic push alert; retrying delivery must not duplicate the stored message.
6. Recipient opens the application/deep link and the backend re-validates authorization before returning conversation/history.

### 6.2 Create and deliver an Analysis-related notification

1. Analysis emits only an approved event from the current versioned contract.
2. Notification consumer validates the event and derives recipients through trusted relationships/permissions; it does not trust a client-supplied recipient list.
3. Consumer creates/deduplicates notification record(s) and delivery work. The exact event-to-notification mappings and rules for `analysis.completed`, `analysis.failed` and `analysis.reviewed` require owner approval.
4. Push contains only generic text and an opaque/deep-link reference; no patient name, image or risk details.
5. Delivery attempts are recorded and retried according to an agreed policy. A failed push does not roll back the source event or business result.

## 7. Security, privacy and operational acceptance

- Backend authorization for every read/write; tenant and assignment checks are server-side.
- No cross-service direct database access.
- Chat history persistence and retention must be confirmed; no claim of MongoDB or any specific datastore until ADR approval.
- Real-time group membership and notification deep links must be re-authorized after assignment or membership changes.
- FCM tokens are account/device endpoints, not identity keys; rotate/revoke them with device lifecycle.
- Push content follows data minimization and contains no PHI/retinal images/risk scores.
- Idempotent event/message handling prevents duplicate message/inbox entries on retries.
- Metrics/audit capture delivery status and correlation identifiers without leaking secrets or sensitive content.
- Define message retention, deletion/correction, reporting/abuse handling, attachment policy, offline history policy, localization and retry/backoff with the owners before implementation acceptance.

## 8. Review / test checklist — planned, not executed

| ID | Scenario | Expected result | Initial status |
|---|---|---|---|
| S307-CHAT-01 | Patient opens own authorized conversation | Authorized history is returned | PLANNED / NOT RUN |
| S307-CHAT-02 | Different patient or cross-tenant user guesses conversation ID | Backend denies; no record/content leakage | PLANNED / NOT RUN |
| S307-CHAT-03 | Unassigned doctor opens/sends to patient | Backend denies even if authenticated | PLANNED / NOT RUN |
| S307-CHAT-04 | Doctor assignment revoked while connected | Subsequent API/history access denied; real-time membership updated per approved policy | PLANNED / NOT RUN |
| S307-CHAT-05 | Message storage succeeds, SignalR delivery fails | Message remains persisted; delivery can retry without duplicate message | PLANNED / NOT RUN |
| S307-CHAT-06 | Client retries the same send request | One durable message; stable replay outcome | PLANNED / NOT RUN |
| S307-NTF-01 | Duplicate Analysis event | One logical notification per agreed deduplication key | PLANNED / NOT RUN |
| S307-NTF-02 | FCM invalid token / provider outage | Delivery failure recorded, retry/cleanup policy applied; source business state unchanged | PLANNED / NOT RUN |
| S307-NTF-03 | Inspect push payload and deep link | No PHI in push; opening destination re-authorizes current user | PLANNED / NOT RUN |
| S307-NTF-04 | User/device logout or token rotation | Old token/registration is no longer treated as a valid account endpoint per approved lifecycle | PLANNED / NOT RUN |
| S307-NTF-05 | Analysis event-to-notification path | End-to-end consumer, recipient policy and delivery evidence captured; not inferred from device PoC | PLANNED / NOT RUN |

## 9. Open decisions and hand-offs

| Decision | Required input | Exit condition |
|---|---|---|
| Chat service owner and boundary (`notification-chat-svc` or separate service) | M1 architecture + M2/M3 | Owner, API/event boundary and deployment scope recorded in ADR/backlog |
| Message/history datastore and retention | M1 + domain owner + security/privacy review | ADR specifies datastore, retention, backup/recovery and access rules |
| Authorization after assignment revocation | Profile/Clinic and Chat owners | Explicit policy for active session, historical messages and audit access |
| Event-to-notification map and deduplication | Analysis + Notification owners | Approved event mapping, recipient resolver, idempotency key and failure policy |
| FCM token lifecycle, template governance and payload rules | Notification owner | Versioned contract and operational/security tests approved |
| M3 test ownership and evidence | M3 with owners | Test cases linked to FR/TC/ADR and executed evidence recorded separately |

## 10. References

- Architecture Decision Records: https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/786441
- Patient Use Cases v0.2 (UC-P10): https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/524545
- Doctor wireframes (D-08 Chat & Notifications): https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/2326559
- Week 2 backlog: https://nguyentruonghau2202.atlassian.net/wiki/spaces/XPMHIT/pages/1966157
- Notification service contract currently in develop: `docs/api/notification_service_contract_v0.1.md` (device registration/test-push PoC scope only).

---
**Change note v0.1:** Business-level proposal for review. It does not finalize architecture ownership, physical schema, endpoint paths or production integration.
