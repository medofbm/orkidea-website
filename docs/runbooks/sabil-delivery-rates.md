# Sabil delivery rates (real prices in the store)

The store charges the `ShippingRates` price the customer picks at checkout (rates mode). This import fills that table with the real home-delivery prices (`deliveryRate`) from the Sabil account catalogue, so the price shown and charged is the carrier's price.

## What the import does
- Reads the branch catalogue from Sabil (read-only, pages of 100, at most 30 pages) through the existing read gateway.
- Keeps only country `lby`, currency `lyd`, and areas that have a delivery rate. Office-pickup pseudo-areas (name contains «استلام» or «إستلام») and any rate of 0 or less are skipped and counted as `skippedPickup`; they are never home delivery.
- Key = city + area. If the same key appears with different rates, the highest rate is used (never undercharge) and it is counted as a conflict.
- Apply replaces ALL rows of `ShippingRates` in one transaction: `CityName = "city — area"`, `Region = city`, `Price = rate`. An empty result is refused. Orders keep their own copy of city and cost, so old orders are not affected. No migration.
- Nothing is stored from the provider except these prices. Errors never show provider text.

## One-time setup (owner, on the server)
Put these in `.preview.env` next to the compose file, then redeploy or restart the web service:
- `ORKIDEA_SABIL_READ_ENABLED=true`
- `ORKIDEA_SABIL_API_KEY=...`
- `ORKIDEA_SABIL_ACCOUNT_ID=...`

The secrets stay on the server only. Editing `deploy/compose.vue-preview.yaml` changes its checksum: update the pinned `SHA256SUMS` as described in `vue-preview-migrations-and-settings.md`.

## Use (admin API, Admin role, antiforgery, optional Idempotency-Key)
- `GET /api/v1/admin/shipping-rates` returns `{ readEnabled, total, rates: [{ id, region, city, price }] }`.
- `POST /api/v1/admin/shipping-rates/import-sabil?dryRun=true&surcharge=10` shows what would change (`applied: false`, counts incl. `skippedPickup`, `surcharge`, 10 sample rows incl. the surcharge). Check it first.
- `POST /api/v1/admin/shipping-rates/import-sabil?dryRun=false&surcharge=10` applies it. Use the same surcharge as the preview.
- `surcharge` (LYD, 0 to 100, at most 2 decimals, default 0) is added to every imported rate; anything else returns 400 `{ error: "قيمة الإضافة غير صحيحة" }`. The backend never assumes a value: the admin types it.

## Surcharge for the women service
The catalogue `deliveryRate` is what Sabil lists, but the «نساء» service adds 10 LYD on top (a real quote for عين زارة was 15 while the catalogue said 5). If the store ships with the women service, enter 10 in the admin field «إضافة على كل سعر (د.ل)». The page leaves it empty so the choice is conscious (0 is allowed).
- 409 means the Sabil link is disabled on the server. 502 means Sabil could not be read now (`state` says why); nothing was changed.

## When Sabil changes prices
Run the dry run, review counts and sample, then apply again. The whole table is replaced each time.

## Customer-facing data
`GET /api/v1/delivery/summary` (rates mode) also returns `groups` (per city: min/max price and areas with prices), and the checkout state returns `region` and `area` per rate, so the frontend can show city then area.
