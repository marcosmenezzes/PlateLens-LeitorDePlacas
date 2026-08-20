import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from '../api'
import { BarChart, TrafficChart } from '../components/Charts'
import EventTable from '../components/EventTable'
import StatCard from '../components/StatCard'
import { useRealtimeRefresh } from '../useRealtimeRefresh'

const empty = {
  summary: { entries: 0, exits: 0, inside: 0, unknown: 0, total: 0 }, daily: [], hourly: [], byType: [],
  frequentVehicles: [], averageStayByType: [], recentEvents: [], cameras: [], peakHour: '--',
  recognition: { platesDetected: 0, ocrValid: 0, ocrInvalid: 0, recognitionRate: 0, averageDetectionConfidence: 0, averageOcrConfidence: 0, averageProcessingMs: 0, rejected: 0 },
}

function localDate(date = new Date()) {
  const offset = date.getTimezoneOffset() * 60000
  return new Date(date - offset).toISOString().slice(0, 10)
}

export default function Analytics() {
  const [period, setPeriod] = useState('7')
  const [from, setFrom] = useState(localDate(new Date(Date.now() - 6 * 86400000)))
  const [to, setTo] = useState(localDate())
  const [data, setData] = useState(empty)
  const [message, setMessage] = useState('')
  const query = period === 'custom' ? `from=${from}&to=${to}` : `days=${period}`
  const refresh = useCallback(() => apiRequest(`/api/analytics?${query}`).then((items) => { setData(items); setMessage('') }).catch((error) => setMessage(error.message)), [query])
  useEffect(() => { refresh() }, [refresh])
  useRealtimeRefresh(refresh)

  return <div className="analytics-page">
    <section className="section-intro"><div><span className="eyebrow">INTELIGÊNCIA OPERACIONAL</span><h2>Do movimento ao padrão.</h2></div><p>Todos os indicadores usam os mesmos eventos persistidos pela Portaria.</p></section>
    <div className="vehicle-toolbar date-filter"><label>Período<select value={period} onChange={(event) => setPeriod(event.target.value)}><option value="1">Hoje</option><option value="7">7 dias</option><option value="30">30 dias</option><option value="custom">Personalizado</option></select></label>{period === 'custom' && <><label>De<input type="date" value={from} max={to} onChange={(event) => setFrom(event.target.value)} /></label><label>Até<input type="date" value={to} min={from} onChange={(event) => setTo(event.target.value)} /></label></>}{message && <span role="status">{message}</span>}</div>
    <section className="analytics-canvas">
      <section className="analytics-hero"><div className="analytics-intro"><span className="eyebrow">TRÁFEGO</span><h2>A portaria em números reais.</h2><p>{data.summary.total} movimentos no período selecionado.</p><a className="button" href="/portaria">Ver movimentos　→</a></div><aside className="key-insight"><h3>Horário de pico</h3><strong>{data.peakHour}</strong><p>Faixa com <b>maior fluxo</b></p><div className="signal-bars">{Array.from({ length: 30 }, (_, index) => <i className={data.summary.total && index < 22 ? 'active' : ''} key={index} />)}</div><small>{data.summary.total} movimentos registrados</small></aside></section>
      <div className="dot-divider" />
      <section className="analytics-metrics"><StatCard label="Entradas" value={data.summary.entries} detail="no período" tone="quartz" /><StatCard label="Saídas" value={data.summary.exits} detail="no período" tone="violet" /><StatCard label="Dentro agora" value={data.summary.inside} detail="último estado conhecido" tone="jade" /><StatCard label="Desconhecidos" value={data.summary.unknown} detail="vistos no período" tone="citrine" /></section>
      <div className="dot-divider" />
      <section className="analytics-overview"><div className="analytics-trend"><header><h3>Entradas × saídas por dia</h3><span>{data.period ? `${data.period.from} — ${data.period.to}` : ''}</span></header>{data.daily.length > 0 && <TrafficChart title="Movimentação diária" data={data.daily} />}</div><aside className="risk-distribution"><h3>Veículos por tipo</h3><BarChart title="Tipos de veículo" data={data.byType} /></aside></section>
      <div className="dot-divider" />
      <section className="analytics-history"><header><h3>Movimentação por horário</h3><span>00h — 23h</span></header>{data.hourly.length > 0 && <TrafficChart title="Movimentação por horário" data={data.hourly} />}</section>
      <div className="dot-divider" />
      <section className="summary-strip"><article><span>Placas detectadas</span><strong>{data.recognition.platesDetected}</strong><small>{data.recognition.ocrValid} OCR válidos · {data.recognition.ocrInvalid} inválidos</small></article><article><span>Taxa de reconhecimento</span><strong>{data.recognition.recognitionRate}%</strong><small>YOLO {data.recognition.averageDetectionConfidence}% · OCR {data.recognition.averageOcrConfidence}%</small></article><article><span>Tempo médio</span><strong>{data.recognition.averageProcessingMs} ms</strong><small>{data.recognition.rejected} leituras rejeitadas</small></article></section>
      <div className="dot-divider" />
      <section className="analytics-overview"><div className="analytics-table"><div className="panel-heading"><div><span className="eyebrow">FROTA</span><h2>Veículos mais frequentes</h2></div></div><div className="table-scroll"><table className="data-table"><thead><tr><th>Placa</th><th>Nome</th><th>Acessos</th></tr></thead><tbody>{data.frequentVehicles.map((item) => <tr key={item.plate}><td className="plate-code">{item.plate}</td><td>{item.name}</td><td>{item.count}</td></tr>)}</tbody></table></div></div><aside className="risk-distribution"><h3>Tempo médio de permanência</h3><ul>{data.averageStayByType.length ? data.averageStayByType.map((item) => <li key={item.name}><i /><span>{item.name}</span><strong>{item.hours}h</strong></li>) : <li><span>Sem pares de entrada e saída</span></li>}</ul><h3>Câmeras</h3><ul>{data.cameras.map((camera) => <li key={camera.id}><i /><span>{camera.name}</span><strong>{camera.online ? 'Online' : 'Offline'}</strong></li>)}</ul></aside></section>
      <div className="dot-divider" />
      <section className="analytics-table"><div className="panel-heading"><div><span className="eyebrow">EVENTOS</span><h2>Movimentos recentes</h2></div></div><EventTable items={data.recentEvents} /></section>
    </section>
  </div>
}
