import { assetUrl, getAsset, type AssetRegion } from './assetCatalog'
import { regionLabel, type EvidenceOverlay } from './evidenceOverlays'
import { regionToRect } from './regionMath'
import './evidence-image.css'

interface EvidenceImageProps {
  assetId: string
  alt: string
  regions?: AssetRegion[]
  activeRegionIds?: string[]
  highlightRegionId?: string
  onRegionClick?: (regionId: string) => void
  showRegions?: boolean
  overlays?: EvidenceOverlay[]
}

/** asset ID→URL 카탈로그로만 이미지를 표시하고, region 오버레이는 CSS 픽셀로 계산한다. */
export function EvidenceImage({
  assetId,
  alt,
  regions = [],
  activeRegionIds = [],
  highlightRegionId,
  onRegionClick,
  showRegions = true,
  overlays = [],
}: EvidenceImageProps) {
  const entry = getAsset(assetId)
  const url = assetUrl(assetId)
  if (!entry || !url) {
    return (
      <div className="anl-evidence-image anl-evidence-image--missing" role="img" aria-label={alt}>
        이미지 없음: {assetId}
      </div>
    )
  }

  const displayed = { x: 0, y: 0, width: 100, height: 100 }

  return (
    <div
      className="anl-evidence-image"
      data-asset-id={assetId}
      style={{ aspectRatio: `${entry.width} / ${entry.height}` }}
    >
      <img src={url} alt={alt} width={entry.width} height={entry.height} />
      {showRegions
        ? regions.map((region) => {
            const rect = regionToRect(displayed, region)
            const active = activeRegionIds.includes(region.regionId)
            const highlighted = highlightRegionId === region.regionId
            const className = [
              'anl-region',
              active ? 'anl-region--active' : '',
              highlighted ? 'anl-region--highlight' : '',
            ]
              .filter(Boolean)
              .join(' ')
            return (
              <button
                key={region.regionId}
                type="button"
                className={className}
                data-region-id={region.regionId}
                style={{
                  left: `${rect.x}%`,
                  top: `${rect.y}%`,
                  width: `${rect.width}%`,
                  height: `${rect.height}%`,
                }}
                aria-label={`조사 지점 ${regionLabel(assetId, region.regionId)}`}
                onClick={(event) => {
                  event.stopPropagation()
                  onRegionClick?.(region.regionId)
                }}
              />
            )
          })
        : null}
      <div className="anl-image-overlays" aria-label="이미지 판독 정보">
        {overlays.map((overlay) => {
          const sourceRect = overlay.regionId
            ? regions.find((region) => region.regionId === overlay.regionId)
            : overlay.rect
          if (!sourceRect) return null
          const rect = regionToRect(displayed, sourceRect)
          return (
            <div
              key={overlay.id}
              className={`anl-image-overlay anl-image-overlay--${overlay.variant} anl-image-overlay--${overlay.tone}`}
              data-annotation-id={overlay.id}
              style={{
                left: `${rect.x}%`,
                top: `${rect.y}%`,
                width: `${rect.width}%`,
                height: `${rect.height}%`,
              }}
            >
              {overlay.lines.map((line) => (
                <span key={line}>{line}</span>
              ))}
            </div>
          )
        })}
      </div>
    </div>
  )
}
