import { createApp } from 'vue'
import App from './App.vue'
import { bootstrap } from './app/bootstrap'
import './styles/global.css'

const app = createApp(App)

bootstrap(app)

app.mount('#app')
