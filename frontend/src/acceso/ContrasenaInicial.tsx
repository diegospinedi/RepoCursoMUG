import { useState, type FormEvent } from 'react'
import { ErrorApi, type ErroresDeCampo } from '../api'
import { apiAcceso } from './apiAcceso'
import { PantallaAcceso } from './PantallaAcceso'

const LONGITUD_MINIMA = 8

export function ContrasenaInicial({ alDefinir }: { alDefinir: () => void }) {
  const [contrasena, setContrasena] = useState('')
  const [confirmacion, setConfirmacion] = useState('')
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [errorGeneral, setErrorGeneral] = useState<string>()
  const [enviando, setEnviando] = useState(false)

  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    setErrorGeneral(undefined)

    const nuevos: ErroresDeCampo = {}
    if (contrasena.length < LONGITUD_MINIMA) {
      nuevos.contrasena = `La contraseña debe tener al menos ${LONGITUD_MINIMA} caracteres`
    } else if (confirmacion !== contrasena) {
      nuevos.confirmacion = 'Las contraseñas no coinciden. Volvé a escribirla'
    }
    setErrores(nuevos)
    if (Object.keys(nuevos).length > 0) return

    setEnviando(true)
    try {
      await apiAcceso.definirContrasenaInicial(contrasena)
      alDefinir()
    } catch (e) {
      if (e instanceof ErrorApi && Object.keys(e.errores).length > 0) setErrores(e.errores)
      else setErrorGeneral(e instanceof ErrorApi ? e.message : 'No se pudo conectar con el sistema. Revisá que esté iniciado.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <PantallaAcceso
      titulo="Definir contraseña"
      ayuda={`Es el primer uso del sistema. Definí la contraseña de acceso (mínimo ${LONGITUD_MINIMA} caracteres).`}
    >
      {errorGeneral && (
        <div className="aviso aviso-error" role="alert">
          {errorGeneral}
        </div>
      )}
      <form onSubmit={enviar} noValidate>
        <div className="campo">
          <label htmlFor="contrasena">Contraseña nueva</label>
          <input
            id="contrasena"
            type="password"
            autoComplete="new-password"
            autoFocus
            value={contrasena}
            onChange={(e) => setContrasena(e.target.value)}
            aria-invalid={errores.contrasena ? 'true' : undefined}
            aria-describedby={errores.contrasena ? 'contrasena-error' : undefined}
          />
          {errores.contrasena && (
            <span id="contrasena-error" className="campo-error" role="alert">
              {errores.contrasena}
            </span>
          )}
        </div>
        <div className="campo">
          <label htmlFor="confirmacion">Repetí la contraseña</label>
          <input
            id="confirmacion"
            type="password"
            autoComplete="new-password"
            value={confirmacion}
            onChange={(e) => setConfirmacion(e.target.value)}
            aria-invalid={errores.confirmacion ? 'true' : undefined}
            aria-describedby={errores.confirmacion ? 'confirmacion-error' : undefined}
          />
          {errores.confirmacion && (
            <span id="confirmacion-error" className="campo-error" role="alert">
              {errores.confirmacion}
            </span>
          )}
        </div>
        <button type="submit" className="boton boton-primario" disabled={enviando}>
          {enviando ? 'Guardando…' : 'Guardar y entrar'}
        </button>
      </form>
    </PantallaAcceso>
  )
}
