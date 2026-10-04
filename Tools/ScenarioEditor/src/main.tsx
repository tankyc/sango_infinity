/**
 * 应用入口
 *
 * 挂载 React 根节点，注入全局状态提供者与消息提示容器。
 */
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { ScenarioProvider } from './state/store'
import { CommonProvider } from './state/commonStore'
import { Toaster } from './components/ui/sonner'
import './index.css'

const container = document.getElementById('root')
if (!container) throw new Error('未找到 #root 挂载节点')

createRoot(container).render(
  <StrictMode>
    <ScenarioProvider>
      <CommonProvider>
        <App />
        <Toaster position="top-center" richColors closeButton />
      </CommonProvider>
    </ScenarioProvider>
  </StrictMode>
)
