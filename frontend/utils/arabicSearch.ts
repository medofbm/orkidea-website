// Arabic-friendly matching: ignore diacritics and unify alef, taa marbuta and alef maqsura forms.
export function normaliseArabic(text: string) {
  return text.toLowerCase().replace(/[ً-ْـ]/g, '')
    .replace(/[أإآ]/g, 'ا').replace(/ة/g, 'ه').replace(/ى/g, 'ي').replace(/\s+/g, ' ').trim()
}
