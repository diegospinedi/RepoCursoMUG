import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { ErrorApi, type ErroresDeCampo } from '../api'
import { formatearDni, formatearFecha, formatearImporte, leerEnteroPositivo } from '../formato'
import { apiPresupuestos, type FiltrosPresupuestos, type PaginaPresupuestos } from './apiPresupuestos'

const CAMPOS_FILTRO = ['desde', 'hasta', 'apellido', 'nombre', 'dni'] as const
type CampoFiltro = (typeof CAMPOS_FILTRO)[number]
type Filtros = Record<CampoFiltro, string>

function leerFiltros(parametros: URLSearchParams): Filtros {
  return Object.fromEntries(CAMPOS_FILTRO.map((c) => [c, parametros.get(c) ?? ''])) as Filtros
}

/**
 * Búsqueda de presupuestos (RF-03). Los filtros viven en la URL: al volver de un
 * presupuesto, la búsqueda sigue ahí.
 */
export function PantallaPresupuestos() {
  const [parametros, setParametros] = useSearchParams()
  const [filtros, setFiltros] = useState<Filtros>(() => leerFiltros(parametros))
  const [resultado, setResultado] = useState<PaginaPresupuestos>()
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [error, setError] = useState<string>()

  const consulta = parametros.toString()

  useEffect(() => {
    let vigente = true
    const busqueda: FiltrosPresupuestos = Object.fromEntries(new URLSearchParams(consulta))
    apiPresupuestos
      .buscar(busqueda)
      .then((r) => {
        if (!vigente) return
        setResultado(r)
        setErrores({})
        setError(undefined)
      })
      .catch((e) => {
        if (!vigente) return
        if (e instanceof ErrorApi && Object.keys(e.errores).length > 0) setErrores(e.errores)
        else setError('No se pudo buscar presupuestos. Volvé a intentar.')
      })
    return () => {
      vigente = false
    }
  }, [consulta])

  function buscar(evento: FormEvent) {
    evento.preventDefault()
    if (filtros.desde && filtros.hasta && filtros.desde > filtros.hasta) {
      setErrores({ hasta: 'La fecha Hasta no puede ser anterior a la fecha Desde' })
      return
    }
    setParametros(Object.entries(filtros).filter(([, v]) => v.trim()).map(([k, v]): [string, string] => [k, v.trim()]))
  }

  function limpiar() {
    setFiltros(leerFiltros(new URLSearchParams()))
    setParametros([])
  }

  function irAPagina(pagina: number) {
    const nuevos = new URLSearchParams(parametros)
    nuevos.set('pagina', String(pagina))
    setParametros(nuevos)
  }

  function campo(nombre: CampoFiltro, etiqueta: string, tipo = 'text') {
    const id = `filtro-${nombre}`
    return (
      <div className="campo">
        <label htmlFor={id}>{etiqueta}</label>
        <input
          id={id}
          type={tipo}
          inputMode={nombre === 'dni' ? 'numeric' : undefined}
          value={filtros[nombre]}
          onChange={(e) => setFiltros((f) => ({ ...f, [nombre]: e.target.value }))}
          aria-invalid={errores[nombre] ? 'true' : undefined}
          aria-describedby={errores[nombre] ? `${id}-error` : undefined}
        />
        {errores[nombre] && (
          <span id={`${id}-error`} className="campo-error" role="alert">
            {errores[nombre]}
          </span>
        )}
      </div>
    )
  }

  const hayFiltros = CAMPOS_FILTRO.some((c) => parametros.get(c))
  const paginas = resultado ? Math.max(1, Math.ceil(resultado.total / resultado.tamanoPagina)) : 1

  return (
    <>
      <div className="encabezado-pantalla">
        <h1>Presupuestos</h1>
        <div className="acciones-encabezado">
          <AbrirPorNumero />
          <Link to="/presupuestos/nuevo" className="boton boton-primario">
            Nuevo presupuesto
          </Link>
        </div>
      </div>

      <section className="tarjeta">
        <form onSubmit={buscar} role="search" aria-label="Buscar presupuestos" noValidate>
          <div className="filtros-busqueda">
            {campo('desde', 'Fecha desde', 'date')}
            {campo('hasta', 'Fecha hasta', 'date')}
            {campo('apellido', 'Apellido')}
            {campo('nombre', 'Nombre')}
            {campo('dni', 'DNI')}
          </div>
          <div className="formulario-acciones">
            <button type="button" className="boton" onClick={limpiar}>
              Limpiar
            </button>
            <button type="submit" className="boton boton-primario">
              Buscar
            </button>
          </div>
        </form>
      </section>

      {error && (
        <div className="aviso aviso-error" role="alert">
          {error}
        </div>
      )}

      {resultado && (
        <section className="tarjeta" aria-label="Resultados">
          {resultado.presupuestos.length === 0 ? (
            <p className="texto-vacio">
              {hayFiltros ? 'No hay presupuestos que cumplan todos los filtros.' : 'Todavía no hay presupuestos grabados.'}
            </p>
          ) : (
            <>
              <div className="grilla-contenedor">
                <table className="grilla">
                  <thead>
                    <tr>
                      <th className="numero">Número</th>
                      <th>Fecha</th>
                      <th>Cliente</th>
                      <th>DNI</th>
                      <th>Estado</th>
                      <th className="numero">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {resultado.presupuestos.map((p) => (
                      <tr key={p.numero}>
                        <td className="numero">
                          <Link to={`/presupuestos/${p.numero}`}>{p.numero}</Link>
                        </td>
                        <td>{formatearFecha(p.fecha)}</td>
                        <td>
                          <Link to={`/presupuestos/${p.numero}`}>
                            {p.apellido}, {p.nombre}
                          </Link>
                        </td>
                        <td>{formatearDni(p.dni)}</td>
                        <td>
                          <span className={`etiqueta etiqueta-${p.estado === 'Final' ? 'final' : 'borrador'}`}>{p.estado}</span>
                        </td>
                        <td className="numero">{formatearImporte(p.total)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <nav className="paginacion" aria-label="Páginas de presupuestos">
                <span>
                  {resultado.total} presupuesto{resultado.total === 1 ? '' : 's'} · página {resultado.pagina} de {paginas}
                </span>
                <button type="button" className="boton" disabled={resultado.pagina <= 1} onClick={() => irAPagina(resultado.pagina - 1)}>
                  Anterior
                </button>
                <button
                  type="button"
                  className="boton"
                  disabled={resultado.pagina >= paginas}
                  onClick={() => irAPagina(resultado.pagina + 1)}
                >
                  Siguiente
                </button>
              </nav>
            </>
          )}
        </section>
      )}
    </>
  )
}

function AbrirPorNumero() {
  const navegar = useNavigate()
  const [numero, setNumero] = useState('')
  const [error, setError] = useState(false)

  function abrir(evento: FormEvent) {
    evento.preventDefault()
    const valor = leerEnteroPositivo(numero)
    if (valor === null) {
      setError(true)
      return
    }
    navegar(`/presupuestos/${valor}`)
  }

  return (
    <form className="abrir-numero" onSubmit={abrir} noValidate>
      <label htmlFor="abrir-numero" className="visualmente-oculto">
        Número de presupuesto
      </label>
      <input
        id="abrir-numero"
        inputMode="numeric"
        placeholder="Nº"
        value={numero}
        onChange={(e) => {
          setNumero(e.target.value)
          setError(false)
        }}
        aria-invalid={error ? 'true' : undefined}
        title={error ? 'Ingresá el número del presupuesto' : undefined}
      />
      <button type="submit" className="boton">
        Abrir
      </button>
    </form>
  )
}
