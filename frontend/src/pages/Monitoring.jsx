import { useEffect, useRef, useState } from 'react'
import Icon from '../components/Icon'
import { apiRequest } from '../api'

const nativeCamera = { id: '00000000-0000-0000-0000-000000000001', name: 'Câmera nativa', kind: 'native', deviceIndex: 0 }
const defaultRegion = { x: .2, y: .25, width: .6, height: .5 }
const captureDelayMs = 3000

/** Lê preferências locais sem impedir a abertura da tela se o valor estiver corrompido. */
function load(key, fallback) {
  try { return JSON.parse(localStorage.getItem(key)) || fallback } catch { return fallback }
}

/** Aceita somente endereços IPv4 que pertencem a uma rede privada. */
function privateIpv4(value) {
  const parts = value.split('.').map(Number)
  return parts.length === 4 && parts.every((part) => Number.isInteger(part) && part >= 0 && part <= 255) &&
    (parts[0] === 10 || parts[0] === 172 && parts[1] >= 16 && parts[1] <= 31 || parts[0] === 192 && parts[1] === 168)
}

const clamp = (value, min, max) => Math.max(min, Math.min(max, value))
/** Limita o retângulo de captura às dimensões normalizadas da imagem. */
function normalizeRegion(value) {
  const number = (key) => Number.isFinite(Number(value?.[key])) ? Number(value[key]) : defaultRegion[key]
  const x = clamp(number('x'), 0, .9)
  const y = clamp(number('y'), 0, .9)
  return { x, y, width: clamp(number('width'), .1, 1 - x), height: clamp(number('height'), .1, 1 - y) }
}
const normalizeCamera = (camera) => ({ ...camera, kind: camera.sourceKind.toLowerCase() })

