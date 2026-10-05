import { useEffect, useState } from 'react'
import { Layout } from './Layout'

function App() {
  const [estadoApi, setEstadoApi] = useState('consultando...')

  useEffect(() => {
    fetch('/api/salud')
      .then((r) => (r.ok ? 'conectada' : `error ${r.status}`))
      .catch(() => 'sin conexión')
      .then(setEstadoApi)
  }, [])

  return (
    <Layout>
      <h1>Inicio</h1>
      <p>API: {estadoApi}</p>
    </Layout>
  )
}

export default App
