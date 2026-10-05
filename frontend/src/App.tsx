import { useEffect, useState } from 'react'

function App() {
  const [estadoApi, setEstadoApi] = useState('consultando...')

  useEffect(() => {
    fetch('/api/salud')
      .then((r) => (r.ok ? 'conectada' : `error ${r.status}`))
      .catch(() => 'sin conexión')
      .then(setEstadoApi)
  }, [])

  return (
    <main>
      <h1>Óptica Sistema</h1>
      <p>API: {estadoApi}</p>
    </main>
  )
}

export default App
