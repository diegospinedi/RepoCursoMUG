import { useState, type FormEvent } from 'react'
import { ErrorApi } from '../api'
import { apiAcceso } from './apiAcceso'
import { PantallaAcceso } from './PantallaAcceso'

export function Ingreso({ alIngresar }: { alIngresar: () => void }) {
  const [contrasena, setContrasena] = useState('')
  const [error, setError] = useState<string>()
  const [enviando, setEnviando] = useState(false)

  async function enviar(evento: FormEvent) {
    evento.preventDefault()
    if (!contrasena) {
      setError('Ingresá la contraseña')
      return
    }
    setEnviando(true)
    try {
      await apiAcceso.ingresar(contrasena)
      alIngresar()
    } catch (e) {
      setError(e instanceof ErrorApi ? (e.errores.contrasena ?? e.message) : 'No se pudo conectar con el sistema. Revisá que esté iniciado.')
      setContrasena('')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <PantallaAcceso titulo="Ingresar" ayuda="Ingresá la contraseña del sistema para continuar.">
      <form onSubmit={enviar} noValidate>
        <div className="campo">
          <label htmlFor="contrasena">Contraseña</label>
          <input
            id="contrasena"
            type="password"
            autoComplete="current-password"
            autoFocus
            value={contrasena}
            onChange={(e) => setContrasena(e.target.value)}
            aria-invalid={error ? 'true' : undefined}
            aria-describedby={error ? 'contrasena-error' : undefined}
          />
          {error && (
            <span id="contrasena-error" className="campo-error" role="alert">
              {error}
            </span>
          )}
        </div>
        <button type="submit" className="boton boton-primario" disabled={enviando}>
          {enviando ? 'Ingresando…' : 'Ingresar'}
        </button>
      </form>
    </PantallaAcceso>
  )
}
