import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi, type ErroresDeCampo } from '../api'
import type { Articulo } from '../catalogo/apiArticulos'
import { formatearDecimal, formatearDni, formatearFecha, formatearImporte, leerDecimal, leerEnteroPositivo } from '../formato'
import {
  apiPresupuestos,
  type Cliente,
  type DatosPresupuesto,
  type EstadoPresupuesto,
  type Presupuesto,
} from './apiPresupuestos'
import { BuscadorArticulos } from './BuscadorArticulos'
import { centavosAImporte, precioConDescuentoEnCentavos, precioFinalEnCentavos } from './calculo'

type CampoCliente = keyof Cliente
type FormularioCliente = Record<CampoCliente, string>

type FormularioLinea = {
  codigoArticulo: number
  descripcion: string
  precioUnitario: number
  cantidad: string
  porcentajeDescuento: string
}

type Formulario = { cliente: FormularioCliente; lineas: FormularioLinea[]; estado: EstadoPresupuesto }
type Aviso = { tipo: 'exito' | 'error'; texto: string }

const MENSAJE_CANTIDAD = 'La cantidad debe ser un número entero mayor a 0'
const MENSAJE_DESCUENTO = 'El descuento debe estar entre 0 y 100'

const camposCliente: { campo: CampoCliente; etiqueta: string; obligatorio?: boolean; tipo?: string; autoComplete?: string }[] = [
  { campo: 'apellido', etiqueta: 'Apellido', obligatorio: true },
  { campo: 'nombre', etiqueta: 'Nombre', obligatorio: true },
  { campo: 'dni', etiqueta: 'DNI', obligatorio: true },
  { campo: 'domicilio', etiqueta: 'Domicilio' },
  { campo: 'email', etiqueta: 'Email', tipo: 'email' },
  { campo: 'telefono', etiqueta: 'Nro. de teléfono', tipo: 'tel' },
]

const vacio: Formulario = {
  cliente: { apellido: '', nombre: '', dni: '', domicilio: '', email: '', telefono: '' },
  lineas: [],
  estado: 'Borrador',
}

function aFormulario(p: Presupuesto): Formulario {
  return {
    cliente: {
      apellido: p.cliente.apellido,
      nombre: p.cliente.nombre,
      dni: formatearDni(p.cliente.dni),
      domicilio: p.cliente.domicilio ?? '',
      email: p.cliente.email ?? '',
      telefono: p.cliente.telefono ?? '',
    },
    lineas: p.lineas.map((l) => ({
      codigoArticulo: l.codigoArticulo,
      descripcion: l.descripcion,
      precioUnitario: l.precioUnitario,
      cantidad: String(l.cantidad),
      porcentajeDescuento: formatearDecimal(l.porcentajeDescuento),
    })),
    estado: p.estado,
  }
}

/** Importes de la línea si cantidad y descuento son válidos; si no, null (se muestra "—"). */
function calcularLinea(l: FormularioLinea) {
  const cantidad = leerEnteroPositivo(l.cantidad)
  const descuento = leerDecimal(l.porcentajeDescuento)
  if (cantidad === null || descuento === null || descuento < 0 || descuento > 100) return null
  const conDescuento = precioConDescuentoEnCentavos(l.precioUnitario, descuento)
  return { conDescuento, final: precioFinalEnCentavos(conDescuento, cantidad) }
}

/** Mismas reglas que la API, con los mismos mensajes y claves (RF-35). */
function validar(f: Formulario): ErroresDeCampo {
  const errores: ErroresDeCampo = {}
  if (!f.cliente.apellido.trim()) errores['cliente.apellido'] = 'Ingresá el apellido del cliente'
  if (!f.cliente.nombre.trim()) errores['cliente.nombre'] = 'Ingresá el nombre del cliente'
  if (!f.cliente.dni.trim()) errores['cliente.dni'] = 'Ingresá el DNI del cliente'
  else if (!/^\d{6,9}$/.test(f.cliente.dni.replace(/[.\s-]/g, '')))
    errores['cliente.dni'] = 'El DNI debe tener entre 6 y 9 números; podés escribirlo con o sin puntos'

  if (f.lineas.length === 0) errores.lineas = 'Agregá al menos un artículo al presupuesto'
  f.lineas.forEach((l, i) => {
    if (leerEnteroPositivo(l.cantidad) === null) errores[`lineas[${i}].cantidad`] = MENSAJE_CANTIDAD
    const descuento = leerDecimal(l.porcentajeDescuento)
    if (descuento === null || descuento < 0 || descuento > 100) errores[`lineas[${i}].porcentajeDescuento`] = MENSAJE_DESCUENTO
    if (l.precioUnitario < 0) errores[`lineas[${i}].precioUnitario`] = 'El precio unitario no puede ser negativo'
  })
  return errores
}

