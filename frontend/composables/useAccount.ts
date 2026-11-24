// Shared sign-in state for the header and the account page.
// Loaded on the client only: the session cookie belongs to the visitor, so it must not shape cached SSR HTML.
export interface AccountSession { signedIn: boolean; name: string | null; phoneNumber: string | null; isAdmin: boolean }

export function useAccount() {
  const account = useState<AccountSession | null>('account-session', () => null)

  async function refresh() {
    try {
      account.value = await $fetch<AccountSession>('/api/account')
    } catch {
      account.value = null
      throw new Error('account-unavailable')
    }
    return account.value
  }

  return { account, refresh }
}
