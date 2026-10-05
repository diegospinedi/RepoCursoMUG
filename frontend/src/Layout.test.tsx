import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import branding from '../../Marca/branding.json'
import { Layout } from './Layout'
import { aplicarColoresDeMarca } from './marca'

describe('marca de la óptica (RF-34, AC-33)', () => {
  it('muestra el logo de Marca/logo.png en el encabezado', () => {
    render(<Layout>contenido</Layout>)

    const logo = screen.getByRole('img', { name: 'Óptica Sistema' })
    expect(logo.getAttribute('src')).toMatch(/logo\.png$/)
  })

  it('aplica el color primario y el fondo definidos en branding.json', () => {
    aplicarColoresDeMarca()

    const estilo = document.documentElement.style
    expect(estilo.getPropertyValue('--color-primario')).toBe('#0903A0')
    expect(estilo.getPropertyValue('--color-fondo')).toBe('#FFFFFF')
    expect(branding.colores.primario).toBe('#0903A0')
  })
})
