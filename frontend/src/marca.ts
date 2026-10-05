// Fuente única del branding de la óptica (RF-34): no copiar estos valores a mano.
import branding from '../../Marca/branding.json'
import logo from '../../Marca/logo.png'

export const marca = {
  logo,
  colorPrimario: branding.colores.primario,
  colorFondo: branding.colores.fondo,
}

export function aplicarColoresDeMarca(raiz: HTMLElement = document.documentElement) {
  raiz.style.setProperty('--color-primario', marca.colorPrimario)
  raiz.style.setProperty('--color-fondo', marca.colorFondo)
}
