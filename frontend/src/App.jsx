import AppShell from './components/AppShell'
import Analytics from './pages/Analytics'
import Dashboard from './pages/Dashboard'
import Gate from './pages/Gate'
import Monitoring from './pages/Monitoring'
import Vehicles from './pages/Vehicles'

const routes = {
  '/': { title: 'Visão geral', component: Dashboard },
  '/portaria': { title: 'Portaria', component: Gate },
  '/vehicles': { title: 'Veículos', component: Vehicles },
  '/monitoring': { title: 'Monitoramento', component: Monitoring },
  '/analytics': { title: 'Estatísticas', component: Analytics },
}

/** Resolve a URL atual e monta a página dentro do layout compartilhado. */
export default function App() {
  const route = routes[window.location.pathname] || routes['/']
  const Page = route.component
  return <AppShell title={route.title}><Page /></AppShell>
}
