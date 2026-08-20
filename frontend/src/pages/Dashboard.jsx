import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from '../api'
import { LineChart } from '../components/Charts'
import EventTable, { formatDate } from '../components/EventTable'
import Icon from '../components/Icon'
import StatCard from '../components/StatCard'
import { useRealtimeRefresh } from '../useRealtimeRefresh'

const empty = { summary: { entries: 0, exits: 0, inside: 0, unknown: 0, total: 0 }, daily: [], recentEvents: [] }

export default function Dashboard() {
  const [today, setToday] = useState(empty)
  const [week, setWeek] = useState(empty)
  const [message, setMessage] = useState('')
  const refresh = useCallback(() => Promise.all([
    apiRequest('/api/analytics?days=1'), apiRequest('/api/analytics?days=7'),
  ]).then(([todayData, weekData]) => { setToday(todayData); setWeek(weekData); setMessage('') }).catch((error) => setMessage(error.message)), [])
  useEffect(() => { refresh() }, [refresh])
  useRealtimeRefresh(refresh)
  const chart = week.daily.map((item) => ({ date: item.label, count: item.count }))
  const latest = today.recentEvents[0] || week.recentEvents[0]

  return <section className="dashboard-canvas">
    <header className="dashboard-header"><div><span className="eyebrow">VISÃO COMPUTACIONAL · CONTROLE EM TEMPO REAL</span><h2>PlateLens</h2><p>Acompanhe o movimento da portaria e identifique veículos desconhecidos.</p></div><div className="dashboard-actions"><span className="period-button">Hoje</span><a className="button" href="/monitoring"><Icon name="camera" /> Monitorar</a></div></header>
    <div className="dot-divider" />
    <section className="update-strip"><div><span><i /> Atualização em tempo real</span><small>{latest ? formatDate(latest.occurredAt) : 'Sem registros'}</small><p>{today.summary.total} movimentos registrados hoje</p>{message && <p role="status">{message}</p>}</div><a href="/portaria">Abrir portaria　→</a></section>
    <div className="dot-divider" />
    <div className="overview-head"><h3>Movimento dos últimos 7 dias</h3><h3>Resumo operacional</h3></div>
    <section className="overview-grid"><div className="overview-chart"><div className="chart-summary"><span>Total de acessos</span><strong>{week.summary.total}</strong><small>dados reais</small><span>últimos 7 dias</span></div>{chart.length > 0 && <LineChart title="Entradas e saídas" data={chart} />}</div><div className="overview-stats"><StatCard label="Entradas hoje" value={today.summary.entries} detail="movimentos confirmados" tone="quartz" /><StatCard label="Saídas hoje" value={today.summary.exits} detail="movimentos confirmados" tone="violet" /><StatCard label="Atualmente dentro" value={today.summary.inside} detail="último estado por veículo" tone="jade" /><StatCard label="Desconhecidos hoje" value={today.summary.unknown} detail="aguardando cadastro" tone="citrine" /></div></section>
    <div className="dot-divider" />
    <section className="dashboard-table"><div className="panel-heading"><div><span className="eyebrow">ÚLTIMOS MOVIMENTOS</span><h2>Eventos recentes</h2></div><a className="text-link" href="/portaria">Ver portaria →</a></div><EventTable items={(today.recentEvents.length ? today.recentEvents : week.recentEvents).slice(0, 5)} compact /></section>
  </section>
}
