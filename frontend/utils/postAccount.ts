type AccountAction = 'login' | 'verify' | 'logout' | 'register' | 'phone' | 'password'

export async function postAccount(action: AccountAction, body?: Record<string, string | boolean | null | undefined>) {
  const session = await $fetch<{ requestToken: string }>('/api/session')
  return $fetch<{ requiresVerification: boolean } | undefined>('/api/account/' + action, {
    method: 'POST',
    body,
    retry: 0,
    headers: { RequestVerificationToken: session.requestToken }
  })
}
