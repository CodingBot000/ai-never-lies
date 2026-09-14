import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'

const CATALOG_PATH = join(process.cwd(), 'src', 'shared', 'assets', 'assetCatalog.generated.json')
const CONTENT_MANIFEST_PATH = join(process.cwd(), 'public', 'content', 'manifest.json')

test('54개 이미지와 콘텐츠 JSON이 404 없이 로드된다', async ({ request }) => {
  const catalog = JSON.parse(readFileSync(CATALOG_PATH, 'utf8')) as {
    assets: Record<string, { url: string }>
  }
  const ids = Object.keys(catalog.assets)
  expect(ids).toHaveLength(54)

  const failures: string[] = []
  for (const id of ids) {
    const url = catalog.assets[id]?.url
    if (!url) {
      failures.push(`${id}: missing url`)
      continue
    }
    const response = await request.get(url)
    if (response.status() !== 200) failures.push(`${id} ${url}: HTTP ${response.status()}`)
  }

  const contentManifest = JSON.parse(readFileSync(CONTENT_MANIFEST_PATH, 'utf8')) as { files: Record<string, string> }
  for (const name of Object.keys(contentManifest.files)) {
    const response = await request.get(`/content/ep01/${name}`)
    if (response.status() !== 200) failures.push(`content ${name}: HTTP ${response.status()}`)
  }

  expect(failures, failures.join('\n')).toEqual([])
})

test('카탈로그에 없는 파일명 추측 요청은 이미지로 제공되지 않는다', async ({ request }) => {
  const guessed = await request.get('/art/ep01/backgrounds/not_a_real_asset.png')
  expect(guessed.headers()['content-type'] ?? '').not.toContain('image/png')
})
