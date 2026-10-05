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

export type Presupuesto = {
  numero: number
  fecha: string
  estado: EstadoPresupuesto
  cliente: Cliente
  lineas: Linea[]
  total: number
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

export const apiPresupuestos = {
  obtener: (numero: number) => pedir<Presupuesto>(`/api/presupuestos/${numero}`),
  crear: (datos: DatosPresupuesto) =>
    pedir<Presupuesto>('/api/presupuestos', { method: 'POST', body: JSON.stringify(datos) }),
  modificar: (numero: number, datos: DatosPresupuesto) =>
    pedir<Presupuesto>(`/api/presupuestos/${numero}`, { method: 'PUT', body: JSON.stringify(datos) }),
}
