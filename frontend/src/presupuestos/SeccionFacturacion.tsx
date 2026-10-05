import { useState } from 'react'
import { ErrorApi } from '../api'
import { apiFacturacion, type ErrorArca } from '../facturacion/apiFacturacion'
import { formatearFecha, formatearImporte } from '../formato'
import type { Presupuesto } from './apiPresupuestos'

type Resultado =
  | { tipo: 'exito'; texto: string }
  | { tipo: 'rechazo'; texto: string; errores: ErrorArca[] }
  | { tipo: 'error'; texto: string; reintentable: boolean }

/**
 * Facturación del presupuesto (RF-25): solo en Final (AC-17), con confirmación porque
 * una factura autorizada no se puede anular desde el sistema. Muestra los errores de
 * ARCA (RF-51) y permite reintentar (RF-52).
 */
export function SeccionFacturacion({ presupuesto, alFacturar }: { presupuesto: Presupuesto; alFacturar: () => void }) {
  const [emitiendo, setEmitiendo] = useState(false)
  const [resultado, setResultado] = useState<Resultado>()

  const esFinal = presupuesto.estado === 'Final'
  const factura = presupuesto.factura

  async function facturar() {
    const confirmado = window.confirm(
      `Se va a emitir una factura electrónica ante ARCA por ${formatearImporte(presupuesto.total)}.\n\n` +
        'Una factura autorizada no se puede modificar ni anular desde el sistema. ¿Continuar?',
    )
    if (!confirmado) return

    setEmitiendo(true)
    setResultado(undefined)
    try {
      const emitida = await apiFacturacion.facturar(presupuesto.numero)
      setResultado({
        tipo: 'exito',
        texto: `Factura ${emitida.letra} ${emitida.comprobante} autorizada por ARCA. CAE ${emitida.cae}.`,
      })
    } catch (e) {
      if (e instanceof ErrorApi && Array.isArray(e.cuerpo.erroresArca)) {
        setResultado({ tipo: 'rechazo', texto: e.message, errores: e.cuerpo.erroresArca as ErrorArca[] })
      } else if (e instanceof ErrorApi) {
        setResultado({ tipo: 'error', texto: e.message, reintentable: e.estado === 503 })
      } else {
        setResultado({ tipo: 'error', texto: 'No se pudo conectar con el sistema. Revisá que esté iniciado.', reintentable: true })
      }
    } finally {
      setEmitiendo(false)
      alFacturar() // vuelve a leer el presupuesto: factura emitida o emisión pendiente
    }
  }

  return (
    <section className="tarjeta" aria-labelledby="titulo-facturacion">
      <h2 id="titulo-facturacion" className="tarjeta-titulo">
        Facturación
      </h2>

      {resultado?.tipo === 'exito' && (
        <div className="aviso aviso-exito" role="status">
          {resultado.texto}
        </div>
      )}
      {resultado?.tipo === 'rechazo' && (
        <div className="aviso aviso-error" role="alert">
          <p className="aviso-titulo">{resultado.texto}</p>
          <ul className="lista-errores">
            {resultado.errores.map((e) => (
              <li key={e.codigo}>
                Código {e.codigo}: {e.mensaje}
              </li>
            ))}
          </ul>
        </div>
      )}
      {resultado?.tipo === 'error' && (
        <div className="aviso aviso-error" role="alert">
          {resultado.texto}
        </div>
      )}

      {factura ? (
        <dl className="datos-factura">
          <div>
            <dt>Comprobante</dt>
            <dd>
              Factura {factura.letra} {factura.comprobante}
            </dd>
          </div>
          <div>
            <dt>Fecha</dt>
            <dd>{formatearFecha(factura.fecha)}</dd>
          </div>
          <div>
            <dt>CAE</dt>
            <dd className="numero-tabular">{factura.cae}</dd>
          </div>
          <div className="datos-factura-accion">
            {/* RF-50, AC-19 */}
            <a className="boton boton-primario" href={`/api/presupuestos/${presupuesto.numero}/factura/pdf`} download>
              Descargar PDF de la factura
            </a>
          </div>
        </dl>
      ) : (
        <>
          {presupuesto.facturacionPendiente && !emitiendo && (
            <div className="aviso aviso-info" role="status">
              La última emisión quedó sin respuesta de ARCA. Al reintentar, el sistema primero consulta si ARCA la
              autorizó, así no se emite una factura duplicada.
            </div>
          )}
          <div className="barra-facturar">
            <button
              type="button"
              className="boton boton-primario"
              onClick={facturar}
              disabled={!esFinal || emitiendo}
              aria-describedby="ayuda-facturar"
            >
              {emitiendo
                ? 'Esperando respuesta de ARCA…'
                : presupuesto.facturacionPendiente || resultado?.tipo !== undefined
                  ? 'Reintentar facturación'
                  : 'Facturar'}
            </button>
            <span id="ayuda-facturar" className="campo-ayuda">
              {!esFinal
                ? 'Se puede facturar cuando el presupuesto está en estado Final.'
                : emitiendo
                  ? 'Puede tardar hasta 30 segundos.'
                  : `Factura electrónica a consumidor final por ${formatearImporte(presupuesto.total)}.`}
            </span>
          </div>
        </>
      )}
    </section>
  )
}
