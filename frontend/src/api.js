/** Executa chamadas JSON/FormData e transforma respostas de erro em mensagens exibíveis. */
export async function apiRequest(path, options = {}) {
  const headers = options.body instanceof FormData ? options.headers : { 'Content-Type': 'application/json', ...options.headers }
  const response = await fetch(path, { credentials: 'include', ...options, headers })
  const text = response.status === 204 ? '' : await response.text()
  let data = null
  try { data = text ? JSON.parse(text) : null } catch { data = null }
  if (!response.ok) {
    const fallback = { 403: 'Ação não permitida.', 429: 'Muitas capturas seguidas. Tentando novamente em alguns segundos.' }
    const error = new Error(data?.message || data?.error || fallback[response.status] || 'Não foi possível concluir a ação')
    error.status = response.status
    throw error
  }
  return data
}
