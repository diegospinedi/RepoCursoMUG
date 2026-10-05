import '@fontsource/montserrat/600.css'
import '@fontsource/montserrat/700.css'
import '@fontsource/barlow/400.css'
import '@fontsource/barlow/500.css'
import '@fontsource/barlow/600.css'
import './estilos/tokens.css'
import './estilos/base.css'
import './estilos/componentes.css'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.tsx'
import { aplicarColoresDeMarca } from './marca'

aplicarColoresDeMarca()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
