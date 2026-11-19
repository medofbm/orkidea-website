# CLAUDE.md - Orkidea repo

Shared brief for Claude, ChatGPT/Codex and humans. Keep it consistent with AGENTS.md. See README.md for commands.

## Stack
Backend: ASP.NET Core net10, EF Core + SQL Server, Identity, ClosedXML, Twilio (opt-in), in `Orkideya/`.
Frontend: Nuxt 4, Vue 3, TypeScript, Tailwind 4, Arabic RTL, in `frontend/`. Nitro routes proxy to the backend `/api/v1/...`.
Legacy Razor store and admin still exist; retire only after Vue acceptance.

## Decisions
- Stay on .NET; no Laravel rewrite.
- Server owns money, stock and delivery rules; the frontend never decides prices.
- Auto-deploy on push applies to the Vue preview only; staging and production need manual approval.
- White-label plan: separate instance per store with its own settings (StoreSettings: name, logo, colours, contact, carrier, currency). Not built yet.

## Rules
- No secrets, tokens, connection strings or customer data in the repo, issues or notes. `appsettings*.json` stay out of git; use `appsettings.Example.json`.
- No production migration, deployment, purchase or real notification without explicit owner approval.
- Small commits; one reviewed push per phase. Do not push or merge on your own.
- Mark work done only with evidence (build, tests, browser check). Static review, runtime tests and deployment are tracked separately.
- Formatting: `.editorconfig` and `frontend/.prettierrc.json` exist; do not mass-reformat in feature commits.

## Checks before a commit
`dotnet build Orkideya.sln -c Release`, the node tests and `python -I tests/check_safety.py` from README.md, and in `frontend/`: `npm run typecheck`.
