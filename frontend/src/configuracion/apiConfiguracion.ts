import { pedir } from '../api'

export type CondicionFiscal = 'ResponsableInscripto' | 'Monotributo'

export type Configuracion = {
  alicuotaIva: number
  condicionFiscal: CondicionFiscal
  topeIdentificacion: number
  multiploRedondeo: number
}

export const apiConfiguracion = {
  obtener: () => pedir<Configuracion>('/api/configuracion'),
  grabar: (configuracion: Configuracion) =>
    pedir<Configuracion>('/api/configuracion', { method: 'PUT', body: JSON.stringify(configuracion) }),
}
