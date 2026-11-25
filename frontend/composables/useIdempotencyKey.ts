// One Idempotency-Key per user intent. The same key is reused when the same submission is sent again
// (a double click, or a retry after a lost response), and a new key is made after a success or when the
// content changes. The server (IdempotentAttribute) replays the first result for a repeated key instead
// of creating a duplicate cart line, message, review, product, size or category.
export function useIdempotencyKey() {
  let key = ''
  let signature = ''
  return {
    get(content: unknown) {
      const next = JSON.stringify(content)
      if (!key || next !== signature) {
        key = crypto.randomUUID()
        signature = next
      }
      return key
    },
    reset() {
      key = ''
      signature = ''
    }
  }
}
