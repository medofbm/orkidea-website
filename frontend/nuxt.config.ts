import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  srcDir: '.',
  compatibilityDate: '2026-11-06',
  devtools: { enabled: false },
  css: ['~/assets/css/main.css'],
  vite: { plugins: [tailwindcss()] },
  runtimeConfig: {
    backendOrigin: 'http://127.0.0.1:5000',
    public: { apiBase: '/api/v1/catalogue', preview: false }
  },
  app: { head: { htmlAttrs: { lang: 'ar', dir: 'rtl' }, title: 'أوركيديا' } }
})
