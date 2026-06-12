import type { App } from 'vue'
import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import router from '@/router'
import 'element-plus/dist/index.css'

export function bootstrap(app: App<Element>) {
  app.use(createPinia())
  app.use(router)
  app.use(ElementPlus)
}
