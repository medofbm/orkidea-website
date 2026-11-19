# Sabil checkout choices

Each enabled checkout choice is a named entry under `Carriers:Sabil:Checkout:HomeDestinations`. A choice binds a provider branch, city, area, service and customer handoff method. The frontend receives the choice key and display metadata. Provider IDs stay on the server.

| Entry field | Configuration |
| --- | --- |
| `BranchId`, `City`, `Area` | Exact destination from the verified Sabil account catalogue |
| `Service` | `women` or `men` |
| `ServiceId` | Approved Sabil service identifier for that kind |
| `IsPickup` | True only for an approved customer pickup office with a provider pickup rate |

Use a different choice key for each available combination, and supply one entry per selectable office or delivery area. Legacy home destinations that omit the new fields still use the existing server-wide quote service. When labeled choices are configured, list those choices together without mixing legacy entries. Use the provider's exact office or area label.

`Checkout:HandoffMode` still describes how the merchant gives the parcel to Sabil. Customer office pickup is a separate option. The carrier calculates the shipping fee with the selected service and top-level `isPickup` flag. Catalogue reference rates are for availability checks.

The `CarrierDeliveryChoice` migration adds recipient pickup and service-kind fields to stored quote snapshots. Apply it to the intended database using the normal migration procedure before switching to the new image. Run the release against isolated data first. Keep the carrier switches off until the local service, origin, product eligibility, destinations and both delivery methods have been verified on the approved account.
