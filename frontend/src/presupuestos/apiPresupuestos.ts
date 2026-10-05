import { pedir } from '../api'

export type EstadoPresupuesto = 'Borrador' | 'Final'

export type Cliente = {
  apellido: string
  nombre: string
  dni: string
  domicilio: string | null
  email: string | null
  telefono: string | null
}

export type Linea = {
  codigoArticulo: number
  descripcion: string
  precioUnitario: number
  cantidad: number
  porcentajeDescuento: number
  precioConDescuento: number
  precioFinal: number
}

export type FacturaResumen = { letra: string; comprobante: string; fecha: string; cae: string }

export type Presupuesto = {
  numero: number
  fecha: string
  estado: EstadoPresupuesto
  cliente: Cliente
  lineas: Linea[]
  total: number
  factura?: FacturaResumen | null
  /** Una emisión quedó sin respuesta de ARCA y hay que reintentarla (RF-52). */
  facturacionPendiente?: boolean
}

/**
 * Lo que envía la pantalla. La descripción y el precio unitario no se envían:
 * el servidor los toma del catálogo o de la línea ya grabada.
 */
export type DatosPresupuesto = {
  cliente: Cliente
  lineas: { codigoArticulo: number; cantidad: number | null; porcentajeDescuento: number | null }[]
  estado?: EstadoPresupuesto
}

export type ResumenPresupuesto = {
  numero: number
  fecha: string
  estado: EstadoPresupuesto
  apellido: string
  nombre: string
  dni: string
  total: number
}

export type PaginaPresupuestos = {
  presupuestos: ResumenPresupuesto[]
  total: number
  pagina: number
  tamanoPagina: number
}

/** Filtros de RF-03; los vacíos no se envían. Fechas en formato ISO (aaaa-mm-dd). */
export type FiltrosPresupuestos = {
  desde?: string
  hasta?: string
  apellido?: string
  nombre?: string
  dni?: string
  pagina?: string
}

export const apiPresupuestos = {
  buscar: (filtros: FiltrosPresupuestos) => {
    const parametros = new URLSearchParams(Object.entries(filtros).filter(([, v]) => v) as [string, string][])
    return pedir<PaginaPresupuestos>(`/api/presupuestos?${parametros}`)
  },
  obtener: (numero: number) => pedir<Presupuesto>(`/api/presupuestos/${numero}`),
  crear: (datos: DatosPresupuesto) =>
    pedir<Presupuesto>('/api/presupuestos', { method: 'POST', body: JSON.stringify(datos) }),
  modificar: (numero: number, datos: DatosPresupuesto) =>
    pedir<Presupuesto>(`/api/presupuestos/${numero}`, { method: 'PUT', body: JSON.stringify(datos) }),
}
