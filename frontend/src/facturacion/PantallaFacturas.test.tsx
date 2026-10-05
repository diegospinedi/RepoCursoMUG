import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PantallaFacturas } from './PantallaFacturas'

const pagina = {
  facturas: [
    {
      letra: 'B', comprobante: '0003-00034561', fecha: '2026-03-20', apellido: 'González', nombre: 'María', dni: '23456789',
      importeTotal: 1815, cae: '76123456789012', numeroPresupuesto: 155,
    },
  ],
  total: 1,
  pagina: 1,
  tamanoPagina: 50,
}

function json(estado: number, cuerpo: unknown) {
  return Promise.resolve(new Response(JSON.stringify(cuerpo), { status: estado }))
}

function renderizarEn(ruta: string) {
  return render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/facturas" element={<PantallaFacturas />} />
      </Routes>
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('facturas emitidas', () => {
  it('AC-33: lista comprobante, fecha, cliente, DNI, total, CAE y presupuesto de origen', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, pagina)))
    renderizarEn('/facturas')

    const fila = (await screen.findByRole('link', { name: 'B 0003-00034561' })).closest('tr')!
    expect(within(fila).getByText('20/03/2026')).toBeInTheDocument()
    expect(within(fila).getByText('González, María')).toBeInTheDocument()
    expect(within(fila).getByText('23.456.789')).toBeInTheDocument()
    expect(within(fila).getByText(/1\.815,00/)).toBeInTheDocument()
    expect(within(fila).getByText('76123456789012')).toBeInTheDocument()
    expect(within(fila).getByRole('link', { name: '155' })).toHaveAttribute('href', '/presupuestos/155')
  })

  it('AC-32: no ofrece acciones de edición ni eliminación', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, pagina)))
    renderizarEn('/facturas')

    await screen.findByRole('link', { name: 'B 0003-00034561' })
    expect(screen.queryByRole('button', { name: /editar|modificar|eliminar|borrar|anular/i })).not.toBeInTheDocument()
  })

  it('AC-22 y AC-88: envía comprobante, apellido y fecha juntos', async () => {
    const fetch = vi.fn(() => json(200, pagina))
    vi.stubGlobal('fetch', fetch)
    renderizarEn('/facturas')

    fireEvent.change(await screen.findByLabelText('Nº de comprobante'), { target: { value: '34561' } })
    fireEvent.change(screen.getByLabelText('Apellido'), { target: { value: 'gonzalez' } })
    fireEvent.change(screen.getByLabelText('Fecha desde'), { target: { value: '2026-03-15' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))

    await waitFor(() =>
      expect(fetch).toHaveBeenLastCalledWith('/api/facturas?desde=2026-03-15&comprobante=34561&apellido=gonzalez', expect.anything()),
    )
  })

  it('muestra junto al campo los errores de la API', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(400, { errors: { comprobante: ['Ingresá los números del comprobante, por ejemplo 0003-00034561 o 34561'] } })))
    renderizarEn('/facturas?comprobante=abc')

    expect(await screen.findByText(/Ingresá los números del comprobante/)).toBeInTheDocument()
    expect(screen.getByLabelText('Nº de comprobante')).toHaveAttribute('aria-invalid', 'true')
  })

  it('indica cuando todavía no hay facturas', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, { facturas: [], total: 0, pagina: 1, tamanoPagina: 50 })))
    renderizarEn('/facturas')

    expect(await screen.findByText('Todavía no se emitieron facturas.')).toBeInTheDocument()
  })
})
