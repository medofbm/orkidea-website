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
  app: {
    head: {
      htmlAttrs: { lang: 'ar', dir: 'rtl' },
      title: 'أوركيديا',
      meta: [
        { name: 'theme-color', content: '#751D82' },
        { property: 'og:site_name', content: 'أوركيديا' },
        { property: 'og:image', content: '/brand/og-cover.webp' }
      ],
      link: [
        { rel: 'icon', type: 'image/png', sizes: '32x32', href: '/brand/favicon-32.png' },
        { rel: 'apple-touch-icon', href: '/brand/apple-touch-icon.png' }
      ]
    }
  }
})
