import type { AssetRegion } from './assetCatalog'

export interface Size {
  width: number
  height: number
}

export interface Rect {
  x: number
  y: number
  width: number
  height: number
}

export interface Point {
  x: number
  y: number
}

/**
 * contain 모드에서 실제 표시되는 이미지 사각형을 계산한다. (CSS 픽셀, 좌상단 원점)
 * 이미지 크기가 0이면 패널 전체로 폴백한다.
 */
export function computeContainRect(panel: Size, image: Size): Rect {
  if (image.width <= 0 || image.height <= 0) {
    return { x: 0, y: 0, width: panel.width, height: panel.height }
  }
  const scale = Math.min(panel.width / image.width, panel.height / image.height)
  const width = image.width * scale
  const height = image.height * scale
  return { x: (panel.width - width) / 2, y: (panel.height - height) / 2, width, height }
}

/** 정규화 region을 표시 사각형 기준 CSS 픽셀 사각형으로 변환한다. 웹은 Y가 아래로 증가한다. */
export function regionToRect(displayed: Rect, region: Rect): Rect {
  return {
    x: displayed.x + region.x * displayed.width,
    y: displayed.y + region.y * displayed.height,
    width: region.width * displayed.width,
    height: region.height * displayed.height,
  }
}

/** 점이 region 내부인지 판정. 경계는 포함한다. */
export function tryRegionLocalPoint(point: Point, displayed: Rect, region: AssetRegion | null): boolean {
  if (!region) return false
  const rect = regionToRect(displayed, region)
  return (
    point.x >= rect.x &&
    point.x <= rect.x + rect.width &&
    point.y >= rect.y &&
    point.y <= rect.y + rect.height
  )
}
