// Live password checklist. Mirrors the server's Identity rules in Program.cs:
// 8+ characters, an upper- and a lower-case English letter, a digit and a symbol.
export function usePasswordRules(password: () => string) {
  return computed(() => {
    const value = password()
    return [
      { ok: value.length >= 8, text: 'ثمانية أحرف على الأقل' },
      { ok: /[A-Z]/.test(value), text: 'حرف إنجليزي كبير' },
      { ok: /[a-z]/.test(value), text: 'حرف إنجليزي صغير' },
      { ok: /\d/.test(value), text: 'رقم' },
      { ok: /[^A-Za-z0-9]/.test(value), text: 'رمز' }
    ]
  })
}
