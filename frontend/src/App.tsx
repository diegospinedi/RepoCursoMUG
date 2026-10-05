import { useEffect, useState } from 'react'
import { Layout } from './Layout'

const claseAviso = {
  consultando: 'aviso-info',
  conectada: 'aviso-exito',
  'sin conexión': 'aviso-error',
} as const

function App() {
  const [estadoApi, setEstadoApi] = useState<'consultando' | 'conectada' | 'sin conexión'>(
    'consultando',
  )

  useEffect(() => {
    fetch('/api/salud')
      .then((r) => (r.ok ? 'conectada' : 'sin conexión'))
      .catch(() => 'sin conexión' as const)
      .then(setEstadoApi)
  }, [])

  return (
    <Layout>
      <h1>Inicio</h1>
      <section className="tarjeta">
        <h2>Estado del sistema</h2>
        <div className={`aviso ${claseAviso[estadoApi]}`}>
          API: {estadoApi}
        </div>
      </section>
    </Layout>
  )
}

export default App
