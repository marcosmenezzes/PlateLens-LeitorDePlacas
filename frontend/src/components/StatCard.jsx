export default function StatCard({ label, value, detail, tone = 'default' }) {
  return <article className={`stat-card stat-card--${tone}`}><span className="stat-card__label">{label}</span><strong>{value}</strong><small>{detail}</small></article>
}
