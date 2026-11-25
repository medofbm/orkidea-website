// Cosmetic guard for /admin pages: the server (backend + Nitro proxy) enforces the Admin role on every API call.
// It runs in the browser only. The session cookie belongs to the visitor, so SSR HTML must not depend on it
// (same rule as useAccount). Non-admins are sent to the sign-in page instead of seeing an empty admin shell.
export default defineNuxtRouteMiddleware(async () => {
  if (import.meta.server) return
  const { account, refresh } = useAccount()
  try {
    const session = account.value ?? await refresh()
    if (!session?.signedIn || !session.isAdmin) return navigateTo('/account')
  } catch {
    // Backend unreachable: let the page show its own error state; the API still refuses non-admins.
  }
})
