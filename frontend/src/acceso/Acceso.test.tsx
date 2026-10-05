import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ContrasenaInicial } from './ContrasenaInicial'
import { Ingreso } from './Ingreso'

function respuestaApi(estado: number, cuerpo?: unknown) {
  return Promise.resolve(new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status: estado }))
}

afterEach(() => vi.unstubAllGlobals())

describe('definición de la contraseña inicial', () => {
  it('no acepta 7 caracteres e indica el mínimo de 8 (AC-57)', async () => {
    const fetch = vi.fn()
    vi.stubGlobal('fetch', fetch)
    render(<ContrasenaInicial alDefinir={() => {}} />)

    fireEvent.change(screen.getByLabelText('Contraseña nueva'), { target: { value: '1234567' } })
    fireEvent.change(screen.getByLabelText('Repetí la contraseña'), { target: { value: '1234567' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar y entrar' }))

    expect(await screen.findByText('La contraseña debe tener al menos 8 caracteres')).toBeInTheDocument()
    expect(screen.getByLabelText('Contraseña nueva')).toHaveAttribute('aria-invalid', 'true')
    expect(fetch).not.toHaveBeenCalled()
  })

  it('avisa si la confirmación no coincide', async () => {
    vi.stubGlobal('fetch', vi.fn())
    render(<ContrasenaInicial alDefinir={() => {}} />)

    fireEvent.change(screen.getByLabelText('Contraseña nueva'), { target: { value: 'clave-segura-1' } })
    fireEvent.change(screen.getByLabelText('Repetí la contraseña'), { target: { value: 'clave-segura-2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar y entrar' }))

    expect(await screen.findByText(/no coinciden/)).toBeInTheDocument()
  })

  it('entra al sistema cuando la API acepta la contraseña', async () => {
    vi.stubGlobal('fetch', vi.fn(() => respuestaApi(204)))
    const alDefinir = vi.fn()
    render(<ContrasenaInicial alDefinir={alDefinir} />)

    fireEvent.change(screen.getByLabelText('Contraseña nueva'), { target: { value: 'clave-segura-1' } })
    fireEvent.change(screen.getByLabelText('Repetí la contraseña'), { target: { value: 'clave-segura-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar y entrar' }))

    await waitFor(() => expect(alDefinir).toHaveBeenCalled())
  })
})

describe('ingreso', () => {
  it('muestra junto al campo el error que informa la API (RF-35)', async () => {
    const mensaje = 'Contraseña incorrecta. Revisala y volvé a intentar (quedan 4 intentos).'
    vi.stubGlobal('fetch', vi.fn(() => respuestaApi(401, { errors: { contrasena: [mensaje] } })))
    const alIngresar = vi.fn()
    render(<Ingreso alIngresar={alIngresar} />)

    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'incorrecta' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByText(mensaje)).toBeInTheDocument()
    expect(alIngresar).not.toHaveBeenCalled()
  })

  it('muestra el bloqueo por intentos fallidos (AC-58)', async () => {
    const mensaje = 'Acceso bloqueado por demasiados intentos fallidos. Esperá 5 minuto(s) y volvé a intentar.'
    vi.stubGlobal('fetch', vi.fn(() => respuestaApi(429, { errors: { contrasena: [mensaje] } })))
    render(<Ingreso alIngresar={() => {}} />)

    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'clave-segura-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByText(mensaje)).toBeInTheDocument()
  })

  it('ingresa con la contraseña correcta', async () => {
    vi.stubGlobal('fetch', vi.fn(() => respuestaApi(204)))
    const alIngresar = vi.fn()
    render(<Ingreso alIngresar={alIngresar} />)

    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'clave-segura-1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    await waitFor(() => expect(alIngresar).toHaveBeenCalled())
  })
})
