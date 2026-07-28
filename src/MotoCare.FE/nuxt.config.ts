export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  telemetry: false,
  ssr: false,
  css: ['~/assets/css/main.css'],
  imports: {
    dirs: ['utils']
  },
  runtimeConfig: {
    public: {
      apiBase: 'https://moto.luandinh.com/api/v1'
    }
  },
  app: {
    head: {
      title: 'MotoCare CMS',
      meta: [
        {
          name: 'description',
          content: 'Hệ thống quản lý tiệm sửa chữa và nâng cấp xe máy'
        },
        {
          name: 'theme-color',
          content: '#102a43'
        }
      ]
    }
  },
  typescript: {
    strict: true,
    typeCheck: true
  },
  vite: {
    clearScreen: false,
    envPrefix: ['VITE_', 'TAURI_'],
    server: {
      strictPort: true
    }
  },
  ignore: ['**/src-tauri/**']
})
