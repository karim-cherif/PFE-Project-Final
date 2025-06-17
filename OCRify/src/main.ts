import { createApp } from 'vue'
import { createPinia } from 'pinia'
import './style.css'
import App from './App.vue'
import router from './router'
import { documents } from '@/services/api'

const app = createApp(App)
const pinia = createPinia()

// Provide the documents service globally
app.provide('documents', documents)

app.use(pinia)
app.use(router)
app.mount('#app')
