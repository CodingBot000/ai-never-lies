import generated from './assetCatalog.generated.json'

export interface AssetRegion {
  regionId: string
  x: number
  y: number
  width: number
  height: number
}

export interface AssetCatalogEntry {
  id: string
  /** 카탈로그 기준 상대 URL. 실행 시에는 assetUrl()을 사용한다. */
  url: string
  kind: string
  width: number
  height: number
  anchor: { x: number; y: number } | null
  regions: AssetRegion[]
  sha256: string
}

interface AssetCatalogFile {
  schemaVersion: number
  contractVersion: string
  episodeId: string
  manifestSha256: string
  assetCount: number
  assets: Record<string, AssetCatalogEntry>
}

const catalog = generated as unknown as AssetCatalogFile

export const assetManifestSha256 = catalog.manifestSha256

/** asset ID→URL. 파일명 추측 로딩을 금지한다. */
export function assetUrl(id: string): string | null {
  const entry = catalog.assets[id]
  if (!entry) return null
  const base = import.meta.env.BASE_URL || '/'
  return `${base.replace(/\/$/, '')}/${entry.url.replace(/^\//, '')}`
}

export function getAsset(id: string): AssetCatalogEntry | null {
  return catalog.assets[id] ?? null
}

export function assetRegions(id: string): AssetRegion[] {
  return catalog.assets[id]?.regions ?? []
}

export function assetIds(): string[] {
  return Object.keys(catalog.assets)
}

export function assetCount(): number {
  return catalog.assetCount
}
