import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/App'
import { ErrorBoundary } from './app/ErrorBoundary'
import './shared/styles/fonts.css'
import './shared/styles/tokens.css'
import './shared/styles/base.css'

const container = document.getElementById('root')
if (!container) {
  throw new Error('#root element not found')
}

createRoot(container).render(
  <StrictMode>
    <ErrorBoundary>
      <App />
    </ErrorBoundary>
  </StrictMode>,
)
