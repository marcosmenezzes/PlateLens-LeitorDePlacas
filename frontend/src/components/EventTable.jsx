export function formatDate(value) {
  return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}

export default function EventTable({ items, compact = false }) {
  return <div className="table-scroll"><table className={compact ? 'data-table data-table--compact' : 'data-table'}><caption className="sr-only">Eventos de entrada e saída de veículos</caption><thead><tr><th>Horário</th><th>Placa</th><th>Veículo</th><th>Tipo</th><th>Movimento</th><th>Leitura</th></tr></thead><tbody>{items.map((item) => { const eventType = item.eventType.toUpperCase(); return <tr key={item.id}><td>{formatDate(item.occurredAt)}</td><td><strong className="plate-code">{item.plateDetected}</strong></td><td>{item.vehicle?.name || 'Desconhecido'}</td><td>{item.vehicle?.vehicleType || 'Desconhecido'}</td><td><span className={`event-pill event-pill--${eventType.toLowerCase()}`}>{eventType === 'ENTRY' ? 'Entrada' : 'Saída'}</span></td><td><span className={item.vehicle?.name === 'Desconhecido' ? 'unknown-pill' : 'known-pill'}>{item.vehicle?.name === 'Desconhecido' ? 'Desconhecido' : `${item.confidence}%`}</span></td></tr> })}</tbody></table></div>
}
