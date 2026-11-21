// Real customer comments from the Orkidea Facebook page, supplied by the owner (2026-11-21).
// Names are intentionally omitted for the commenters' privacy. Text is kept as written;
// only emoji were removed. Move to the database and manage from admin in task 4.7.
export interface Testimonial {
  text: string
  product?: string
  source: 'facebook'
}

export const testimonials: Testimonial[] = [
  { text: 'حاجة طبيعية واكويسة... بالتوفيق', source: 'facebook' },
  { text: 'جربت جميع زيوت عندك ممتازة وروعة', source: 'facebook' },
  { text: 'انى جربت زيت السمسم ونتيجته تبارك الرحمن روووعة. وبالنسبة لزيت جوز الهند مانستغناش عليه نهااائى لأنه فوق الروعة لكل شئ', product: 'زيت السمسم وزيت جوز الهند', source: 'facebook' },
  { text: 'ربي يعاونك ويفتحها عليك', source: 'facebook' },
  { text: 'انى جربته منكم بسم الله ماشاء الله فعلا مفعوله ممتاز للشعر', source: 'facebook' }
]
