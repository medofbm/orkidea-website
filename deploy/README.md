# Isolated staging deployment

These files prepare staging. They do not deploy automatically. The public site and existing ALTO/PharmaNet services must remain available.

1. Obtain authorized SSH access and run `bash deploy/preflight.sh`. Review current capacity, existing containers, listeners and Docker networks before choosing a loopback port or subnet. Do not infer availability from an old inventory.
2. Use only the image artifact from a successful `preview-image` job for the exact reviewed commit. Verify the downloaded artifact checksum before transfer. Record the loaded image ID and commit. The image contains no local appsettings or customer exports.
3. Keep staging environment values outside Git, with permissions 600. Generate new staging-only secrets on the server. Set the image, verified loopback port and proxy gateway, SA password, application connection, migration connection, and optional initial administrator values required by compose. Never reuse production credentials.
4. Start the dedicated database only. Create a fresh `OrkideaStaging` database, a migration identity with schema privileges limited to that database, and a separate application identity with only required data permissions. The web process must never connect as SA. This provisioning is intentionally not automated until the live inventory is reviewed.
5. Run the explicit `initialize` maintenance service once. It applies migrations and seeds synthetic preview data only in Staging and only in `OrkideaStaging`. Confirm its successful exit before starting web. Preview products and delivery use clearly marked test prices, not published business prices.
6. Verify web readiness on loopback, seeded home/catalog, login and a synthetic checkout. Confirm persistent media/keys, container user, resource limits, disabled notification flags, and that the database has no public port.
7. Prepare a dedicated Nginx vhost using the template and verified loopback port. Set a staging-only Basic Auth password interactively. Issue TLS using the existing certificate tooling after DNS ownership and routing are verified. Run the Nginx configuration test before a graceful reload. Do not replace existing vhosts or stop shared services.
8. Verify HTTPS and access protection externally at `staging.orkidea.ly`, including noindex and the preview banner. Record HTTP and browser evidence separately. Do not describe staging as a production launch.

Rollback: disable only the new staging vhost, test/reload Nginx, and stop only the `orkidea-staging` project. Preserve its database, media and key volumes for diagnosis; never use `down --volumes`. DNS changes must be recorded with their prior values. No real notifications are enabled by this deployment.

Current image smoke test checks startup, login/banner and noindex without a database. SQL regressions use a separate disposable database; neither proves the server deployment or full browser checkout.
