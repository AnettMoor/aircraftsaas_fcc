import { createApp } from 'vue'
import { createPinia } from 'pinia'
import './assets/tailwind.css'
import App from './App.vue'
import router from './router'
import { useAuthStore } from './stores/authStore'

const app = createApp(App)

const pinia = createPinia()
app.use(pinia)
app.use(router)

// Bootstrap auth BEFORE mounting — registers Axios token provider
// and attempts silent session restore from sessionStorage + localStorage.
const authStore = useAuthStore()
authStore.bootstrap()

app.mount('#app')
