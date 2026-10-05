import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PantallaPresupuestos } from './PantallaPresupuestos'

const pagina = {
  presupuestos: [
    { numero: 156, fecha: '2026-03-20', estado: 'Final', apellido: 'González', nombre: 'María', dni: '23456789', total: 435800 },
    { numero: 155, fecha: '2026-03-10', estado: 'Borrador', apellido: 'Gómez', nombre: 'Juan', dni: '30111222', total: 1850 },
  ],
  total: 2,
  pagina: 1,
  tamanoPagina: 50,
}

function json(estado: number, cuerpo: unknown) {
  return Promise.resolve(new Response(JSON.stringify(cuerpo), { status: estado }))
}

function UbicacionActual() {
  const { pathname, search } = useLocation()
  return <output data-testid="ubicacion">{pathname + search}</output>
}

function renderizarEn(ruta: string) {
  return render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/presupuestos" element={<PantallaPresupuestos />} />
        <Route path="/presupuestos/:numero" element={<p>Presupuesto abierto</p>} />
      </Routes>
      <UbicacionActual />
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('búsqueda de presupuestos', () => {
  it('muestra número, fecha, cliente, DNI, estado y total con formato argentino', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, pagina)))
    renderizarEn('/presupuestos')

    const fila = (await screen.findByRole('link', { name: 'González, María' })).closest('tr')!
    expect(within(fila).getByText('20/03/2026')).toBeInTheDocument()
    expect(within(fila).getByText('23.456.789')).toBeInTheDocument()
    expect(within(fila).getByText('Final')).toBeInTheDocument()
    expect(within(fila).getByText(/435\.800,00/)).toBeInTheDocument()
  })

  it('AC-68: envía todos los filtros cargados juntos y los deja en la URL', async () => {
    const fetch = vi.fn(() => json(200, pagina))
    vi.stubGlobal('fetch', fetch)
    renderizarEn('/presupuestos')

    fireEvent.change(await screen.findByLabelText('Apellido'), { target: { value: 'González' } })
    fireEvent.change(screen.getByLabelText('Fecha desde'), { target: { value: '2026-03-15' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))

    await waitFor(() =>
      expect(fetch).toHaveBeenLastCalledWith('/api/presupuestos?desde=2026-03-15&apellido=Gonz%C3%A1lez', expect.anything()),
    )
    expect(screen.getByTestId('ubicacion')).toHaveTextContent('/presupuestos?desde=2026-03-15&apellido=Gonz%C3%A1lez')
  })

  it('al volver a la pantalla conserva la búsqueda de la URL', async () => {
    const fetch = vi.fn(() => json(200, pagina))
    vi.stubGlobal('fetch', fetch)
    renderizarEn('/presupuestos?dni=3456')

    expect(await screen.findByLabelText('DNI')).toHaveValue('3456')
    expect(fetch).toHaveBeenCalledWith('/api/presupuestos?dni=3456', expect.anything())
  })

  it('avisa si Hasta es anterior a Desde sin consultar a la API', async () => {
    const fetch = vi.fn(() => json(200, pagina))
    vi.stubGlobal('fetch', fetch)
    renderizarEn('/presupuestos')
    await screen.findByRole('link', { name: 'González, María' })

    fireEvent.change(screen.getByLabelText('Fecha desde'), { target: { value: '2026-03-20' } })
    fireEvent.change(screen.getByLabelText('Fecha hasta'), { target: { value: '2026-03-10' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))

    expect(await screen.findByText('La fecha Hasta no puede ser anterior a la fecha Desde')).toBeInTheDocument()
    expect(fetch).toHaveBeenCalledTimes(1)
  })

  it('muestra junto al campo los errores de la API', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(400, { errors: { dni: ['Ingresá solo los números del DNI, con o sin puntos'] } })))
    renderizarEn('/presupuestos?dni=abc')

    expect(await screen.findByText('Ingresá solo los números del DNI, con o sin puntos')).toBeInTheDocument()
    expect(screen.getByLabelText('DNI')).toHaveAttribute('aria-invalid', 'true')
  })

  it('indica cuando ningún presupuesto cumple todos los filtros', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, { presupuestos: [], total: 0, pagina: 1, tamanoPagina: 50 })))
    renderizarEn('/presupuestos?apellido=zzz')

    expect(await screen.findByText('No hay presupuestos que cumplan todos los filtros.')).toBeInTheDocument()
  })

  it('abre un presupuesto por número', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, pagina)))
    renderizarEn('/presupuestos')

    fireEvent.change(screen.getByLabelText('Número de presupuesto'), { target: { value: '155' } })
    fireEvent.click(screen.getByRole('button', { name: 'Abrir' }))

    expect(await screen.findByText('Presupuesto abierto')).toBeInTheDocument()
    expect(screen.getByTestId('ubicacion')).toHaveTextContent('/presupuestos/155')
  })
})
