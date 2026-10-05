import { pedir } from '../api'

export type EstadoAcceso = {
  contrasenaDefinida: boolean
  sesionIniciada: boolean
}

export const apiAcceso = {
  estado: () => pedir<EstadoAcceso>('/api/acceso/estado'),
  definirContrasenaInicial: (contrasena: string) =>
    pedir('/api/acceso/contrasena-inicial', { method: 'POST', body: JSON.stringify({ contrasena }) }),
  ingresar: (contrasena: string) =>
    pedir('/api/acceso/ingresar', { method: 'POST', body: JSON.stringify({ contrasena }) }),
  salir: () => pedir('/api/acceso/salir', { method: 'POST' }),
}
