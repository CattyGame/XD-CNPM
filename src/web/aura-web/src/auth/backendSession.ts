import { createContext, useContext } from 'react'
import type { BackendSessionState } from './backendSessionStore'

export type BackendSession = {
  state: BackendSessionState
  retry: () => void
  logout: () => Promise<void>
}

export const BackendSessionContext = createContext<BackendSession | null>(null)

export function useBackendSession(): BackendSession {
  const session = useContext(BackendSessionContext)
  if (!session) throw new Error('BackendSessionProvider is required.')
  return session
}
