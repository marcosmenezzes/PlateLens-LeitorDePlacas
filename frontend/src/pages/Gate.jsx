import { useCallback, useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api'
import EventTable from '../components/EventTable'
import { useRealtimeRefresh } from '../useRealtimeRefresh'

export default function Gate() {
  const [query, setQuery] = useState('')
  const [events, setEvents] = useState([])
  const refresh = useCallback(() => apiRequest('/api/access-events').then(setEvents).catch(() => {}), [])
  useEffect(() => {
    refresh()
    const timer = window.setInterval(refresh, 30000)
    return () => window.clearInterval(timer)
  }, [refresh])
  useRealtimeRefresh(refresh)
  const filtered = useMemo(() => events.filter((event) => `${event.plateDetected} ${event.vehicle?.name}`.toLowerCase().includes(query.toLowerCase())), [events, query])
  return <div className="detections-page"><section className="section-intro"><div><span className="eyebrow">OPERAÇÃO EM TEMPO REAL</span><h2>Movimento da portaria.</h2></div><p>Cada placa aparece uma vez quando cruza a linha virtual, com direção e confiança da leitura.</p></section><div className="vehicle-toolbar"><label>Pesquisar placa ou nome<input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="ABC1D23 ou nome" /></label><span className="camera-state camera-state--online"><i /> Atualização automática</span></div><section className="panel section-panel"><div className="panel-heading"><div><span className="eyebrow">REGISTRO DE ACESSO</span><h2>Entradas e saídas</h2></div><span className="panel-count">{filtered.length} registros</span></div><EventTable items={filtered} /></section></div>
}
