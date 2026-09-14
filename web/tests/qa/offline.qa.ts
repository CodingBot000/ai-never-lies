import { expect, test } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const catalog = JSON.parse(
  readFileSync(join(process.cwd(), 'src/shared/assets/assetCatalog.generated.json'), 'utf8'),
) as { assets: Record<string, { url: string }> }
const manifest = JSON.parse(readFileSync(join(process.cwd(), 'public/content/manifest.json'), 'utf8')) as {
  files: Record<string, string>
}
const REQUIRED_URLS = [
  '/',
  '/favicon.svg',
  '/fonts/NanumGothic-Regular.ttf',
  '/fonts/NanumGothic-Bold.ttf',
  '/content/manifest.json',
  ...Object.keys(manifest.files).map((name) => `/content/ep01/${name}`),
  ...Object.values(catalog.assets).map((asset) => asset.url),
]

test('production 빌드가 완전 오프라인 재로드와 전체 사건 자산을 지원한다', async ({ page, context }) => {
  test.setTimeout(180_000)
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(String(error)))
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })
  page.on('dialog', (dialog) => dialog.accept())

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeVisible()
  await page.waitForFunction(() => document.documentElement.dataset.offlineReady === 'true', null, { timeout: 120_000 })
  await page.reload()
  await expect(page.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeVisible()

  const cacheSnapshot = await page.evaluate(async () => {
    const snapshots: { name: string; urls: string[] }[] = []
    for (const name of await window.caches.keys()) {
      const cache = await window.caches.open(name)
      snapshots.push({ name, urls: (await cache.keys()).map((request) => request.url) })
    }
    return snapshots
  })
  expect(cacheSnapshot).toHaveLength(1)
  expect(cacheSnapshot[0]?.urls).toEqual(expect.arrayContaining(REQUIRED_URLS.map((url) => new URL(url, 'http://localhost:4173').href)))

  await context.setOffline(true)
  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeVisible()

  const results = await page.evaluate(async (urls) => {
    return Promise.all(
      urls.map(async (url) => {
        try {
          const response = await fetch(url)
          return { url, ok: response.ok, status: response.status }
        } catch (error) {
          return { url, ok: false, status: 0, error: String(error) }
        }
      }),
    )
  }, REQUIRED_URLS)
  expect(results.filter((result) => !result.ok), JSON.stringify(results.filter((result) => !result.ok), null, 2)).toEqual([])

  await page.getByRole('button', { name: /새 게임/ }).press('Enter')
  await expect(page.getByText('이 작품의 인물·회사·사건·조사기관은 허구입니다.')).toBeVisible()
  expect(errors).toEqual([])
  await context.setOffline(false)
})
