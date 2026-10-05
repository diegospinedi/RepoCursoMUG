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

export const apiFacturacion = {
  /** Emite la factura del presupuesto; también es el reintento (RF-52). */
  facturar: (numeroPresupuesto: number) =>
    pedir<Factura>(`/api/presupuestos/${numeroPresupuesto}/factura`, { method: 'POST' }),
}
