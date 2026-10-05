import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { ErrorApi, type ErroresDeCampo } from '../api'
import { formatearDecimal, leerDecimal } from '../formato'
import { apiConfiguracion, type CondicionFiscal, type Configuracion } from './apiConfiguracion'

type Formulario = {
  alicuotaIva: string
  condicionFiscal: CondicionFiscal
  topeIdentificacion: string
  multiploRedondeo: string
}

const MULTIPLO_MINIMO = 0.01

function aFormulario(c: Configuracion): Formulario {
  return {
    alicuotaIva: formatearDecimal(c.alicuotaIva),
    condicionFiscal: c.condicionFiscal,
    topeIdentificacion: formatearDecimal(c.topeIdentificacion),
    multiploRedondeo: formatearDecimal(c.multiploRedondeo),
  }
}

/** Mismas reglas que la API, para avisar antes de enviar (RF-35, RF-72). */
function validar(f: Formulario): { errores: ErroresDeCampo; datos?: Configuracion } {
  const errores: ErroresDeCampo = {}
  const alicuotaIva = leerDecimal(f.alicuotaIva)
  const topeIdentificacion = leerDecimal(f.topeIdentificacion)
  const multiploRedondeo = leerDecimal(f.multiploRedondeo)

  if (alicuotaIva === null || alicuotaIva < 0 || alicuotaIva > 100)
    errores.alicuotaIva = 'Ingresá una alícuota de IVA entre 0 y 100, por ejemplo 21 o 10,5'
  if (topeIdentificacion === null || topeIdentificacion < 0)
    errores.topeIdentificacion = 'Ingresá un tope mayor o igual a 0'
  if (multiploRedondeo === null || multiploRedondeo < MULTIPLO_MINIMO)
    errores.multiploRedondeo = 'El valor mínimo es 0,01'

  if (Object.keys(errores).length > 0) return { errores }
  return {
    errores,
    datos: {
      alicuotaIva: alicuotaIva!,
      condicionFiscal: f.condicionFiscal,
      topeIdentificacion: topeIdentificacion!,
      multiploRedondeo: multiploRedondeo!,
    },
  }
}

export function PantallaConfiguracion() {
  const [formulario, setFormulario] = useState<Formulario>()
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [aviso, setAviso] = useState<{ tipo: 'exito' | 'error'; texto: string }>()
  const [grabando, setGrabando] = useState(false)

  useEffect(() => {
    apiConfiguracion
      .obtener()
      .then((c) => setFormulario(aFormulario(c)))
      .catch(() => setAviso({ tipo: 'error', texto: 'No se pudo leer la configuración. Recargá la página.' }))
  }, [])

  function cambiar<K extends keyof Formulario>(campo: K, valor: Formulario[K]) {
    setFormulario((f) => f && { ...f, [campo]: valor })
    setAviso(undefined)
  }

  async function grabar(evento: FormEvent) {
    evento.preventDefault()
    if (!formulario) return

    const { errores, datos } = validar(formulario)
    setErrores(errores)
    if (!datos) return

    setGrabando(true)
    try {
      setFormulario(aFormulario(await apiConfiguracion.grabar(datos)))
      setAviso({ tipo: 'exito', texto: 'Configuración guardada.' })
    } catch (e) {
      if (e instanceof ErrorApi && Object.keys(e.errores).length > 0) setErrores(e.errores)
      else setAviso({ tipo: 'error', texto: 'No se pudo guardar la configuración. Volvé a intentar.' })
    } finally {
      setGrabando(false)
    }
  }

  function propsCampo(campo: Exclude<keyof Formulario, 'condicionFiscal'>) {
    return {
      id: campo,
      type: 'text',
      inputMode: 'decimal' as const,
      value: formulario?.[campo] ?? '',
      onChange: (e: ChangeEvent<HTMLInputElement>) => cambiar(campo, e.target.value),
      'aria-invalid': errores[campo] ? ('true' as const) : undefined,
      'aria-describedby': `${campo}-ayuda${errores[campo] ? ` ${campo}-error` : ''}`,
    }
  }

  function error(campo: keyof Formulario) {
    return (
      errores[campo] && (
        <span id={`${campo}-error`} className="campo-error" role="alert">
          {errores[campo]}
        </span>
      )
    )
  }

  return (
    <>
      <h1>Configuración</h1>
      {aviso && (
        <div className={`aviso aviso-${aviso.tipo}`} role={aviso.tipo === 'error' ? 'alert' : 'status'}>
          {aviso.texto}
        </div>
      )}
      {formulario && (
        <form className="tarjeta" onSubmit={grabar} noValidate>
          <h2>Parámetros de facturación y precios</h2>
          <div className="formulario-grilla">
            <div className="campo">
              <label htmlFor="condicionFiscal">Condición fiscal</label>
              <select
                id="condicionFiscal"
                value={formulario.condicionFiscal}
                onChange={(e) => cambiar('condicionFiscal', e.target.value as CondicionFiscal)}
                aria-describedby="condicionFiscal-ayuda"
              >
                <option value="ResponsableInscripto">Responsable Inscripto</option>
                <option value="Monotributo">Monotributo</option>
              </select>
              <span id="condicionFiscal-ayuda" className="campo-ayuda">
                Responsable Inscripto emite Factura B; Monotributo emite Factura C.
              </span>
              {error('condicionFiscal')}
            </div>

            <div className="campo">
              <label htmlFor="alicuotaIva">Alícuota de IVA (%)</label>
              <input {...propsCampo('alicuotaIva')} />
              <span id="alicuotaIva-ayuda" className="campo-ayuda">
                Única para todo el catálogo. Por ejemplo, 21.
              </span>
              {error('alicuotaIva')}
            </div>

            <div className="campo">
              <label htmlFor="topeIdentificacion">Tope de identificación del receptor ($)</label>
              <input {...propsCampo('topeIdentificacion')} />
              <span id="topeIdentificacion-ayuda" className="campo-ayuda">
                Si el total de la factura lo supera, se identifica al cliente con su DNI.
              </span>
              {error('topeIdentificacion')}
            </div>

            <div className="campo">
              <label htmlFor="multiploRedondeo">Múltiplo de redondeo comercial ($)</label>
              <input {...propsCampo('multiploRedondeo')} />
              <span id="multiploRedondeo-ayuda" className="campo-ayuda">
                El precio de venta se redondea hacia arriba a este múltiplo. 0,01 redondea solo a centavos.
              </span>
              {error('multiploRedondeo')}
            </div>
          </div>
          <div className="formulario-acciones">
            <button type="submit" className="boton boton-primario" disabled={grabando}>
              {grabando ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </form>
      )}
    </>
  )
}
