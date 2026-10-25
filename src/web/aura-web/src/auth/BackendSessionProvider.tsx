import { useEffect, useState, useSyncExternalStore, type ReactNode } from 'react'
import { BackendSessionContext } from './backendSession'
import { createBackendSessionStore } from './backendSessionStore'
import { bindSessionPageLifecycle } from './sessionPageLifecycle'

export default function BackendSessionProvider({ children }: { children: ReactNode }) {
  const [store] = useState(createBackendSessionStore)
  const state = useSyncExternalStore(store.subscribe, store.getSnapshot)
  useEffect(() => {
    const detachLifecycle = bindSessionPageLifecycle({
      suspend: store.suspendForNavigation,
      root: document.getElementById('root'),
    })
    const stop = store.start()
    return () => { detachLifecycle(); stop() }
  }, [store])

  return (
    <BackendSessionContext.Provider value={{ state, retry: store.retry, logout: store.logout }}>
      {children}
    </BackendSessionContext.Provider>
  )
}
