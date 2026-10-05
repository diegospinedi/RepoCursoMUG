import { useCallback, useEffect, useState } from 'react'
import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router'
import { registrarAlPerderSesion } from './api'
import { apiAcceso } from './acceso/apiAcceso'
import { ContrasenaInicial } from './acceso/ContrasenaInicial'
import { Ingreso } from './acceso/Ingreso'
import { useInactividad } from './acceso/useInactividad'
import { PantallaArticulo } from './catalogo/PantallaArticulo'
import { PantallaCatalogo } from './catalogo/PantallaCatalogo'
import { PantallaConfiguracion } from './configuracion/PantallaConfiguracion'
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
    <BrowserRouter>
      <Layout
        acciones={
          <>
            <nav className="layout-navegacion" aria-label="Principal">
              <NavLink to="/" end>
                Inicio
              </NavLink>
              <NavLink to="/catalogo">Catálogo</NavLink>
              <NavLink to="/configuracion">Configuración</NavLink>
            </nav>
            <button type="button" className="boton" onClick={salir}>
              Salir
            </button>
          </>
        }
      >
        <Routes>
          <Route path="/" element={<Inicio />} />
          <Route path="/catalogo" element={<PantallaCatalogo />} />
          <Route path="/catalogo/nuevo" element={<PantallaArticulo />} />
          <Route path="/catalogo/:codigo" element={<PantallaArticulo />} />
          <Route path="/configuracion" element={<PantallaConfiguracion />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Layout>
    </BrowserRouter>
  )
}

function Inicio() {
  return (
    <>
      <h1>Inicio</h1>
      <section className="tarjeta">
        <p>Sesión iniciada. La pantalla de presupuestos llega en los próximos pasos.</p>
      </section>
    </>
  )
}

export default App
