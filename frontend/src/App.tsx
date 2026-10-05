import { useCallback, useEffect, useState } from 'react'
import { registrarAlPerderSesion } from './api'
import { apiAcceso } from './acceso/apiAcceso'
import { ContrasenaInicial } from './acceso/ContrasenaInicial'
import { Ingreso } from './acceso/Ingreso'
import { useInactividad } from './acceso/useInactividad'
import { Layout } from './Layout'

type Pantalla = 'cargando' | 'sin-conexion' | 'definir-contrasena' | 'ingreso' | 'app'

function App() {
  const [pantalla, setPantalla] = useState<Pantalla>('cargando')

  const consultarEstado = useCallback(() => {
    apiAcceso
      .estado()
      .then((e) => setPantalla(!e.contrasenaDefinida ? 'definir-contrasena' : e.sesionIniciada ? 'app' : 'ingreso'))
      .catch(() => setPantalla('sin-conexion'))
  }, [])

  useEffect(() => {
    registrarAlPerderSesion(() => setPantalla('ingreso'))
    consultarEstado()
  }, [consultarEstado])

  switch (pantalla) {
    case 'cargando':
      return null
    case 'sin-conexion':
      return (
        <Layout>
          <div className="aviso aviso-error" role="alert">
            No se pudo conectar con el sistema. Revisá que esté iniciado y recargá la página.
          </div>
        </Layout>
      )
    case 'definir-contrasena':
      return <ContrasenaInicial alDefinir={() => setPantalla('app')} />
    case 'ingreso':
      return <Ingreso alIngresar={() => setPantalla('app')} />
    case 'app':
      return <AppConSesion alSalir={() => setPantalla('ingreso')} />
  }
}

function AppConSesion({ alSalir }: { alSalir: () => void }) {
  useInactividad(alSalir)

  async function salir() {
    await apiAcceso.salir().catch(() => {})
    alSalir()
  }

  return (
    <Layout
      acciones={
        <button type="button" className="boton" onClick={salir}>
          Salir
        </button>
      }
    >
      <h1>Inicio</h1>
      <section className="tarjeta">
        <p>Sesión iniciada. Las pantallas de catálogo y presupuestos llegan en los próximos pasos.</p>
      </section>
    </Layout>
  )
}

export default App
