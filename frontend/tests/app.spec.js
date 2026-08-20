import { expect, test } from '@playwright/test'

test.beforeEach(async ({ page }) => {
  let vehicles = []
  const analytics = {
    period: { from: '2026-08-14', to: '2026-08-20' },
    summary: { entries: 3, exits: 2, inside: 1, unknown: 1, total: 5 },
    daily: [{ label: '20/08', entries: 3, exits: 2, count: 5 }],
    hourly: [{ label: '06h', entries: 3, exits: 2, count: 5 }],
    byType: [{ name: 'Passeio', count: 1 }], frequentVehicles: [], averageStayByType: [], recentEvents: [],
    recognition: { platesDetected: 12, ocrValid: 9, ocrInvalid: 3, recognitionRate: 75, averageDetectionConfidence: 90, averageOcrConfidence: 85, averageProcessingMs: 240, rejected: 3 },
    cameras: [{ id: '1', name: 'Câmera nativa', online: true }], peakHour: '06h',
  }
  await page.route('**/api/cameras', (route) => route.abort())
  await page.route('**/api/analytics**', (route) => route.fulfill({ json: analytics }))
  await page.route('**/api/access-events/stream', (route) => route.abort())
  await page.route('**/api/access-events', (route) => route.fulfill({ json: [] }))
  await page.route('**/api/vehicles**', (route) => {
    const method = route.request().method()
    if (method === 'GET') return route.fulfill({ json: vehicles })
    const id = route.request().url().split('/').pop()
    if (method === 'DELETE') {
      vehicles = vehicles.filter((item) => item.id !== id)
      return route.fulfill({ status: 204 })
    }
    const body = route.request().postDataJSON()
    if (method === 'POST') {
      const saved = { ...body, id: '10000000-0000-0000-0000-000000000001' }
      vehicles = [...vehicles.filter((item) => item.plate !== saved.plate), saved]
      return route.fulfill({ json: saved })
    }
    vehicles = vehicles.map((item) => item.id === id ? { ...item, ...body } : item)
    return route.fulfill({ status: 204 })
  })
})

test('visão geral e estatísticas usam o analytics central', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByText('5 movimentos registrados hoje')).toBeVisible()
  await page.goto('/analytics')
  await expect(page.getByText('12', { exact: true })).toBeVisible()
  await expect(page.getByText('75%', { exact: true })).toBeVisible()
})

for (const [path, heading] of [['/', 'Visão geral'], ['/portaria', 'Portaria'], ['/vehicles', 'Veículos'], ['/monitoring', 'Monitoramento'], ['/analytics', 'Estatísticas']]) {
  test(`${heading} renderiza sem overflow`, async ({ page }) => {
    await page.goto(path)
    await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible()
    expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false)
  })
}

test('veículo desconhecido pode ser identificado', async ({ page }) => {
  await page.goto('/vehicles')
  await expect(page.getByText('0 registros')).toBeVisible()
  await page.getByLabel('Nome').fill('Desconhecido')
  await page.getByLabel('Placa').fill('ABC1D23')
  await page.getByLabel('Tipo').selectOption('Desconhecido')
  await page.getByRole('button', { name: 'Cadastrar veículo' }).click()
  await page.getByRole('button', { name: 'Editar' }).click()
  await page.getByLabel('Nome').fill('Visitante autorizado')
  await page.getByRole('button', { name: 'Salvar alterações' }).click()
  await expect(page.getByText('Visitante autorizado')).toBeVisible()
  page.once('dialog', (dialog) => dialog.accept())
  await page.getByRole('button', { name: 'Apagar ABC1D23' }).click()
  await expect(page.getByText('0 registros')).toBeVisible()
})

test('câmera IP e região de captura podem ser configuradas', async ({ page }) => {
  await page.goto('/monitoring')
  await page.getByLabel('Nome').fill('Entrada lateral')
  await page.getByLabel('Endereço IPv4').fill('192.168.1.20')
  await page.getByRole('button', { name: /Cadastrar câmera/ }).click()
  await expect(page.locator('.camera-list').getByText('192.168.1.20:554')).toBeVisible()
  await page.getByRole('slider', { name: 'Largura' }).fill('45')
  await expect(page.locator('.control-message')).toContainText('Região de captura salva automaticamente')
  await expect(page.getByRole('button', { name: 'Salvar região' })).toHaveCount(0)
})

test('câmera nativa inicia o reconhecimento automaticamente', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('platelens-gate-region', JSON.stringify({ x: .6, y: .2, width: .6, height: .8 }))
    navigator.mediaDevices.getUserMedia = async () => new MediaStream()
    Object.defineProperty(HTMLVideoElement.prototype, 'videoWidth', { get: () => 1280 })
    Object.defineProperty(HTMLVideoElement.prototype, 'videoHeight', { get: () => 720 })
    HTMLCanvasElement.prototype.getContext = () => ({ drawImage() {} })
    HTMLCanvasElement.prototype.toBlob = function (callback) { callback(new Blob(['frame'], { type: 'image/jpeg' })) }
    HTMLCanvasElement.prototype.toDataURL = () => 'data:image/jpeg;base64,AA=='
  })
  let attempts = 0
  await page.route('**/api/vision/recognize', async (route) => {
    attempts++
    if (attempts === 1) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return route.fulfill({ status: 503, json: { message: 'Falha temporária' } })
    }
    return route.fulfill({ json: { recorded: true, vehicleName: 'Desconhecido', detections: [{ accepted: true, plate: 'ABC1D23', plateType: 'MERCOSUL', confidence: .91, box: { x: .3, y: .3, width: .2, height: .1 } }] } })
  })
  await page.goto('/monitoring', { waitUntil: 'domcontentloaded' })
  await expect.poll(() => attempts, { timeout: 5000 }).toBeGreaterThanOrEqual(2)
  await expect(page.getByText('AO VIVO · CAPTURA AUTOMÁTICA')).toBeVisible()
  await expect(page.locator('.control-message')).toContainText('gravada na Portaria como Desconhecido')
  await expect(page.getByText('ABC1D23', { exact: true })).toBeVisible()
  await expect(page.getByAltText('Recorte da última placa detectada')).toBeVisible()
  await expect(page.getByRole('slider', { name: 'Largura' })).toHaveValue('40')
  await expect(page.getByRole('button', { name: /reconhecimento/ })).toHaveCount(0)
  await page.waitForTimeout(1500)
  expect(attempts).toBe(2)
})

test('raiz abre diretamente sem autenticação', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1, name: 'Visão geral' })).toBeVisible()
})
