# Sabil integration — phase 1: disabled read transport

This release prepares a server-only read boundary. It does not create contacts or shipments, calculate delivery prices, consume webhooks, poll in the background, change delivery states, access wallets or expose any new controller/UI. No provider call is made at startup. The accepted manual order/stock/delivery workflow stays operational.

## Implemented

`ICarrierReadGateway` exposes service packages, shipment lookup and timeline only. The Sabil adapter follows the authenticated developer guide at https://app.sabil.ly/developer/docs, pins HTTPS https://v2.sabil.ly and API version1.0.0, validates references and sets credentials per request. The configured factory handler disables redirects/cookies and removes HTTP factory loggers so request references and headers are not added to those logs. No automatic retries. Responses have a1MiB bound and depth32; failures use sanitized states and never include provider bodies/messages. Caller cancellation propagates; a bounded internal timeout becomes Unavailable. Successful provider JSON remains opaque server data pending full schema/status review and must not be logged or exposed directly to clients.

## Default and future configuration

`Carriers:Sabil:ReadEnabled` defaults false. No secret or account ID ships in the repository/image. ApiKey and AccountId must eventually come from an approved server secret source. TimeoutSeconds defaults15 and accepts1..60. IOptions is startup-cached: replace secrets through the chosen secret mechanism and restart the process when required; do not mutate shared options objects. There is no arbitrary endpoint setting or sandbox host configured. Do not enable against a real account merely to test these tests; they use a fake handler and synthetic placeholders only.

This phase registers the disabled client for future application services. It does not add an activation screen, accept credentials from a browser or require a staging redeployment. Keep it disabled until account/read scope and any private-data access have been authorized.

## Gates before shipment creation

Confirm sandbox/test-account behavior, exact service/city/area and response schemas, amount-versus-unit-price and paymentBy/collection/fee semantics, allowed key scopes, rate limits and webhook authentication/event contract. Establish provider idempotency or a unique searchable merchant reference before adding automatic shipment-create retries. A timeout after POST may mean the shipment already exists; do not guess a retry is safe. Remote pending creation must remain separate from actual handover; carrier return status must not confirm physical receipt at the merchant. Keep stock and wallet semantics unchanged.

Next phase needs durable attempts, reconciliation of unknown outcomes and sanitized audit, isolated contract tests followed by an approved test environment, then a bounded live pilot. No real recipient/contact/shipment or webhook/security settings are changed by this release. Read-only protocol tests are not evidence of live API compatibility or shipment exactly-once semantics.
