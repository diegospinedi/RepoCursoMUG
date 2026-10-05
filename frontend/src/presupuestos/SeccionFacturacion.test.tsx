import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Presupuesto } from './apiPresupuestos'
import { SeccionFacturacion } from './SeccionFacturacion'

const final: Presupuesto = {
  numero: 155,
  fecha: '2026-10-05',
  estado: 'Final',
  cliente: { apellido: 'González', nombre: 'María', dni: '23456789', domicilio: null, email: null, telefono: null },
  lineas: [],
  total: 1815,
  factura: null,
  facturacionPendiente: false,
}

const facturaEmitida = {
  letra: 'B', comprobante: '0003-00034561', puntoVenta: 3, numero: 34561, fecha: '2026-10-05', receptor: 'Consumidor Final',
  importeTotal: 1815, importeNeto: 1500, importeIva: 315, alicuotaIva: 21, cae: '76123456789012', vencimientoCae: '2026-10-15',
  numeroPresupuesto: 155, lineas: [],
}

function json(estado: number, cuerpo: unknown) {
  return Promise.resolve(new Response(JSON.stringify(cuerpo), { status: estado }))
}

let confirmar: ReturnType<typeof vi.spyOn>
beforeEach(() => {
  confirmar = vi.spyOn(window, 'confirm').mockReturnValue(true)
})
afterEach(() => {
  confirmar.mockRestore()
  vi.unstubAllGlobals()
})

describe('facturación del presupuesto', () => {
  it('AC-17: en Borrador el botón Facturar está deshabilitado', () => {
    render(<SeccionFacturacion presupuesto={{ ...final, estado: 'Borrador' }} alFacturar={() => {}} />)

    expect(screen.getByRole('button', { name: 'Facturar' })).toBeDisabled()
    expect(screen.getByText('Se puede facturar cuando el presupuesto está en estado Final.')).toBeInTheDocument()
  })

  it('AC-18: pide confirmación, emite y muestra el número de comprobante', async () => {
    const fetch = vi.fn(() => json(201, facturaEmitida))
    vi.stubGlobal('fetch', fetch)
    const alFacturar = vi.fn()
    render(<SeccionFacturacion presupuesto={final} alFacturar={alFacturar} />)

    fireEvent.click(screen.getByRole('button', { name: 'Facturar' }))

    expect(await screen.findByText('Factura B 0003-00034561 autorizada por ARCA. CAE 76123456789012.')).toBeInTheDocument()
    expect(confirmar).toHaveBeenCalledWith(expect.stringContaining('no se puede modificar ni anular'))
    expect(fetch).toHaveBeenCalledWith('/api/presupuestos/155/factura', expect.objectContaining({ method: 'POST' }))
    expect(alFacturar).toHaveBeenCalled()
  })

  it('si no se confirma, no emite', () => {
    confirmar.mockReturnValue(false)
    const fetch = vi.fn()
    vi.stubGlobal('fetch', fetch)
    render(<SeccionFacturacion presupuesto={final} alFacturar={() => {}} />)

    fireEvent.click(screen.getByRole('button', { name: 'Facturar' }))

    expect(fetch).not.toHaveBeenCalled()
  })

  it('AC-20: si ARCA rechaza muestra código y descripción y permite reintentar', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        json(422, {
          detail: 'ARCA rechazó la factura. Corregí lo indicado y volvé a intentar.',
          erroresArca: [{ codigo: 10016, mensaje: 'El número de comprobante no es el próximo a autorizar.' }],
        }),
      ),
    )
    render(<SeccionFacturacion presupuesto={final} alFacturar={() => {}} />)

    fireEvent.click(screen.getByRole('button', { name: 'Facturar' }))

    expect(await screen.findByText('Código 10016: El número de comprobante no es el próximo a autorizar.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reintentar facturación' })).toBeEnabled()
  })

  it('AC-60: si ARCA no responde muestra el motivo', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(503, { detail: 'ARCA no respondió en 30 segundos. Podés reintentar más tarde.', pendiente: true })))
    render(<SeccionFacturacion presupuesto={final} alFacturar={() => {}} />)

    fireEvent.click(screen.getByRole('button', { name: 'Facturar' }))

    expect(await screen.findByText('ARCA no respondió en 30 segundos. Podés reintentar más tarde.')).toBeInTheDocument()
  })

  it('mientras espera a ARCA avisa que puede tardar y no deja volver a pulsar', async () => {
    let responder: (r: Response) => void = () => {}
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((r) => (responder = r))))
    render(<SeccionFacturacion presupuesto={final} alFacturar={() => {}} />)

    fireEvent.click(screen.getByRole('button', { name: 'Facturar' }))

    expect(await screen.findByRole('button', { name: 'Esperando respuesta de ARCA…' })).toBeDisabled()
    expect(screen.getByText('Puede tardar hasta 30 segundos.')).toBeInTheDocument()
    responder(new Response(JSON.stringify(facturaEmitida), { status: 201 }))
    await waitFor(() => expect(screen.getByText(/autorizada por ARCA/)).toBeInTheDocument())
  })

  it('con una emisión pendiente explica el reintento seguro', () => {
    render(<SeccionFacturacion presupuesto={{ ...final, facturacionPendiente: true }} alFacturar={() => {}} />)

    expect(screen.getByText(/primero consulta si ARCA la autorizó/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reintentar facturación' })).toBeEnabled()
  })

  it('con factura emitida muestra comprobante y CAE, sin botón para facturar de nuevo', () => {
    render(
      <SeccionFacturacion
        presupuesto={{ ...final, factura: { letra: 'B', comprobante: '0003-00034561', fecha: '2026-10-05', cae: '76123456789012' } }}
        alFacturar={() => {}}
      />,
    )

    expect(screen.getByText('Factura B 0003-00034561')).toBeInTheDocument()
    expect(screen.getByText('76123456789012')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Facturar/ })).not.toBeInTheDocument()
  })
})
