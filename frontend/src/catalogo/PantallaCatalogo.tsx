import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { formatearDecimal, formatearImporte } from '../formato'
import { apiArticulos, type PaginaArticulos } from './apiArticulos'

export function PantallaCatalogo() {
  const [texto, setTexto] = useState('')
  const [busqueda, setBusqueda] = useState({ texto: '', pagina: 1 })
  const [resultado, setResultado] = useState<PaginaArticulos>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    let vigente = true
    apiArticulos
      .buscar(busqueda.texto, busqueda.pagina)
      .then((r) => vigente && (setResultado(r), setError(undefined)))
      .catch(() => vigente && setError('No se pudo leer el catálogo. Volvé a intentar.'))
    return () => {
      vigente = false
    }
  }, [busqueda])

  function buscar(evento: FormEvent) {
    evento.preventDefault()
    setBusqueda({ texto: texto.trim(), pagina: 1 })
  }

  const paginas = resultado ? Math.max(1, Math.ceil(resultado.total / resultado.tamanoPagina)) : 1

  return (
    <>
      <div className="encabezado-pantalla">
        <h1>Catálogo</h1>
        <Link to="/catalogo/nuevo" className="boton boton-primario">
          Nuevo artículo
        </Link>
      </div>

      <section className="tarjeta">
        <form className="barra-busqueda" onSubmit={buscar} role="search">
          <div className="campo">
            <label htmlFor="busqueda">Buscar por código, código en el proveedor o descripción</label>
            <input id="busqueda" type="search" value={texto} onChange={(e) => setTexto(e.target.value)} />
          </div>
          <button type="submit" className="boton">
            Buscar
          </button>
        </form>

        {error && (
          <div className="aviso aviso-error" role="alert">
            {error}
          </div>
        )}

        {resultado && resultado.articulos.length === 0 && (
          <p className="texto-vacio">
            {busqueda.texto ? `No hay artículos que coincidan con "${busqueda.texto}".` : 'Todavía no hay artículos cargados.'}
          </p>
        )}

        {resultado && resultado.articulos.length > 0 && (
          <>
            <div className="grilla-contenedor">
              <table className="grilla">
                <thead>
                  <tr>
                    <th className="numero">Código</th>
                    <th>Cód. proveedor</th>
                    <th>Descripción</th>
                    <th className="numero">Precio de costo</th>
                    <th className="numero">Margen</th>
                    <th className="numero">Precio de venta</th>
                  </tr>
                </thead>
                <tbody>
                  {resultado.articulos.map((a) => (
                    <tr key={a.codigo}>
                      <td className="numero">{a.codigo}</td>
                      <td>{a.codigoProveedor}</td>
                      <td>
                        <Link to={`/catalogo/${a.codigo}`}>{a.descripcion}</Link>
                      </td>
                      <td className="numero">{formatearImporte(a.precioCosto)}</td>
                      <td className="numero">{formatearDecimal(a.margenUtilidad)} %</td>
                      <td className="numero">
                        <strong>{formatearImporte(a.precioVenta)}</strong>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <nav className="paginacion" aria-label="Páginas del catálogo">
              <span>
                {resultado.total} artículo{resultado.total === 1 ? '' : 's'} · página {resultado.pagina} de {paginas}
              </span>
              <button
                type="button"
                className="boton"
                disabled={resultado.pagina <= 1}
                onClick={() => setBusqueda((b) => ({ ...b, pagina: b.pagina - 1 }))}
              >
                Anterior
              </button>
              <button
                type="button"
                className="boton"
                disabled={resultado.pagina >= paginas}
                onClick={() => setBusqueda((b) => ({ ...b, pagina: b.pagina + 1 }))}
              >
                Siguiente
              </button>
            </nav>
          </>
        )}
      </section>
    </>
  )
}
