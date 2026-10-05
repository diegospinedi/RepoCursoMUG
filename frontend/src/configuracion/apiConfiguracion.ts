import { pedir } from '../api'

export type CondicionFiscal = 'ResponsableInscripto' | 'Monotributo'

export type Configuracion = {
  alicuotaIva: number
  condicionFiscal: CondicionFiscal
  topeIdentificacion: number
  multiploRedondeo: number
}

/** Al grabar, la API informa cuántos precios de venta del catálogo cambiaron (RF-86). */
export type ConfiguracionGrabada = Configuracion & { preciosActualizados: number }

export const apiConfiguracion = {
  obtener: () => pedir<Configuracion>('/api/configuracion'),
  grabar: (configuracion: Configuracion) =>
    pedir<ConfiguracionGrabada>('/api/configuracion', { method: 'PUT', body: JSON.stringify(configuracion) }),
}