function aDatos(f: Formulario): DatosPresupuesto {
  const opcional = (texto: string) => (texto.trim() === '' ? null : texto.trim())
  return {
    cliente: {
      apellido: f.cliente.apellido.trim(),
      nombre: f.cliente.nombre.trim(),
      dni: f.cliente.dni.trim(),
      domicilio: opcional(f.cliente.domicilio),
      email: opcional(f.cliente.email),
      telefono: opcional(f.cliente.telefono),
    },
    lineas: f.lineas.map((l) => ({
      codigoArticulo: l.codigoArticulo,
      cantidad: leerEnteroPositivo(l.cantidad),
      porcentajeDescuento: leerDecimal(l.porcentajeDescuento),
    })),
    estado: f.estado,
  }
}

export function PantallaPresupuesto() {
  const { numero } = useParams()
  // /presupuestos/nuevo y /presupuestos/:numero comparten componente: la key reinicia el estado.
  return <FormularioPresupuesto key={numero ?? 'nuevo'} numero={numero === undefined ? undefined : Number(numero)} />
}

function FormularioPresupuesto({ numero }: { numero?: number }) {
  const navegar = useNavigate()
  const avisoAlLlegar = (useLocation().state as { aviso?: Aviso } | null)?.aviso

  const [presupuesto, setPresupuesto] = useState<Presupuesto>()
  const [formulario, setFormulario] = useState<Formulario | undefined>(numero === undefined ? vacio : undefined)
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [aviso, setAviso] = useState<Aviso | undefined>(avisoAlLlegar)
  const [grabando, setGrabando] = useState(false)

  useEffect(() => {
    if (numero === undefined) return
    apiPresupuestos
      .obtener(numero)
      .then((p) => {
        setPresupuesto(p)
        setFormulario(aFormulario(p))
      })
      .catch((e) =>
        setAviso({
          tipo: 'error',
          texto: e instanceof ErrorApi && e.estado === 404 ? `No existe el presupuesto ${numero}.` : 'No se pudo leer el presupuesto.',
        }),
      )
  }, [numero])

  const esFinal = presupuesto?.estado === 'Final'

  function actualizar(cambio: (f: Formulario) => Formulario) {
    setFormulario((f) => f && cambio(f))
    setAviso(undefined)
  }

  function cambiarCliente(campo: CampoCliente) {
    return (e: ChangeEvent<HTMLInputElement>) =>
      actualizar((f) => ({ ...f, cliente: { ...f.cliente, [campo]: e.target.value } }))
  }

  function cambiarLinea(indice: number, campo: 'cantidad' | 'porcentajeDescuento') {
    return (e: ChangeEvent<HTMLInputElement>) =>
      actualizar((f) => ({ ...f, lineas: f.lineas.map((l, i) => (i === indice ? { ...l, [campo]: e.target.value } : l)) }))
  }

  function agregarArticulo(a: Articulo) {
    // RF-39: código, descripción y precio de venta del catálogo, tal cual (RF-73).
    actualizar((f) => ({
      ...f,
      lineas: [
        ...f.lineas,
        { codigoArticulo: a.codigo, descripcion: a.descripcion, precioUnitario: a.precioVenta, cantidad: '1', porcentajeDescuento: '0' },
      ],
    }))
    setErrores((e) => {
      const { lineas: _, ...resto } = e
      return resto
    })
  }

  function quitarLinea(indice: number) {
    actualizar((f) => ({ ...f, lineas: f.lineas.filter((_, i) => i !== indice) }))
    setErrores({}) // los índices de los errores de línea ya no corresponden
  }

  async function grabar(evento: FormEvent) {
    evento.preventDefault()
    if (!formulario) return

    const nuevos = validar(formulario)
    setErrores(nuevos)
    if (Object.keys(nuevos).length > 0) {
      setAviso({ tipo: 'error', texto: 'Revisá los campos marcados en rojo.' })
      return
    }
    if (
      formulario.estado === 'Final' &&
      !window.confirm('Un presupuesto en estado Final ya no se puede modificar ni volver a Borrador. ¿Grabarlo como Final?')
    )
      return

    setGrabando(true)
    try {
      const datos = aDatos(formulario)
      const grabado =
        numero === undefined ? await apiPresupuestos.crear(datos) : await apiPresupuestos.modificar(numero, datos)
      const texto = `Presupuesto ${grabado.numero} grabado en estado ${grabado.estado}.`
      if (numero === undefined) {
        navegar(`/presupuestos/${grabado.numero}`, { replace: true, state: { aviso: { tipo: 'exito', texto } } })
      } else {
        setPresupuesto(grabado)
        setFormulario(aFormulario(grabado))
        setAviso({ tipo: 'exito', texto })
      }
    } catch (e) {
      if (e instanceof ErrorApi && Object.keys(e.errores).length > 0) {
        setErrores(e.errores)
        setAviso({ tipo: 'error', texto: 'Revisá los campos marcados en rojo.' })
      } else {
        setAviso({ tipo: 'error', texto: e instanceof ErrorApi ? e.message : 'No se pudo grabar el presupuesto. Volvé a intentar.' })
      }
    } finally {
      setGrabando(false)
    }
  }

  function error(clave: string) {
    const id = `error-${clave.replace(/[^\w-]/g, '-')}`
    return errores[clave] ? (
      <span id={id} className="campo-error" role="alert">
        {errores[clave]}
      </span>
    ) : null
  }

  function propsError(clave: string) {
    return errores[clave]
      ? { 'aria-invalid': 'true' as const, 'aria-describedby': `error-${clave.replace(/[^\w-]/g, '-')}` }
      : {}
  }

  const calculos = formulario?.lineas.map(calcularLinea) ?? []
  const totalCentavos = calculos.every((c) => c !== null) ? calculos.reduce((t, c) => t + c!.final, 0) : null

  return (
    <>
      <div className="encabezado-pantalla">
        <div className="titulo-con-etiqueta">
          <h1>{numero === undefined ? 'Nuevo presupuesto' : `Presupuesto ${numero}`}</h1>
          {presupuesto && (
            <span className={`etiqueta etiqueta-${presupuesto.estado === 'Final' ? 'final' : 'borrador'}`}>
              {presupuesto.estado}
            </span>
          )}
          {presupuesto && <span className="texto-secundario">{formatearFecha(presupuesto.fecha)}</span>}
        </div>
        <Link to="/presupuestos" className="boton">
          Volver a presupuestos
        </Link>
      </div>

      {aviso && (
        <div className={`aviso aviso-${aviso.tipo}`} role={aviso.tipo === 'error' ? 'alert' : 'status'}>
          {aviso.texto}
        </div>
      )}
      {esFinal && (
        <div className="aviso aviso-info">Este presupuesto está en estado Final: no se puede modificar.</div>
      )}

      {formulario && (
        <>
          <form id="form-presupuesto" onSubmit={grabar} noValidate>
            <fieldset className="tarjeta" disabled={esFinal}>
              <legend className="tarjeta-titulo">Cliente</legend>
              <div className="formulario-grilla formulario-grilla-3">
                {camposCliente.map(({ campo, etiqueta, obligatorio, tipo, autoComplete }) => (
                  <div className="campo" key={campo}>
                    <label htmlFor={`cliente-${campo}`}>
                      {etiqueta}
                      {obligatorio && <span aria-hidden="true"> *</span>}
                    </label>
                    <input
                      id={`cliente-${campo}`}
                      type={tipo ?? 'text'}
                      autoComplete={autoComplete ?? 'off'}
                      value={formulario.cliente[campo] ?? ''}
                      onChange={cambiarCliente(campo)}
                      required={obligatorio}
                      {...propsError(`cliente.${campo}`)}
                    />
                    {error(`cliente.${campo}`)}
                  </div>
                ))}
              </div>
            </fieldset>
          </form>

          <section className="tarjeta" aria-labelledby="titulo-lineas">
            <h2 id="titulo-lineas" className="tarjeta-titulo">
              Artículos
            </h2>
            {!esFinal && <BuscadorArticulos alElegir={agregarArticulo} />}
            {error('lineas')}

            {formulario.lineas.length > 0 && (
              <div className="grilla-contenedor">
                <table className="grilla grilla-lineas">
                  <thead>
                    <tr>
                      <th className="numero">Código</th>
                      <th>Descripción</th>
                      <th className="numero">Precio unitario</th>
                      <th className="numero">Cantidad</th>
                      <th className="numero">% Desc.</th>
                      <th className="numero">Precio c/desc.</th>
                      <th className="numero">Precio final</th>
                      {!esFinal && <th aria-label="Acciones" />}
                    </tr>
                  </thead>
                  <tbody>
                    {formulario.lineas.map((l, i) => {
                      const calculo = calculos[i]
                      return (
                        <tr key={i}>
                          <td className="numero">{l.codigoArticulo}</td>
                          <td>{l.descripcion}</td>
                          <td className="numero">
                            {formatearImporte(l.precioUnitario)}
                            {error(`lineas[${i}].precioUnitario`)}
                            {error(`lineas[${i}].codigoArticulo`)}
                          </td>
                          <td className="numero celda-campo">
                            <input
                              aria-label={`Cantidad de la línea ${i + 1}`}
                              inputMode="numeric"
                              value={l.cantidad}
                              onChange={cambiarLinea(i, 'cantidad')}
                              disabled={esFinal}
                              {...propsError(`lineas[${i}].cantidad`)}
                            />
                            {error(`lineas[${i}].cantidad`)}
                          </td>
                          <td className="numero celda-campo">
                            <input
                              aria-label={`Descuento de la línea ${i + 1}`}
                              inputMode="decimal"
                              value={l.porcentajeDescuento}
                              onChange={cambiarLinea(i, 'porcentajeDescuento')}
                              disabled={esFinal}
                              {...propsError(`lineas[${i}].porcentajeDescuento`)}
                            />
                            {error(`lineas[${i}].porcentajeDescuento`)}
                          </td>
                          <td className="numero" data-testid={`con-descuento-${i}`}>
                            {calculo ? formatearImporte(centavosAImporte(calculo.conDescuento)) : '—'}
                          </td>
                          <td className="numero" data-testid={`precio-final-${i}`}>
                            {calculo ? formatearImporte(centavosAImporte(calculo.final)) : '—'}
                          </td>
                          {!esFinal && (
                            <td className="celda-accion">
                              <button
                                type="button"
                                className="boton boton-peligro"
                                onClick={() => quitarLinea(i)}
                                aria-label={`Quitar ${l.descripcion}`}
                              >
                                Quitar
                              </button>
                            </td>
                          )}
                        </tr>
                      )
                    })}
                  </tbody>
                  <tfoot>
                    <tr className="fila-total">
                      <th colSpan={6} scope="row">
                        Total
                      </th>
                      <td className="numero" data-testid="total">
                        {totalCentavos === null ? '—' : formatearImporte(centavosAImporte(totalCentavos))}
                      </td>
                      {!esFinal && <td />}
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
            <p className="campo-ayuda">Precios finales, IVA incluido.</p>
          </section>

          {!esFinal && (
            <div className="tarjeta barra-acciones">
              <div className="campo campo-en-linea">
                <label htmlFor="estado">Estado</label>
                <select
                  id="estado"
                  form="form-presupuesto"
                  value={formulario.estado}
                  onChange={(e) => actualizar((f) => ({ ...f, estado: e.target.value as EstadoPresupuesto }))}
                >
                  <option value="Borrador">Borrador</option>
                  <option value="Final">Final</option>
                </select>
              </div>
              <button type="submit" form="form-presupuesto" className="boton boton-primario" disabled={grabando}>
                {grabando ? 'Grabando…' : 'Grabar'}
              </button>
            </div>
          )}
        </>
      )}
    </>
  )
}
