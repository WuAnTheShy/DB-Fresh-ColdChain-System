import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  base: '/app/',
  plugins: [vue()],
  build: {
    outDir: '../wwwroot/app',
    emptyOutDir: true,
  },
  server: {
    port: 8080,
    proxy: {
      '/api': 'http://localhost:5064',
      // 商品图片与团长推文配图也由后端提供，开发时保持与部署后的同源路径一致。
      '/images': 'http://localhost:5064',
      '/uploads': 'http://localhost:5064',
      '/favicon.ico': 'http://localhost:5064',
    },
  },
})