/** Controla câmera, região de interesse e envio contínuo de quadros ao backend. */
export default function Monitoring() {
  const [cameras, setCameras] = useState(() => load('platelens-cameras', [nativeCamera]))
  const [activeId, setActiveId] = useState(() => localStorage.getItem('platelens-active-camera') || nativeCamera.id)
  const [region, setRegion] = useState(() => normalizeRegion(load('platelens-gate-region', defaultRegion)))
  const [form, setForm] = useState({ name: '', ipAddress: '', port: 554 })
  const [message, setMessage] = useState('')
  const [stream, setStream] = useState(null)
  const [networkReady, setNetworkReady] = useState(false)
  const [interaction, setInteraction] = useState(null)
  const [backendState, setBackendState] = useState('checking')
  const [lastDetection, setLastDetection] = useState(null)
  const [lastCrop, setLastCrop] = useState('')
  const videoRef = useRef(null)
  const networkRef = useRef(null)
  const viewportRef = useRef(null)
  const requestsRef = useRef(0)
  const nextCaptureAtRef = useRef(0)
  const startingRef = useRef(false)
  const active = cameras.find((camera) => camera.id === activeId) || nativeCamera

  useEffect(() => { localStorage.setItem('platelens-cameras', JSON.stringify(cameras)) }, [cameras])
  useEffect(() => { localStorage.setItem('platelens-active-camera', activeId) }, [activeId])
  useEffect(() => { if (videoRef.current) videoRef.current.srcObject = stream }, [stream])
  useEffect(() => () => stream?.getTracks().forEach((track) => track.stop()), [stream])
  useEffect(() => {
    apiRequest('/api/cameras')
      .then((items) => {
        const normalized = items.map(normalizeCamera)
        setCameras(normalized)
        setActiveId(normalized.find((camera) => camera.isActive)?.id || nativeCamera.id)
        setBackendState('online')
      })
      .catch(() => setBackendState('offline'))
  }, [])

  useEffect(() => {
    if (!stream && !networkReady) return
    analyzeFrame()
    const timer = window.setInterval(analyzeFrame, 250)
    return () => window.clearInterval(timer)
  }, [stream, networkReady, region, activeId])

  useEffect(() => {
    if (backendState === 'checking') return
    if (active.kind === 'native') startNative()
    else setStream(null)
  }, [activeId, active.kind, backendState])

  useEffect(() => {
    const timer = window.setTimeout(saveRegion, 500)
    return () => window.clearTimeout(timer)
  }, [region, backendState, activeId])

  useEffect(() => {
    if (!interaction) return
    function move(event) {
      const bounds = viewportRef.current.getBoundingClientRect()
      const dx = (event.clientX - interaction.startX) / bounds.width
      const dy = (event.clientY - interaction.startY) / bounds.height
      if (interaction.mode === 'move') {
        setRegion(normalizeRegion({ ...interaction.region, x: interaction.region.x + dx, y: interaction.region.y + dy }))
      } else {
        setRegion(normalizeRegion({ ...interaction.region, width: interaction.region.width + dx, height: interaction.region.height + dy }))
      }
    }
    const stop = () => setInteraction(null)
    window.addEventListener('pointermove', move)
    window.addEventListener('pointerup', stop, { once: true })
    return () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', stop) }
  }, [interaction])

  /** Solicita a câmera nativa e mantém seu stream como fonte ativa. */
  async function startNative() {
    if (stream || startingRef.current) return
    startingRef.current = true
    setMessage('')
    if (!navigator.mediaDevices?.getUserMedia) {
      startingRef.current = false
      return setMessage('Este navegador não oferece acesso à câmera.')
    }
    try {
      const next = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false })
      setStream(next)
      setActiveId(nativeCamera.id)
      setMessage('Câmera ativa. Captura automática em tempo real.')
    } catch { setMessage('Autorize o acesso à câmera para iniciar a captura automática.') }
    finally { startingRef.current = false }
  }

  /** Captura um quadro e envia ao pipeline de visão sem sobrepor requisições. */
  async function analyzeFrame() {
    const source = active.kind === 'native' ? videoRef.current : networkRef.current
    const sourceWidth = source?.videoWidth || source?.naturalWidth
    const sourceHeight = source?.videoHeight || source?.naturalHeight
    if (!sourceWidth || requestsRef.current || Date.now() < nextCaptureAtRef.current) return
    requestsRef.current++
    const capturedAt = Date.now()
    try {
      const scale = Math.min(1, 1280 / sourceWidth)
      const canvas = document.createElement('canvas')
      canvas.width = Math.round(sourceWidth * scale)
      canvas.height = Math.round(sourceHeight * scale)
      canvas.getContext('2d').drawImage(source, 0, 0, canvas.width, canvas.height)
      const blob = await new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', .86))
      if (!blob) return
      const body = new FormData()
      body.append('image', blob, 'frame.jpg')
      const result = await apiRequest('/api/vision/recognize', { method: 'POST', body })
      const detection = result.detections.find((item) => item.accepted) || result.detections[0] || null
      if (detection) setLastDetection({ ...detection, processingMs: result.processingMs })
      if (detection?.box) {
        const box = detection.box
        const padding = .08
        const sx = Math.max(0, (box.x - box.width * padding) * canvas.width)
        const sy = Math.max(0, (box.y - box.height * padding) * canvas.height)
        const sw = Math.min(canvas.width - sx, box.width * (1 + padding * 2) * canvas.width)
        const sh = Math.min(canvas.height - sy, box.height * (1 + padding * 2) * canvas.height)
        const crop = document.createElement('canvas')
        crop.width = 420
        crop.height = Math.max(100, Math.round(420 * sh / sw))
        crop.getContext('2d').drawImage(canvas, sx, sy, sw, sh, 0, 0, crop.width, crop.height)
        setLastCrop(crop.toDataURL('image/jpeg', .9))
      }
      if (detection?.accepted) {
        nextCaptureAtRef.current = capturedAt + captureDelayMs
        const movement = detection.crossing === 'Entry' ? 'entrada' : detection.crossing === 'Exit' ? 'saída' : null
        setMessage(result.recorded
          ? `Placa ${detection.plate}: ${movement} gravada na Portaria como ${result.vehicleName || 'Desconhecido'} em ${result.processingMs} ms.`
          : `Placa ${detection.plate} reconhecida em ${result.processingMs} ms. Atravesse a linha central para registrar entrada ou saída.`)
      } else if (detection) {
        const reason = detection.pendingConsensus ? 'confirmando a leitura em vários quadros' : !detection.formatValid ? 'os caracteres ainda estão incorretos' : !detection.insideRegion ? 'a placa ainda não cruzou a região' : detection.qualityScore < .2 ? 'o recorte está desfocado ou mal iluminado' : 'a confiança ainda está baixa'
        setMessage(`Leitura "${detection.plate || 'ilegível'}" não confirmada: ${reason}.`)
      }
      else setMessage('Analisando em tempo real. Aguardando a próxima placa.')
    } catch (error) {
      if (error.status === 429) nextCaptureAtRef.current = Date.now() + captureDelayMs
      setMessage(error.message)
    } finally { requestsRef.current-- }
  }

  /** Valida e persiste uma nova câmera da rede local. */
  async function registerCamera(event) {
    event.preventDefault()
    if (!privateIpv4(form.ipAddress)) return setMessage('Use um IPv4 privado da rede local.')
    if (cameras.some((camera) => camera.ipAddress === form.ipAddress)) return setMessage('Já existe uma câmera com esse IP.')
    let camera
    if (backendState === 'online') {
      try { camera = normalizeCamera(await apiRequest('/api/cameras', { method: 'POST', body: JSON.stringify(form) })) }
      catch (error) { return setMessage(error.message) }
    } else camera = { ...form, name: form.name.trim(), id: crypto.randomUUID(), kind: 'network' }
    setCameras([...cameras, camera])
    setActiveId(camera.id)
    stream?.getTracks().forEach((track) => track.stop())
    setStream(null)
    setForm({ name: '', ipAddress: '', port: 554 })
    setMessage('Câmera cadastrada e selecionada.')
  }

  /** Troca a fonte ativa no backend e na interface. */
  async function activate(camera) {
    if (backendState === 'online') {
      try { await apiRequest(`/api/cameras/${camera.id}/activate`, { method: 'POST' }) }
      catch (error) { return setMessage(error.message) }
    }
    setActiveId(camera.id)
    setNetworkReady(false)
    if (camera.kind === 'network') { stream?.getTracks().forEach((track) => track.stop()); setStream(null) }
    setMessage(`${camera.name} selecionada.`)
  }

  /** Exclui uma câmera de rede após confirmação do operador. */
  async function remove(camera) {
    if (!window.confirm(`Apagar a câmera ${camera.name}?`)) return
    if (backendState === 'online') {
      try { await apiRequest(`/api/cameras/${camera.id}`, { method: 'DELETE' }) }
      catch (error) { return setMessage(error.message) }
    }
    setCameras(cameras.filter((item) => item.id !== camera.id))
    if (activeId === camera.id) setActiveId(nativeCamera.id)
    setMessage(`${camera.name} removida.`)
  }

  /** Atualiza uma coordenada e conserva o retângulo dentro da imagem. */
  function updateRegion(key, value) {
    setRegion(normalizeRegion({ ...region, [key]: Number(value) }))
  }

  /** Persiste a região após o operador parar de movê-la. */
  async function saveRegion() {
    const normalized = normalizeRegion(region)
    if (backendState === 'online') {
      try { await apiRequest(`/api/cameras/${active.id}/region`, { method: 'PUT', body: JSON.stringify(normalized) }) }
      catch (error) { return setMessage(error.message) }
    }
    localStorage.setItem('platelens-gate-region', JSON.stringify(normalized))
    setMessage('Região de captura salva automaticamente.')
  }

  return <section className="monitor-page">
    <header className="monitor-header"><div><span className="eyebrow">OPERAÇÃO EM TEMPO REAL</span><h2>Central de monitoramento</h2><p>Selecione a fonte e ajuste onde uma placa pode gerar captura.</p></div><div className="camera-state camera-state--online"><i /><span>{active.name}</span></div></header>
    <div className="monitor-grid">
      <article className="live-panel">
        <div className="panel-title"><div><Icon name="camera" /><span>{active.name}</span></div><span className="source-chip">{active.kind === 'native' ? 'Dispositivo 0' : `${active.ipAddress}:${active.port}`}</span></div>
        <div className="camera-viewport gate-preview" ref={viewportRef}>
          {active.kind === 'network'
            ? <img ref={networkRef} src={`/api/cameras/${active.id}/stream`} alt={`Vídeo ao vivo de ${active.name}`} onLoad={() => { setNetworkReady(true); setMessage('Câmera IP conectada. Iniciando leitura automática.') }} onError={() => { setNetworkReady(false); setMessage('Não foi possível abrir o stream /video desta câmera.') }} />
            : stream ? <video ref={videoRef} autoPlay muted playsInline /> : <><div className="road-grid" /><div className="camera-overlay camera-overlay--transparent"><Icon name="camera" size={30} /><strong>Ativando câmera nativa</strong><span>Autorize o navegador para manter a captura automática.</span></div></>}
          <div className="capture-region" style={{ left: `${region.x * 100}%`, top: `${region.y * 100}%`, width: `${region.width * 100}%`, height: `${region.height * 100}%` }} onPointerDown={(event) => setInteraction({ mode: 'move', startX: event.clientX, startY: event.clientY, region })}>
            <span>REGIÃO DE CAPTURA</span><i onPointerDown={(event) => { event.stopPropagation(); setInteraction({ mode: 'resize', startX: event.clientX, startY: event.clientY, region }) }} aria-label="Redimensionar região" />
          </div>
          <div className="plate-sample">{lastCrop && <img src={lastCrop} alt="Recorte da última placa detectada" />}<div><small>{lastDetection ? (lastDetection.accepted ? 'PLACA CAPTURADA' : lastDetection.pendingConsensus ? 'CONFIRMANDO PLACA' : 'LEITURA NÃO VALIDADA') : 'AGUARDANDO PLACA'}</small><strong>{lastDetection?.plate || '-------'}</strong><span>{lastDetection ? `${lastDetection.plateType} · ${Math.round(lastDetection.confidence * 100)}% · qualidade ${Math.round((lastDetection.qualityScore ?? 0) * 100)}% · ${lastDetection.processingMs} ms` : 'o centro deve cruzar a região'}</span></div></div>
          <div className="live-label"><i /> {stream || networkReady ? 'AO VIVO · CAPTURA AUTOMÁTICA' : 'CONECTANDO'}</div>
        </div>
        <footer className="camera-meta"><div><span>Fonte</span><strong>{active.kind === 'native' ? 'Webcam USB / integrada' : 'IPv4 privado'}</strong></div><div><span>Gate</span><strong>Retângulo editável</strong></div><div><span>Captura</span><strong>Centro dentro da região</strong></div></footer>
        {active.kind === 'native' && !stream && <div className="monitor-actions"><button className="button" type="button" onClick={startNative}><Icon name="camera" />Tentar ativar câmera</button></div>}
      </article>
      <aside className="control-panel">
        <div className="panel-title"><div><Icon name="settings" /><span>Fontes e região</span></div></div>
        <div className="control-content">
          <div className="control-section-heading"><div><span className="eyebrow">REDE LOCAL</span><h3>Cadastrar câmera</h3></div><span>{backendState === 'online' ? 'API conectada' : 'Modo demonstração'}</span></div>
          <form className="camera-form" onSubmit={registerCamera}><label>Nome<input required maxLength="64" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="Entrada principal" /></label><label>Endereço IPv4<input required inputMode="decimal" value={form.ipAddress} onChange={(event) => setForm({ ...form, ipAddress: event.target.value })} placeholder="192.168.1.20" /></label><label>Porta<input required type="number" min="1" max="65535" value={form.port} onChange={(event) => setForm({ ...form, port: Number(event.target.value) })} /></label><button className="button" type="submit"><Icon name="plus" />Cadastrar câmera</button></form>
          <div className="region-controls"><div className="control-section-heading"><div><span className="eyebrow">ÁREA DE INTERESSE</span><h3>Região de captura</h3></div></div>{[['x', 'Posição horizontal'], ['y', 'Posição vertical'], ['width', 'Largura'], ['height', 'Altura']].map(([key, label]) => <label className="control-field" key={key}><span><span className="control-label">{label}</span><output>{Math.round(region[key] * 100)}%</output></span><input aria-label={label} type="range" min={key === 'width' || key === 'height' ? 10 : 0} max={key === 'x' ? (1 - region.width) * 100 : key === 'y' ? (1 - region.height) * 100 : key === 'width' ? (1 - region.x) * 100 : (1 - region.y) * 100} value={region[key] * 100} onChange={(event) => updateRegion(key, event.target.value / 100)} /></label>)}</div>
          <div className="camera-list"><div className="camera-list-heading"><span>Câmeras cadastradas</span><small>{cameras.length}</small></div>{cameras.map((camera) => <article key={camera.id}><span className="camera-list-icon"><Icon name="camera" /></span><div className="camera-list-data"><strong>{camera.name}</strong><span>{camera.kind === 'native' ? 'Dispositivo 0' : `${camera.ipAddress}:${camera.port}`}</span></div><div className="camera-list-actions"><b>{camera.id === activeId ? 'Selecionada' : 'Salva'}</b><button type="button" onClick={() => activate(camera)}>{camera.id === activeId ? 'Ativa' : 'Selecionar'}</button>{camera.kind !== 'native' && <button className="camera-delete" type="button" onClick={() => remove(camera)}><Icon name="trash" />Apagar</button>}</div></article>)}</div>
          {message && <p className="control-message" role="status">{message}</p>}
        </div>
      </aside>
    </div>
  </section>
}
