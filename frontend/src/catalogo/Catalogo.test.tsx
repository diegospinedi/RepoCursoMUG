import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PantallaArticulo } from './PantallaArticulo'
import { PantallaCatalogo } from './PantallaCatalogo'

const articulo = {
  codigo: 7,
  codigoProveedor: 'ABC-1',
  descripcion: 'Armazón acetato negro',
  precioCosto: 1210,
  margenUtilidad: 50,
  precioVenta: 1815,
}

function json(estado: number, cuerpo: unknown) {
  return Promise.resolve(new Response(JSON.stringify(cuerpo), { status: estado }))
}

function renderizarEn(ruta: string) {
  return render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/catalogo" element={<PantallaCatalogo />} />
        <Route path="/catalogo/nuevo" element={<PantallaArticulo />} />
        <Route path="/catalogo/:codigo" element={<PantallaArticulo />} />
      </Routes>
    </MemoryRouter>,
  )
}

afterEach(() => vi.unstubAllGlobals())

describe('listado del catálogo', () => {
  it('muestra los artículos con importes en formato argentino', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, { articulos: [articulo], total: 1, pagina: 1, tamanoPagina: 50 })))
    renderizarEn('/catalogo')

    const fila = (await screen.findByRole('link', { name: 'Armazón acetato negro' })).closest('tr')!
    expect(within(fila).getByText(/1\.210,00/)).toBeInTheDocument()
    expect(within(fila).getByText(/1\.815,00/)).toBeInTheDocument()
    expect(within(fila).getByText('50 %')).toBeInTheDocument()
  })

  it('busca el texto ingresado al presionar Buscar (RF-11)', async () => {
    const fetch = vi.fn(() => json(200, { articulos: [], total: 0, pagina: 1, tamanoPagina: 50 }))
    vi.stubGlobal('fetch', fetch)
    renderizarEn('/catalogo')

    fireEvent.change(await screen.findByLabelText(/Buscar por código/), { target: { value: 'armazón' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))

    await waitFor(() => expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('texto=armaz%C3%B3n'), expect.anything()))
    expect(await screen.findByText('No hay artículos que coincidan con "armazón".')).toBeInTheDocument()
  })
})

describe('formulario de artículo', () => {
  it('muestra el precio de venta como solo lectura (AC-81)', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(200, articulo)))
    renderizarEn('/catalogo/7')

    const precioVenta = await screen.findByLabelText('Precio de venta')
    await waitFor(() => expect(precioVenta).toHaveValue('$ 1.815,00'))
    expect(precioVenta).toHaveAttribute('readonly')
  })

  it('valida junto a cada campo antes de enviar (RF-35)', async () => {
    const fetch = vi.fn()
    vi.stubGlobal('fetch', fetch)
    renderizarEn('/catalogo/nuevo')

    fireEvent.change(screen.getByLabelText('Margen de utilidad (%)'), { target: { value: '150' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Ingresá el código del artículo en el proveedor')).toBeInTheDocument()
    expect(screen.getByText('Ingresá la descripción del artículo')).toBeInTheDocument()
    expect(screen.getByText('Ingresá un precio de costo mayor a 0')).toBeInTheDocument()
    expect(screen.getByText('Ingresá un margen de utilidad entre 0 y 100')).toBeInTheDocument()
    expect(fetch).not.toHaveBeenCalled()
  })

  it('crea el artículo y muestra el código y el precio de venta asignados (AC-30)', async () => {
    const fetch = vi.fn((_: string, opciones?: RequestInit) =>
      opciones?.method === 'POST' ? json(201, articulo) : json(200, articulo),
    )
    vi.stubGlobal('fetch', fetch)
    renderizarEn('/catalogo/nuevo')

    fireEvent.change(screen.getByLabelText('Código en el proveedor'), { target: { value: 'ABC-1' } })
    fireEvent.change(screen.getByLabelText('Descripción'), { target: { value: 'Armazón acetato negro' } })
    fireEvent.change(screen.getByLabelText('Precio de costo ($)'), { target: { value: '1.210' } })
    fireEvent.change(screen.getByLabelText('Margen de utilidad (%)'), { target: { value: '50' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText(/Artículo 7 guardado\. Precio de venta: \$\s1\.815,00\./)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Artículo 7' })).toBeInTheDocument()
    const [, opciones] = fetch.mock.calls.find(([, o]) => o?.method === 'POST')!
    expect(JSON.parse(opciones!.body as string)).toEqual({
      codigoProveedor: 'ABC-1',
      descripcion: 'Armazón acetato negro',
      precioCosto: 1210,
      margenUtilidad: 50,
    })
  })

  it('al modificar el margen muestra el precio de venta recalculado (AC-70)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((_: string, opciones?: RequestInit) =>
        opciones?.method === 'PUT'
          ? json(200, { ...articulo, margenUtilidad: 60, precioVenta: 1936 })
          : json(200, articulo),
      ),
    )
    renderizarEn('/catalogo/7')

    fireEvent.change(await screen.findByLabelText('Margen de utilidad (%)'), { target: { value: '60' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => expect(screen.getByLabelText('Precio de venta')).toHaveValue('$ 1.936,00'))
  })
})
