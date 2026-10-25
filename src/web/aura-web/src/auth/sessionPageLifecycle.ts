type SessionPageLifecycleOptions = {
  suspend: () => void
  root: HTMLElement | null
  target?: Window
  reload?: () => void
}

/** A restored document must not reuse its in-memory Firebase user or profile. */
export function bindSessionPageLifecycle({
  suspend, root, target = window, reload = () => target.location.reload(),
}: SessionPageLifecycleOptions) {
  let suspended = false
  let reloadRequested = false

  function concealAndSuspend() {
    // Conceal synchronously, before React can schedule a render or a cache snapshot.
    if (root) {
      root.hidden = true
      root.setAttribute('inert', '')
      root.style.setProperty('visibility', 'hidden', 'important')
    }
    if (suspended) return
    suspended = true
    suspend()
  }

  function onPageShow(event: PageTransitionEvent) {
    if (!event.persisted) return
    concealAndSuspend()
    if (reloadRequested) return
    reloadRequested = true
    // Reinitialize the SDK from current persistence; never resume a cached store.
    reload()
  }

  target.addEventListener('pagehide', concealAndSuspend)
  target.addEventListener('pageshow', onPageShow)
  return () => {
    target.removeEventListener('pagehide', concealAndSuspend)
    target.removeEventListener('pageshow', onPageShow)
  }
}
