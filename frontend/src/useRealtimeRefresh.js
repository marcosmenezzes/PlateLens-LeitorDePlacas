import { useEffect } from 'react'

/** Mantém uma conexão SSE e pede nova consulta quando o backend grava um acesso. */
export function useRealtimeRefresh(refresh) {
  useEffect(() => {
    const stream = new EventSource('/api/access-events/stream')
    stream.onmessage = refresh
    return () => stream.close()
  }, [refresh])
}
