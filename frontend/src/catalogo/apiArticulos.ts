import { pedir } from '../api'

export type Articulo = {
  codigo: number
  codigoProveedor: string
  descripcion: string
  precioCosto: number
  margenUtilidad: number
  precioVenta: number
}

/** Lo que carga la operadora: el código y el precio de venta los asigna el sistema (RF-20, RF-69). */
export type DatosArticulo = Pick<Articulo, 'codigoProveedor' | 'descripcion' | 'precioCosto' | 'margenUtilidad'>

export type PaginaArticulos = {
  articulos: Articulo[]
  total: number
  pagina: number
  tamanoPagina: number
}

export const apiArticulos = {
  buscar: (texto: string, pagina: number) =>
    pedir<PaginaArticulos>(`/api/articulos?${new URLSearchParams({ texto, pagina: String(pagina) })}`),
  obtener: (codigo: number) => pedir<Articulo>(`/api/articulos/${codigo}`),
  crear: (datos: DatosArticulo) =>
    pedir<Articulo>('/api/articulos', { method: 'POST', body: JSON.stringify(datos) }),
  modificar: (codigo: number, datos: DatosArticulo) =>
    pedir<Articulo>(`/api/articulos/${codigo}`, { method: 'PUT', body: JSON.stringify(datos) }),
}
