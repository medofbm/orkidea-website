# Phase 4C: password reset by email (no schema change)

Owner decision 2026-11-21: password reset by email through Brevo SMTP. `orkidea.ly` is authenticated in Brevo and the sender `أوركيديا <noreply@orkidea.ly>` is verified. The SMTP key stays with the owner and is set only on the server.

## Configuration (environment variables on the server; never in git)
| Variable | Example | Notes |
|---|---|---|
| `Email__Smtp__Host` | `smtp-relay.brevo.com` | |
| `Email__Smtp__Port` | `587` | STARTTLS |
| `Email__Smtp__Username` | Brevo SMTP login | from Brevo → SMTP & API |
| `Email__Smtp__Password` | Brevo SMTP key | secret |
| `Email__FromEmail` | `noreply@orkidea.ly` | |
| `Email__FromName` | `أوركيديا` | |
| `App__PublicBaseUrl` | `https://vue-preview.orkidea.ly` | used to build the reset link |

`appsettings.Example.json` documents the keys with empty values. When `Email:Smtp:Host` is empty the sender logs a warning (without the address) and sends nothing; endpoints still answer 202 so behaviour does not reveal configuration.

Admin-managed SMTP settings (per store, password encrypted with Data Protection) need a new table and are a later step that needs a separate migration approval.

## API (antiforgery header `RequestVerificationToken`, rate limit `public-forms`)
| Method | Route | Body | Response |
|---|---|---|---|
| POST | `/api/v1/account/forgot-password` | `{ email }` | always `202 { message: "إذا كان البريد مسجّل عندنا، بيوصلك رابط لتغيير كلمة المرور خلال دقائق." }` (no account enumeration); invalid email format → 400 |
| POST | `/api/v1/account/reset-password` | `{ email, token, password, confirmPassword }` | `200 { message: "تم تغيير كلمة المرور. تقدري تسجلي دخولك الحين." }`; invalid/expired token or weak password → `400 { message, errors?: string[] }` with Arabic messages |

- Token: Identity `GeneratePasswordResetTokenAsync`, base64url-encoded in the link; lifespan 2 hours (`DataProtectionTokenProviderOptions.TokenLifespan`).
- Link: `{App:PublicBaseUrl}/account/reset-password?email={email}&token={token}`.
- Email: Arabic RTL HTML with the brand colours (#6a1b9a / #4a148c), plain-text alternative, subject "تغيير كلمة المرور - أوركيديا", one button, expiry note, "إذا ما طلبتيش، تجاهلي الرسالة".
- Never log tokens, links or full email addresses.

## Nitro
`frontend/server/api/account/[action].ts` allowlist adds `forgot-password` and `reset-password`.

## Vue
- Login page: link "نسيتي كلمة المرور؟" → `/account/forgot-password`.
- `/account/forgot-password`: email field, generic success message.
- `/account/reset-password`: reads `email` and `token` from the query, new password + confirm (show strength hints, same rules as registration), success → link to login.
