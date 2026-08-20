function smoothPath(points, height) {
  if (points.length < 2) return ''
  return points.slice(0, -1).reduce((path, point, index) => {
    const previous = points[index - 1] || point
    const next = points[index + 1]
    const after = points[index + 2] || next
    const clamp = (value) => Math.max(0, Math.min(height, value))
    const first = [point[0] + (next[0] - previous[0]) / 6, clamp(point[1] + (next[1] - previous[1]) / 6)]
    const second = [next[0] - (after[0] - point[0]) / 6, clamp(next[1] - (after[1] - point[1]) / 6)]
    return `${path} C ${first.join(',')} ${second.join(',')} ${next.join(',')}`
  }, `M ${points[0].join(',')}`)
}

export function LineChart({ title, data, labelKey = 'date' }) {
  const width = 720
  const height = 250
  const max = Math.max(...data.map((item) => item.count), 1)
  const points = data.map((item, index) => [index * (width / Math.max(1, data.length - 1)), height - (item.count / max) * (height - 24) - 12])
  const line = smoothPath(points, height)
  return <figure className="chart" aria-label={title}><figcaption>{title}<span>{data.reduce((sum, item) => sum + item.count, 0)} acessos</span></figcaption><svg className="line-chart" viewBox={`0 0 ${width} ${height}`} role="img"><path className="chart-area" d={`${line} L ${width},${height} L 0,${height} Z`} fill="color-mix(in srgb, var(--chart-primary) 20%, transparent)" /><path className="chart-line" d={line} fill="none" stroke="var(--chart-primary)" strokeWidth="4" pathLength="1" vectorEffect="non-scaling-stroke" />{points.map(([x, y], index) => <circle className="chart-point" key={data[index][labelKey]} cx={x} cy={y} r="7" fill="var(--chart-secondary)" stroke="var(--surface)" strokeWidth="3" style={{ '--index': index }}><title>{data[index][labelKey]}: {data[index].count}</title></circle>)}</svg><div className="chart-axis"><span>{data[0][labelKey]}</span><span>{data.at(-1)[labelKey]}</span></div></figure>
}

export function TrafficChart({ title, data }) {
  const width = 720
  const height = 250
  const max = Math.max(...data.flatMap((item) => [item.entries, item.exits]), 1)
  const points = (key) => data.map((item, index) => `${index * (width / Math.max(1, data.length - 1))},${height - item[key] / max * (height - 24) - 12}`).join(' ')
  return <figure className="chart" aria-label={title}><figcaption>{title}<span>Entrada × saída</span></figcaption><svg className="line-chart" viewBox={`0 0 ${width} ${height}`} role="img"><polyline points={points('entries')} fill="none" stroke="var(--chart-primary)" strokeWidth="4" vectorEffect="non-scaling-stroke" /><polyline points={points('exits')} fill="none" stroke="var(--chart-secondary)" strokeWidth="4" vectorEffect="non-scaling-stroke" /></svg><div className="chart-axis"><span>{data[0]?.label || '--'}</span><span>{data.at(-1)?.label || '--'}</span></div></figure>
}

export function BarChart({ title, data }) {
  const max = Math.max(...data.map((item) => item.count), 1)
  return <figure className="chart" aria-label={title}><figcaption>{title}<span>{data.reduce((sum, item) => sum + item.count, 0)} veículos</span></figcaption><div className="bar-chart">{data.map((item, index) => <div className="bar-row" key={item.name}><span>{item.name}</span><div><i style={{ width: `${item.count / max * 100}%`, '--index': index }} /></div><strong>{item.count}</strong></div>)}</div></figure>
}
