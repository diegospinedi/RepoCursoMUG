import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PantallaConfiguracion } from './PantallaConfiguracion'

const guardada = {
  alicuotaIva: 21,
  condicionFiscal: 'ResponsableInscripto',
  topeIdentificacion: 10000000,
  multiploRedondeo: 0.01,
}

function json(estado: number, cuerpo: unknown) {
  return Promise.resolve(new Response(JSON.stringify(cuerpo), { status: estado }))
}

afterEach(() => vi.unstubAllGlobals())

describe('pantalla de configuración', () => {
  it('muestra los valores guardados con formato argentino', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, guardada)))
    render(<PantallaConfiguracion />)

    expect(await screen.findByLabelText('Alícuota de IVA (%)')).toHaveValue('21')
    expect(screen.getByLabelText('Múltiplo de redondeo comercial ($)')).toHaveValue('0,01')
    expect(screen.getByLabelText('Tope de identificación del receptor ($)')).toHaveValue('10.000.000')
    expect(screen.getByLabelText('Condición fiscal')).toHaveValue('ResponsableInscripto')
  })

  it('no acepta un múltiplo de 0,001 e indica el mínimo (AC-82)', async () => {
    const fetch = vi.fn(() => json(200, guardada))
    vi.stubGlobal('fetch', fetch)
    render(<PantallaConfiguracion />)

    fireEvent.change(await screen.findByLabelText('Múltiplo de redondeo comercial ($)'), { target: { value: '0,001' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('El valor mínimo es 0,01')).toBeInTheDocument()
    expect(screen.getByLabelText('Múltiplo de redondeo comercial ($)')).toHaveAttribute('aria-invalid', 'true')
    expect(fetch).toHaveBeenCalledTimes(1) // solo la lectura inicial
  })

  it('graba los valores escritos con coma decimal (AC-49)', async () => {
    const fetch = vi.fn((_: string, opciones?: RequestInit) =>
      opciones?.method === 'PUT'
        ? json(200, { ...JSON.parse(opciones.body as string), preciosActualizados: 0 })
        : json(200, guardada),
    )
    vi.stubGlobal('fetch', fetch)
    render(<PantallaConfiguracion />)

    fireEvent.change(await screen.findByLabelText('Alícuota de IVA (%)'), { target: { value: '10,5' } })
    fireEvent.change(screen.getByLabelText('Condición fiscal'), { target: { value: 'Monotributo' } })
    fireEvent.change(screen.getByLabelText('Tope de identificación del receptor ($)'), { target: { value: '380.000,50' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Configuración guardada.')).toBeInTheDocument()
    const [, opciones] = fetch.mock.calls.find(([, o]) => o?.method === 'PUT')!
    expect(JSON.parse(opciones!.body as string)).toEqual({
      alicuotaIva: 10.5,
      condicionFiscal: 'Monotributo',
      topeIdentificacion: 380000.5,
      multiploRedondeo: 0.01,
    })
  })

  it('muestra junto al campo el error que devuelve la API (RF-35)', async () => {
    const mensaje = 'El múltiplo puede tener como máximo 2 decimales, por ejemplo 0,01, 10 o 50'
    vi.stubGlobal(
      'fetch',
      vi.fn((_: string, opciones?: RequestInit) =>
        opciones?.method === 'PUT' ? json(400, { errors: { multiploRedondeo: [mensaje] } }) : json(200, guardada),
      ),
    )
    render(<PantallaConfiguracion />)

    fireEvent.change(await screen.findByLabelText('Múltiplo de redondeo comercial ($)'), { target: { value: '0,015' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => expect(screen.getByText(mensaje)).toBeInTheDocument())
  })

  it('informa cuántos precios del catálogo se recalcularon (RF-86)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((_: string, opciones?: RequestInit) =>
        opciones?.method === 'PUT'
          ? json(200, { ...JSON.parse(opciones.body as string), preciosActualizados: 37 })
          : json(200, guardada),
      ),
    )
    render(<PantallaConfiguracion />)

    fireEvent.change(await screen.findByLabelText('Múltiplo de redondeo comercial ($)'), { target: { value: '50' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText('Configuración guardada. Se actualizó el precio de venta de 37 artículos del catálogo.'),
    ).toBeInTheDocument()
  })
})
