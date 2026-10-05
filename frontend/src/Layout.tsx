import type { ReactNode } from 'react'
import { marca } from './marca'
import './Layout.css'

type Props = {
  children: ReactNode
  /** Navegación o acciones de sesión, a la derecha del logo. */
  acciones?: ReactNode
}

export function Layout({ children, acciones }: Props) {
  return (
    <div className="layout">
      <header className="layout-encabezado">
        <div className="layout-encabezado-interior">
          <img className="layout-logo" src={marca.logo} alt="Óptica Sistema" />
          {acciones && <div className="layout-acciones">{acciones}</div>}
        </div>
      </header>
      <main className="layout-contenido">{children}</main>
    </div>
  )
}
