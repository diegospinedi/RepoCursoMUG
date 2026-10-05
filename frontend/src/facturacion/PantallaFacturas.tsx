import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ErrorApi, type ErroresDeCampo } from '../api'
import { formatearDni, formatearFecha, formatearImporte } from '../formato'
import { apiFacturacion, type PaginaFacturas } from './apiFacturacion'

const CAMPOS_FILTRO = ['desde', 'hasta', 'comprobante', 'apellido', 'nombre', 'dni'] as const
type CampoFiltro = (typeof CAMPOS_FILTRO)[number]
type Filtros = Record<CampoFiltro, string>

const etiquetas: Record<CampoFiltro, string> = {
  desde: 'Fecha desde',
  hasta: 'Fecha hasta',
  comprobante: 'Nº de comprobante',
  apellido: 'Apellido',
  nombre: 'Nombre',
  dni: 'DNI',
}

function leerFiltros(parametros: URLSearchParams): Filtros {
  return Object.fromEntries(CAMPOS_FILTRO.map((c) => [c, parametros.get(c) ?? ''])) as Filtros
}

/**
 * Facturas emitidas (RF-33) con búsqueda por fecha, comprobante y cliente (RF-54).
 * Solo lectura: una factura con CAE no se modifica ni elimina (RF-32, AC-32).
 */
export function PantallaFacturas() {
  const [parametros, setParametros] = useSearchParams()
  const [filtros, setFiltros] = useState<Filtros>(() => leerFiltros(parametros))
  const [resultado, setResultado] = useState<PaginaFacturas>()
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [error, setError] = useState<string>()

  const consulta = parametros.toString()

  useEffect(() => {
    let vigente = true
    apiFacturacion
      .buscar(Object.fromEntries(new URLSearchParams(consulta)))
      .then((r) => {
        if (!vigente) return
        setResultado(r)
        setErrores({})
        setError(undefined)
      })
      .catch((e) => {
        if (!vigente) return
        if (e instanceof ErrorApi && Object.keys(e.errores).length > 0) setErrores(e.errores)
        else setError('No se pudo buscar facturas. Volvé a intentar.')
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

  const hayFiltros = CAMPOS_FILTRO.some((c) => parametros.get(c))
  const paginas = resultado ? Math.max(1, Math.ceil(resultado.total / resultado.tamanoPagina)) : 1

  return (
    <>
      <div className="encabezado-pantalla">
        <h1>Facturas</h1>
      </div>

      <section className="tarjeta">
        <form onSubmit={buscar} role="search" aria-label="Buscar facturas" noValidate>
          <div className="filtros-busqueda">
            {CAMPOS_FILTRO.map((campo) => {
              const id = `filtro-${campo}`
              return (
                <div className="campo" key={campo}>
                  <label htmlFor={id}>{etiquetas[campo]}</label>
                  <input
                    id={id}
                    type={campo === 'desde' || campo === 'hasta' ? 'date' : 'text'}
                    inputMode={campo === 'dni' || campo === 'comprobante' ? 'numeric' : undefined}
                    value={filtros[campo]}
                    onChange={(e) => setFiltros((f) => ({ ...f, [campo]: e.target.value }))}
                    aria-invalid={errores[campo] ? 'true' : undefined}
                    aria-describedby={errores[campo] ? `${id}-error` : undefined}
                  />
                  {errores[campo] && (
                    <span id={`${id}-error`} className="campo-error" role="alert">
                      {errores[campo]}
                    </span>
                  )}
                </div>
              )
            })}
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
          {resultado.facturas.length === 0 ? (
            <p className="texto-vacio">
              {hayFiltros ? 'No hay facturas que cumplan todos los filtros.' : 'Todavía no se emitieron facturas.'}
            </p>
          ) : (
            <>
              <div className="grilla-contenedor">
                <table className="grilla">
                  <thead>
                    <tr>
                      <th>Comprobante</th>
                      <th>Fecha</th>
                      <th>Cliente</th>
                      <th>DNI</th>
                      <th className="numero">Total</th>
                      <th>CAE</th>
                      <th className="numero">Presupuesto</th>
                      <th aria-label="PDF" />
                    </tr>
                  </thead>
                  <tbody>
                    {resultado.facturas.map((f) => (
                      <tr key={`${f.letra}-${f.comprobante}`}>
                        <td className="numero-tabular">
                          <Link to={`/presupuestos/${f.numeroPresupuesto}`}>
                            {f.letra} {f.comprobante}
                          </Link>
                        </td>
                        <td>{formatearFecha(f.fecha)}</td>
                        <td>
                          {f.apellido}, {f.nombre}
                        </td>
                        <td>{formatearDni(f.dni)}</td>
                        <td className="numero">{formatearImporte(f.importeTotal)}</td>
                        <td className="numero-tabular">{f.cae}</td>
                        <td className="numero">
                          <Link to={`/presupuestos/${f.numeroPresupuesto}`}>{f.numeroPresupuesto}</Link>
                        </td>
                        <td className="celda-accion">
                          <a
                            className="boton"
                            href={`/api/presupuestos/${f.numeroPresupuesto}/factura/pdf`}
                            download
                            aria-label={`Descargar PDF de la factura ${f.letra} ${f.comprobante}`}
                          >
                            PDF
                          </a>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <nav className="paginacion" aria-label="Páginas de facturas">
                <span>
                  {resultado.total} factura{resultado.total === 1 ? '' : 's'} · página {resultado.pagina} de {paginas}
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
