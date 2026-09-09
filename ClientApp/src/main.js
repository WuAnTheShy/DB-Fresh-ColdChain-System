import { createApp } from 'vue'
import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap/dist/js/bootstrap.esm.js'
import './styles/main.css'
import App from './App.vue'
import router from './router'

createApp(App).use(router).mount('#app')
