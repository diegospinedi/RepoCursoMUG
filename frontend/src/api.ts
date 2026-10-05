/** Errores de validación por campo, en el formato ValidationProblem de la API (RF-35). */
export type ErroresDeCampo = Record<string, string>

export class ErrorApi extends Error {
  readonly estado: number
  readonly errores: ErroresDeCampo

  constructor(estado: number, mensaje: string, errores: ErroresDeCampo = {}) {
    super(mensaje)
    this.estado = estado
    this.errores = errores
  }
}

let alPerderSesion: () => void = () => {}

/** La app registra acá qué hacer cuando la API responde 401 (sesión vencida, AC-59). */
export function registrarAlPerderSesion(accion: () => void) {
  alPerderSesion = accion
}

export async function pedir<T = void>(ruta: string, opciones: RequestInit = {}): Promise<T> {
  const respuesta = await fetch(ruta, {
    ...opciones,
    headers: { 'Content-Type': 'application/json', ...opciones.headers },
  })

  if (respuesta.ok) {
    return respuesta.status === 204 ? (undefined as T) : ((await respuesta.json()) as T)
  }

  const cuerpo = await respuesta.json().catch(() => ({}))
  const errores: ErroresDeCampo = {}
  for (const [campo, mensajes] of Object.entries<string[]>(cuerpo.errors ?? {})) {
    errores[campo] = mensajes[0]
  }

  if (respuesta.status === 401 && Object.keys(errores).length === 0) {
    alPerderSesion()
  }

  throw new ErrorApi(respuesta.status, cuerpo.detail ?? cuerpo.title ?? 'Error inesperado', errores)
}
