import { useState, type FormEvent } from 'react'
import { apiArticulos, type Articulo } from '../catalogo/apiArticulos'
import { formatearImporte } from '../formato'

const MAXIMO_RESULTADOS = 10

/** Busca artículos del catálogo por código o descripción para agregarlos como línea (RF-11, RF-68). */
export function BuscadorArticulos({ alElegir }: { alElegir: (articulo: Articulo) => void }) {
  const [texto, setTexto] = useState('')
  const [resultados, setResultados] = useState<Articulo[]>()
  const [total, setTotal] = useState(0)
  const [error, setError] = useState<string>()

  async function buscar(evento: FormEvent) {
    evento.preventDefault()
    if (!texto.trim()) {
      setError('Escribí un código o parte de la descripción del artículo')
      return
    }
    try {
      const pagina = await apiArticulos.buscar(texto.trim(), 1)
      setResultados(pagina.articulos.slice(0, MAXIMO_RESULTADOS))
      setTotal(pagina.total)
      setError(undefined)
    } catch {
      setError('No se pudo buscar en el catálogo. Volvé a intentar.')
    }
  }

  function elegir(articulo: Articulo) {
    alElegir(articulo)
    setResultados(undefined)
    setTexto('')
  }

  return (
    <div className="buscador-articulos">
      {/* Un form anidado no es HTML válido: el buscador vive fuera del form del presupuesto. */}
      <form className="barra-busqueda" onSubmit={buscar} role="search">
        <div className="campo">
          <label htmlFor="buscar-articulo">Agregar artículo: buscá por código o descripción</label>
          <input
            id="buscar-articulo"
            type="search"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            aria-invalid={error ? 'true' : undefined}
            aria-describedby={error ? 'buscar-articulo-error' : undefined}
          />
          {error && (
            <span id="buscar-articulo-error" className="campo-error" role="alert">
              {error}
            </span>
          )}
        </div>
        <button type="submit" className="boton">
          Buscar
        </button>
      </form>

      {resultados && resultados.length === 0 && <p className="texto-vacio">No hay artículos que coincidan.</p>}
      {resultados && resultados.length > 0 && (
        <div className="grilla-contenedor">
          <table className="grilla" aria-label="Artículos encontrados">
            <thead>
              <tr>
                <th className="numero">Código</th>
                <th>Descripción</th>
                <th className="numero">Precio de venta</th>
                <th aria-label="Acciones" />
              </tr>
            </thead>
            <tbody>
              {resultados.map((a) => (
                <tr key={a.codigo}>
                  <td className="numero">{a.codigo}</td>
                  <td>{a.descripcion}</td>
                  <td className="numero">{formatearImporte(a.precioVenta)}</td>
                  <td className="celda-accion">
                    <button type="button" className="boton" onClick={() => elegir(a)}>
                      Agregar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {total > resultados.length && (
            <p className="campo-ayuda">
              Se muestran {resultados.length} de {total}. Escribí más texto para acotar la búsqueda.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
