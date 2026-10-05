import type { ReactNode } from 'react'
import { marca } from './marca'
import './Layout.css'

export function Layout({ children }: { children: ReactNode }) {
  return (
    <div className="layout">
      <header className="layout-encabezado">
        <img className="layout-logo" src={marca.logo} alt="Óptica Sistema" />
      </header>
      <main className="layout-contenido">{children}</main>
    </div>
  )
}
