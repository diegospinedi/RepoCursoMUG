import { pedir } from '../api'
import type { Linea } from '../presupuestos/apiPresupuestos'

export type Factura = {
  letra: string
  comprobante: string
  puntoVenta: number
  numero: number
  fecha: string
  receptor: string
  importeTotal: number
  importeNeto: number
  importeIva: number
  alicuotaIva: number | null
  cae: string
  vencimientoCae: string
  numeroPresupuesto: number
  lineas: Linea[]
}

export type ErrorArca = { codigo: number; mensaje: string }

export type ResumenFactura = {
  letra: string
  comprobante: string
  fecha: string
  apellido: string
  nombre: string
  dni: string
  importeTotal: number
  cae: string
  numeroPresupuesto: number
}

export type PaginaFacturas = { facturas: ResumenFactura[]; total: number; pagina: number; tamanoPagina: number }

export const apiFacturacion = {
  /** RF-54: filtros vacíos no se envían. */
  buscar: (filtros: Record<string, string>) => {
    const parametros = new URLSearchParams(Object.entries(filtros).filter(([, v]) => v))
    return pedir<PaginaFacturas>(`/api/facturas?${parametros}`)
  },
  /** Emite la factura del presupuesto; también es el reintento (RF-52). */
  facturar: (numeroPresupuesto: number) =>
    pedir<Factura>(`/api/presupuestos/${numeroPresupuesto}/factura`, { method: 'POST' }),
}
