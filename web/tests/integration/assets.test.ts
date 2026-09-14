import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import generated from '../../src/shared/assets/assetCatalog.generated.json'
import { loadContent } from '../../src/content/loadContent'

const ROOT = join(process.cwd(), '..')
const MANIFEST_PATH = join(ROOT, 'handoff', 'art', 'manifest.lock.json')

interface CatalogEntry {
  id: string
  url: string
  kind: string
  width: number
  height: number
  sha256: string
  regions: { regionId: string }[]
}

interface CatalogFile {
  assetCount: number
  manifestSha256: string
  assets: Record<string, CatalogEntry>
}

const catalog = generated as unknown as CatalogFile

describe('아트 카탈로그 계약 (W2)', () => {
  it('54개 자산이 asset ID로 모두 조회되고 URL이 중복되지 않는다', () => {
    const ids = Object.keys(catalog.assets)
    expect(ids).toHaveLength(54)
    expect(catalog.assetCount).toBe(54)

    const urls = ids.map((id) => catalog.assets[id]?.url ?? '')
    expect(new Set(urls).size).toBe(54)
    for (const url of urls) {
      expect(url).toMatch(/^\/art\/ep01\/(backgrounds|portraits|evidence|cg)\/[a-z0-9_]+\.png$/)
    }
  })

  it('카탈로그 해시가 manifest.lock.json과 일치한다', () => {
    const manifest = JSON.parse(readFileSync(MANIFEST_PATH, 'utf8')) as {
      assets: { id: string; path: string; sha256: string; actualWidth: number; actualHeight: number }[]
    }
    expect(manifest.assets).toHaveLength(54)

    for (const asset of manifest.assets) {
      const entry = catalog.assets[asset.id]
      expect(entry, `catalog entry ${asset.id}`).toBeTruthy()
      if (!entry) continue
      expect(entry.url).toBe(`/art/${asset.path}`)
      expect(entry.sha256).toBe(asset.sha256)
      expect(entry.width).toBe(asset.actualWidth)
      expect(entry.height).toBe(asset.actualHeight)
    }
  })

  it('region 좌표가 0~1 정규화 범위를 지킨다', () => {
    for (const entry of Object.values(catalog.assets)) {
      for (const region of entry.regions) {
        expect(region.regionId, `${entry.id} region id`).toBeTruthy()
        const regionRecord = region as unknown as Record<string, number | string>
        for (const key of ['x', 'y', 'width', 'height']) {
          const value = regionRecord[key]
          expect(typeof value, `${entry.id}.${region.regionId}.${key}`).toBe('number')
          expect(value as number).toBeGreaterThanOrEqual(0)
          expect(value as number).toBeLessThanOrEqual(1)
        }
      }
    }
  })
})

describe('브라우저 콘텐츠 로더 (W2)', () => {
  it('정적 콘텐츠를 로드하고 검증을 통과한다', async () => {
    const publicContentDir = join(ROOT, 'web', 'public', 'content', 'ep01')
    const fetchFn = async (input: string): Promise<Response> => {
      const name = input.split('/').pop() ?? ''
      if (name === 'manifest.json') {
        return new Response('{}', { status: 200 })
      }
      try {
        const text = readFileSync(join(publicContentDir, name), 'utf8')
        return new Response(text, { status: 200 })
      } catch {
        return new Response('not found', { status: 404 })
      }
    }
    const result = await loadContent(fetchFn, 'content/ep01')
    expect(result.errors, result.errors.join('\n')).toEqual([])
    expect(result.ok).toBe(true)
    expect(result.index?.scenes.size).toBe(43)
    expect(result.index?.evidence.size).toBe(36)
    expect(result.index?.puzzles.size).toBe(20)
  })
})
