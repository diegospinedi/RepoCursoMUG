import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PantallaPresupuesto } from './PantallaPresupuesto'

const armazon = { codigo: 7, codigoProveedor: 'ABC-1', descripcion: 'Armazón acetato negro', precioCosto: 1210, margenUtilidad: 50, precioVenta: 1850 }
const lente = { codigo: 8, codigoProveedor: 'L-1', descripcion: 'Lente orgánico', precioCosto: 6.67, margenUtilidad: 0, precioVenta: 6.67 }

const grabado = {
  numero: 155,
  fecha: '2026-10-05',
  estado: 'Borrador',
  cliente: { apellido: 'González', nombre: 'María', dni: '23456789', domicilio: null, email: null, telefono: null },
  lineas: [
    { codigoArticulo: 7, descripcion: 'Armazón acetato negro', precioUnitario: 1850, cantidad: 1, porcentajeDescuento: 0, precioConDescuento: 1850, precioFinal: 1850 },
  ],
  total: 1850,
}

function json(estado: number, cuerpo: unknown) {
  return Promise.resolve(new Response(JSON.stringify(cuerpo), { status: estado }))
}

/** Simula la API: búsqueda de artículos, alta, lectura y modificación de presupuestos. */
function simularApi(presupuesto: unknown = grabado) {
  const fetch = vi.fn((url: string, opciones?: RequestInit) => {
    if (url.startsWith('/api/articulos'))
      return json(200, { articulos: url.includes('lente') ? [lente] : [armazon], total: 1, pagina: 1, tamanoPagina: 50 })
    if (opciones?.method === 'POST') return json(201, presupuesto)
    if (opciones?.method === 'PUT') return json(200, presupuesto)
    return json(200, presupuesto)
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}

function renderizarEn(ruta: string) {
  return render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/presupuestos/nuevo" element={<PantallaPresupuesto />} />
        <Route path="/presupuestos/:numero" element={<PantallaPresupuesto />} />
      </Routes>
    </MemoryRouter>,
  )
}

function cargarCliente(datos: { apellido?: string; nombre?: string; dni?: string } = {}) {
  const { apellido = 'González', nombre = 'María', dni = '23.456.789' } = datos
  fireEvent.change(screen.getByLabelText(/^Apellido/), { target: { value: apellido } })
  fireEvent.change(screen.getByLabelText(/^Nombre/), { target: { value: nombre } })
  fireEvent.change(screen.getByLabelText(/^DNI/), { target: { value: dni } })
}

async function agregarArticulo(texto: string) {
  fireEvent.change(screen.getByLabelText(/Agregar artículo/), { target: { value: texto } })
  fireEvent.click(screen.getByRole('button', { name: 'Buscar' }))
  const resultados = await screen.findByRole('table', { name: 'Artículos encontrados' })
  fireEvent.click(within(resultados).getByRole('button', { name: 'Agregar' }))
}

afterEach(() => vi.unstubAllGlobals())

describe('pantalla de presupuesto', () => {
  it('AC-10 y AC-40: al elegir un artículo completa código, descripción y precio de venta del catálogo', async () => {
    simularApi()
    renderizarEn('/presupuestos/nuevo')

    await agregarArticulo('armazon')

    const fila = screen.getByText('Armazón acetato negro').closest('tr')!
    expect(within(fila).getByText('7')).toBeInTheDocument()
    // Sin descuento y con cantidad 1, precio unitario, con descuento y final valen lo mismo.
    expect(within(fila).getAllByText(/1\.850,00/)).toHaveLength(3)
    expect(screen.getByLabelText('Cantidad de la línea 1')).toHaveValue('1')
    expect(screen.getByLabelText('Descuento de la línea 1')).toHaveValue('0')
  })

  it('AC-29 y AC-46: recalcula en vivo al cambiar cantidad y descuento, redondeando como el servidor', async () => {
    simularApi()
    renderizarEn('/presupuestos/nuevo')
    await agregarArticulo('lente')

    fireEvent.change(screen.getByLabelText('Cantidad de la línea 1'), { target: { value: '3' } })
    fireEvent.change(screen.getByLabelText('Descuento de la línea 1'), { target: { value: '50' } })

    expect(screen.getByTestId('con-descuento-0')).toHaveTextContent(/3,34/)
    expect(screen.getByTestId('precio-final-0')).toHaveTextContent(/10,02/)
    expect(screen.getByTestId('total')).toHaveTextContent(/10,02/)
  })

  it('AC-11: el total suma los precios finales redondeados de todas las líneas', async () => {
    simularApi()
    renderizarEn('/presupuestos/nuevo')
    await agregarArticulo('lente')
    await agregarArticulo('armazon')

    fireEvent.change(screen.getByLabelText('Cantidad de la línea 1'), { target: { value: '3' } })
    fireEvent.change(screen.getByLabelText('Descuento de la línea 1'), { target: { value: '50' } })

    expect(screen.getByTestId('total')).toHaveTextContent(/1\.860,02/) // 10,02 + 1.850,00
  })

  it('AC-34: sin DNI no graba y muestra el mensaje junto al campo', async () => {
    const fetch = simularApi()
    renderizarEn('/presupuestos/nuevo')
    await agregarArticulo('armazon')
    cargarCliente({ dni: '' })

    fireEvent.click(screen.getByRole('button', { name: 'Grabar' }))

    expect(await screen.findByText('Ingresá el DNI del cliente')).toBeInTheDocument()
    expect(screen.getByLabelText(/^DNI/)).toHaveAttribute('aria-invalid', 'true')
    expect(fetch).not.toHaveBeenCalledWith('/api/presupuestos', expect.anything())
  })

  it('AC-43 y AC-42: no acepta cantidad 2,5 ni descuento mayor a 100', async () => {
    const fetch = simularApi()
    renderizarEn('/presupuestos/nuevo')
    await agregarArticulo('armazon')
    cargarCliente()

    fireEvent.change(screen.getByLabelText('Cantidad de la línea 1'), { target: { value: '2,5' } })
    fireEvent.change(screen.getByLabelText('Descuento de la línea 1'), { target: { value: '120' } })
    fireEvent.click(screen.getByRole('button', { name: 'Grabar' }))

    expect(await screen.findByText('La cantidad debe ser un número entero mayor a 0')).toBeInTheDocument()
    expect(screen.getByText('El descuento debe estar entre 0 y 100')).toBeInTheDocument()
    expect(screen.getByTestId('precio-final-0')).toHaveTextContent('—')
    expect(fetch).not.toHaveBeenCalledWith('/api/presupuestos', expect.anything())
  })

  it('pide al menos un artículo', async () => {
    simularApi()
    renderizarEn('/presupuestos/nuevo')
    cargarCliente()

    fireEvent.click(screen.getByRole('button', { name: 'Grabar' }))

    expect(await screen.findByText('Agregá al menos un artículo al presupuesto')).toBeInTheDocument()
  })

  it('AC-04: graba, envía solo artículo, cantidad y descuento, y confirma el estado Borrador', async () => {
    const fetch = simularApi()
    renderizarEn('/presupuestos/nuevo')
    await agregarArticulo('armazon')
    cargarCliente()

    fireEvent.click(screen.getByRole('button', { name: 'Grabar' }))

    expect(await screen.findByText('Presupuesto 155 grabado en estado Borrador.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Presupuesto 155' })).toBeInTheDocument()
    const [, opciones] = fetch.mock.calls.find(([url, o]) => url === '/api/presupuestos' && o?.method === 'POST')!
    expect(JSON.parse(opciones!.body as string)).toEqual({
      cliente: { apellido: 'González', nombre: 'María', dni: '23.456.789', domicilio: null, email: null, telefono: null },
      lineas: [{ codigoArticulo: 7, cantidad: 1, porcentajeDescuento: 0 }],
      estado: 'Borrador',
    })
  })

  it('muestra junto al campo los errores que devuelve la API', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string, opciones?: RequestInit) =>
        url.startsWith('/api/articulos')
          ? json(200, { articulos: [armazon], total: 1, pagina: 1, tamanoPagina: 50 })
          : opciones?.method === 'POST'
            ? json(400, { errors: { 'cliente.email': ['Revisá el email: debe tener la forma nombre@dominio.com'] } })
            : json(200, grabado),
      ),
    )
    renderizarEn('/presupuestos/nuevo')
    await agregarArticulo('armazon')
    cargarCliente()
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'maria@' } })

    fireEvent.click(screen.getByRole('button', { name: 'Grabar' }))

    expect(await screen.findByText('Revisá el email: debe tener la forma nombre@dominio.com')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
  })

  it('AC-05: pasar a Final pide confirmación y envía el estado', async () => {
    const fetch = simularApi()
    const confirmar = vi.spyOn(window, 'confirm').mockReturnValue(true)
    renderizarEn('/presupuestos/155')

    fireEvent.change(await screen.findByLabelText('Estado'), { target: { value: 'Final' } })
    fireEvent.click(screen.getByRole('button', { name: 'Grabar' }))

    await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/presupuestos/155', expect.objectContaining({ method: 'PUT' })))
    expect(confirmar).toHaveBeenCalled()
    const [, opciones] = fetch.mock.calls.find(([, o]) => o?.method === 'PUT')!
    expect(JSON.parse(opciones!.body as string).estado).toBe('Final')
    confirmar.mockRestore()
  })

  it('si no se confirma el pase a Final, no graba', async () => {
    const fetch = simularApi()
    const confirmar = vi.spyOn(window, 'confirm').mockReturnValue(false)
    renderizarEn('/presupuestos/155')

    fireEvent.change(await screen.findByLabelText('Estado'), { target: { value: 'Final' } })
    fireEvent.click(screen.getByRole('button', { name: 'Grabar' }))

    expect(fetch).not.toHaveBeenCalledWith('/api/presupuestos/155', expect.objectContaining({ method: 'PUT' }))
    confirmar.mockRestore()
  })

  it('AC-66: un presupuesto Final se muestra sin posibilidad de edición', async () => {
    simularApi({ ...grabado, estado: 'Final' })
    renderizarEn('/presupuestos/155')

    expect(await screen.findByText(/no se puede modificar/)).toBeInTheDocument()
    expect(screen.getByLabelText(/^Apellido/)).toBeDisabled()
    expect(screen.getByLabelText('Cantidad de la línea 1')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Grabar' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/Agregar artículo/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Quitar/ })).not.toBeInTheDocument()
  })

  it('AC-28: no discrimina IVA en líneas ni total', async () => {
    simularApi()
    renderizarEn('/presupuestos/155')

    await screen.findByText('Armazón acetato negro')
    expect(screen.queryByText(/neto/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/^IVA/)).not.toBeInTheDocument()
    expect(screen.getByText('Precios finales, IVA incluido.')).toBeInTheDocument()
  })
})
