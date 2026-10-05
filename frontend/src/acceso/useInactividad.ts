import { useEffect, useRef } from 'react'
import { apiAcceso } from './apiAcceso'

const LIMITE_INACTIVIDAD_MS = 60 * 60 * 1000 // RNF-09
const AVISO_ACTIVIDAD_MS = 5 * 60 * 1000
const EVENTOS = ['mousedown', 'keydown', 'wheel', 'touchstart'] as const

/**
 * Cierra la sesión tras 60 minutos sin interacción de la operadora. Mientras
 * hay actividad, avisa al servidor cada 5 minutos para que su sesión, que solo
 * ve pedidos HTTP, no venza mientras la operadora completa un formulario.
 */
export function useInactividad(alVencer: () => void) {
  const ultimaActividad = useRef(0)
  const ultimoAviso = useRef(0)

  useEffect(() => {
    ultimaActividad.current = Date.now()
    ultimoAviso.current = Date.now()

    function registrarActividad() {
      const ahora = Date.now()
      ultimaActividad.current = ahora
      if (ahora - ultimoAviso.current >= AVISO_ACTIVIDAD_MS) {
        ultimoAviso.current = ahora
        apiAcceso.estado().catch(() => {})
      }
    }

    const temporizador = setInterval(() => {
      if (Date.now() - ultimaActividad.current >= LIMITE_INACTIVIDAD_MS) {
        apiAcceso.salir().catch(() => {})
        alVencer()
      }
    }, 30_000)

    EVENTOS.forEach((e) => window.addEventListener(e, registrarActividad, { passive: true }))
    return () => {
      clearInterval(temporizador)
      EVENTOS.forEach((e) => window.removeEventListener(e, registrarActividad))
    }
  }, [alVencer])
}
