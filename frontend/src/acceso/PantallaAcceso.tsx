import type { ReactNode } from 'react'
import { marca } from '../marca'
import './PantallaAcceso.css'

/** Marco común de las pantallas de ingreso y de definición de contraseña. */
export function PantallaAcceso({ titulo, ayuda, children }: { titulo: string; ayuda: string; children: ReactNode }) {
  return (
    <main className="acceso">
      <section className="tarjeta acceso-tarjeta">
        <img className="acceso-logo" src={marca.logo} alt="Óptica Sistema" />
        <h1>{titulo}</h1>
        <p className="acceso-ayuda">{ayuda}</p>
        {children}
      </section>
    </main>
  )
}
