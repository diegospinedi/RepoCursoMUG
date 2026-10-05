import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { leerEnteroPositivo } from '../formato'

/** Punto de entrada a presupuestos. La búsqueda por fecha y cliente llega en el paso 12. */
export function PantallaPresupuestos() {
  const navegar = useNavigate()
  const [numero, setNumero] = useState('')
  const [error, setError] = useState<string>()

  function abrir(evento: FormEvent) {
    evento.preventDefault()
    const valor = leerEnteroPositivo(numero)
    if (valor === null) {
      setError('Ingresá el número del presupuesto')
      return
    }
    navegar(`/presupuestos/${valor}`)
  }

  return (
    <>
      <div className="encabezado-pantalla">
        <h1>Presupuestos</h1>
        <Link to="/presupuestos/nuevo" className="boton boton-primario">
          Nuevo presupuesto
        </Link>
      </div>
      <section className="tarjeta">
        <form className="barra-busqueda" onSubmit={abrir}>
          <div className="campo">
            <label htmlFor="abrir-numero">Abrir presupuesto por número</label>
            <input
              id="abrir-numero"
              inputMode="numeric"
              value={numero}
              onChange={(e) => setNumero(e.target.value)}
              aria-invalid={error ? 'true' : undefined}
              aria-describedby={error ? 'abrir-numero-error' : undefined}
            />
            {error && (
              <span id="abrir-numero-error" className="campo-error" role="alert">
                {error}
              </span>
            )}
          </div>
          <button type="submit" className="boton">
            Abrir
          </button>
        </form>
      </section>
    </>
  )
}
