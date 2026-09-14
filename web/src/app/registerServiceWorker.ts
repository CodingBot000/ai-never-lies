export async function registerServiceWorker(): Promise<ServiceWorkerRegistration | null> {
  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return null

  const base = import.meta.env.BASE_URL.endsWith('/') ? import.meta.env.BASE_URL : `${import.meta.env.BASE_URL}/`
  try {
    const registration = await navigator.serviceWorker.register(`${base}sw.js`, { scope: base })
    await navigator.serviceWorker.ready
    document.documentElement.dataset.offlineReady = 'true'
    window.dispatchEvent(new CustomEvent('anl:offline-ready'))
    return registration
  } catch (error) {
    document.documentElement.dataset.offlineReady = 'failed'
    console.warn('오프라인 캐시를 준비하지 못했습니다.', error)
    return null
  }
}
