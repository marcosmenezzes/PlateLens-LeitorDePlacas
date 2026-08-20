import { useEffect, useState } from 'react'
import Icon from './Icon'

const links = [
  ['/', 'dashboard', 'Visão geral'],
  ['/portaria', 'gate', 'Portaria'],
  ['/vehicles', 'car', 'Veículos'],
  ['/monitoring', 'camera', 'Monitoramento'],
  ['/analytics', 'chart', 'Estatísticas'],
]

export default function AppShell({ title, children }) {
  const [open, setOpen] = useState(false)
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem('platelens-sidebar-collapsed') === 'true')
  const [theme, setTheme] = useState(() => localStorage.getItem('platelens-theme') || 'dark')

  useEffect(() => {
    const close = (event) => event.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [])
  useEffect(() => { document.documentElement.dataset.theme = theme; localStorage.setItem('platelens-theme', theme) }, [theme])
  useEffect(() => { localStorage.setItem('platelens-sidebar-collapsed', String(collapsed)) }, [collapsed])

  const toggleTheme = () => setTheme(theme === 'light' ? 'dark' : 'light')
  return (
    <div className={collapsed ? 'app-shell app-shell--collapsed' : 'app-shell'}>
      <aside className={open ? 'sidebar sidebar--open' : 'sidebar'}>
        <div className="sidebar-brand-row">
          <a className="brand" href="/" aria-label="PlateLens — início"><span>PlateLens</span></a>
          <button className="collapse-button" type="button" aria-label={collapsed ? 'Expandir menu lateral' : 'Recolher menu lateral'} onClick={() => setCollapsed(!collapsed)}><Icon name="chevron" /></button>
        </div>
        <nav id="main-navigation" className="nav" aria-label="Navegação principal">
          <span className="nav-label">MENU</span>
          {links.map(([href, icon, label]) => <a key={href} href={href} aria-label={label} title={collapsed ? label : undefined} aria-current={window.location.pathname === href ? 'page' : undefined}><span><Icon name={icon} /></span><b>{label}</b></a>)}
        </nav>
        <div className="sidebar-footer">
          <div className="system-status"><span /><b>Portaria ativa</b></div>
          <button className="theme-button" type="button" aria-label={theme === 'light' ? 'Ativar tema escuro' : 'Ativar tema claro'} onClick={toggleTheme}><Icon name="sun" /><b>{theme === 'light' ? 'Tema escuro' : 'Tema claro'}</b></button>
        </div>
      </aside>
      {open && <button className="sidebar-backdrop" type="button" aria-label="Fechar menu" onClick={() => setOpen(false)} />}
      <div className="workspace">
        <header className="topbar">
          <button className="menu-button" type="button" aria-label="Alternar menu" aria-controls="main-navigation" aria-expanded={open} onClick={() => setOpen(!open)}><span /><span /><span /></button>
          <div className="page-heading"><span>Controle de acesso</span><b>/</b><h1>{title}</h1></div>
          <div className="topbar-actions"><span className="live-dot" /> Monitoramento ativo</div>
          <button className="top-theme-button" type="button" aria-label={theme === 'light' ? 'Ativar tema escuro' : 'Ativar tema claro'} onClick={toggleTheme}><Icon name="sun" /></button>
        </header>
        <main>{children}</main>
        <footer><span>PlateLens · portaria inteligente com visão computacional</span><span>Dados processados localmente</span></footer>
      </div>
    </div>
  )
}
