import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  srcDir: '.',
  compatibilityDate: '2026-11-06',
  devtools: { enabled: false },
  css: ['~/assets/css/main.css'],
  vite: { plugins: [tailwindcss()] },
  runtimeConfig: {
    backendOrigin: 'http://127.0.0.1:5000',
    // Social links are the owner's public pages as recorded in the project Overview and already used in the Vue pages.
    // The WhatsApp order number stays empty until the owner approves it; set NUXT_PUBLIC_WHATSAPP_NUMBER. UI hides empty links.
    public: {
      apiBase: '/api/v1/catalogue',
      preview: false,
      whatsappNumber: '',
      facebookUrl: 'https://www.facebook.com/ORKIDEALY/',
      instagramUrl: 'https://www.instagram.com/_orkidea_1/'
    }
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
