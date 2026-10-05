import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi, type ErroresDeCampo } from '../api'
import { formatearDecimal, formatearImporte, leerDecimal } from '../formato'
import { apiArticulos, type Articulo, type DatosArticulo } from './apiArticulos'

type Formulario = Record<keyof DatosArticulo, string>
type Aviso = { tipo: 'exito' | 'error'; texto: string }

const vacio: Formulario = { codigoProveedor: '', descripcion: '', precioCosto: '', margenUtilidad: '' }

function aFormulario(a: Articulo): Formulario {
  return {
    codigoProveedor: a.codigoProveedor,
    descripcion: a.descripcion,
    precioCosto: formatearDecimal(a.precioCosto),
    margenUtilidad: formatearDecimal(a.margenUtilidad),
  }
}

/** Mismas reglas que la API, para avisar antes de enviar (RF-35). */
function validar(f: Formulario): { errores: ErroresDeCampo; datos?: DatosArticulo } {
  const errores: ErroresDeCampo = {}
  const precioCosto = leerDecimal(f.precioCosto)
  const margenUtilidad = leerDecimal(f.margenUtilidad)

  if (!f.codigoProveedor.trim()) errores.codigoProveedor = 'Ingresá el código del artículo en el proveedor'
  if (!f.descripcion.trim()) errores.descripcion = 'Ingresá la descripción del artículo'
  if (precioCosto === null || precioCosto <= 0) errores.precioCosto = 'Ingresá un precio de costo mayor a 0'
  if (margenUtilidad === null || margenUtilidad < 0 || margenUtilidad > 100)
    errores.margenUtilidad = 'Ingresá un margen de utilidad entre 0 y 100'

  if (Object.keys(errores).length > 0) return { errores }
  return {
    errores,
    datos: {
      codigoProveedor: f.codigoProveedor.trim(),
      descripcion: f.descripcion.trim(),
      precioCosto: precioCosto!,
      margenUtilidad: margenUtilidad!,
    },
  }
}

export function PantallaArticulo() {
  const { codigo } = useParams()
  // /catalogo/nuevo y /catalogo/:codigo usan este mismo componente: la key fuerza
  // a empezar de cero al pasar de uno a otro (por ejemplo, después de crear).
  return <FormularioArticulo key={codigo ?? 'nuevo'} codigo={codigo === undefined ? undefined : Number(codigo)} />
}

function FormularioArticulo({ codigo }: { codigo?: number }) {
  const navegar = useNavigate()
  const avisoAlLlegar = (useLocation().state as { aviso?: Aviso } | null)?.aviso

  const [articulo, setArticulo] = useState<Articulo>()
  const [formulario, setFormulario] = useState<Formulario | undefined>(codigo === undefined ? vacio : undefined)
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [aviso, setAviso] = useState<Aviso | undefined>(avisoAlLlegar)
  const [grabando, setGrabando] = useState(false)

  useEffect(() => {
    if (codigo === undefined) return
    apiArticulos
      .obtener(codigo)
      .then((a) => {
        setArticulo(a)
        setFormulario(aFormulario(a))
      })
      .catch((e) =>
        setAviso({
          tipo: 'error',
          texto: e instanceof ErrorApi && e.estado === 404 ? `No existe el artículo ${codigo}.` : 'No se pudo leer el artículo.',
        }),
      )
  }, [codigo])

  function cambiar(campo: keyof Formulario) {
    return (e: ChangeEvent<HTMLInputElement>) => {
      setFormulario((f) => f && { ...f, [campo]: e.target.value })
      setAviso(undefined)
    }
  }

  async function grabar(evento: FormEvent) {
    evento.preventDefault()
    if (!formulario) return

    const { errores, datos } = validar(formulario)
    setErrores(errores)
    if (!datos) return

    setGrabando(true)
    try {
      const grabado = codigo === undefined ? await apiArticulos.crear(datos) : await apiArticulos.modificar(codigo, datos)
      const texto = `Artículo ${grabado.codigo} guardado. Precio de venta: ${formatearImporte(grabado.precioVenta)}.`
      if (codigo === undefined) {
        navegar(`/catalogo/${grabado.codigo}`, { replace: true, state: { aviso: { tipo: 'exito', texto } } })
      } else {
        setArticulo(grabado)
        setFormulario(aFormulario(grabado))
        setAviso({ tipo: 'exito', texto })
      }
    } catch (e) {
      if (e instanceof ErrorApi && Object.keys(e.errores).length > 0) setErrores(e.errores)
      else setAviso({ tipo: 'error', texto: 'No se pudo guardar el artículo. Volvé a intentar.' })
    } finally {
      setGrabando(false)
    }
  }

  function campo(nombre: keyof Formulario, etiqueta: string, extra: { inputMode?: 'decimal'; ayuda?: string } = {}) {
    const error = errores[nombre]
    const describe = [extra.ayuda && `${nombre}-ayuda`, error && `${nombre}-error`].filter(Boolean).join(' ')
    return (
      <div className="campo">
        <label htmlFor={nombre}>{etiqueta}</label>
        <input
          id={nombre}
          type="text"
          inputMode={extra.inputMode}
          value={formulario?.[nombre] ?? ''}
          onChange={cambiar(nombre)}
          aria-invalid={error ? 'true' : undefined}
          aria-describedby={describe || undefined}
        />
        {extra.ayuda && (
          <span id={`${nombre}-ayuda`} className="campo-ayuda">
            {extra.ayuda}
          </span>
        )}
        {error && (
          <span id={`${nombre}-error`} className="campo-error" role="alert">
            {error}
          </span>
        )}
      </div>
    )
  }

  return (
    <>
      <div className="encabezado-pantalla">
        <h1>{codigo === undefined ? 'Nuevo artículo' : `Artículo ${codigo}`}</h1>
        <Link to="/catalogo" className="boton">
          Volver al catálogo
        </Link>
      </div>

      {aviso && (
        <div className={`aviso aviso-${aviso.tipo}`} role={aviso.tipo === 'error' ? 'alert' : 'status'}>
          {aviso.texto}
        </div>
      )}

      {formulario && (
        <form className="tarjeta" onSubmit={grabar} noValidate>
          <div className="formulario-grilla">
            {campo('codigoProveedor', 'Código en el proveedor')}
            {campo('descripcion', 'Descripción')}
            {campo('precioCosto', 'Precio de costo ($)', {
              inputMode: 'decimal',
              ayuda: 'Precio final del folleto del proveedor, con IVA incluido.',
            })}
            {campo('margenUtilidad', 'Margen de utilidad (%)', { inputMode: 'decimal', ayuda: 'Entre 0 y 100.' })}
            <div className="campo">
              <label htmlFor="precioVenta">Precio de venta</label>
              <input
                id="precioVenta"
                type="text"
                readOnly
                value={articulo ? formatearImporte(articulo.precioVenta) : ''}
                placeholder="Se calcula al guardar"
                aria-describedby="precioVenta-ayuda"
              />
              <span id="precioVenta-ayuda" className="campo-ayuda">
                Lo calcula el sistema con el costo, el margen y la configuración. Precio final con IVA incluido.
              </span>
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
