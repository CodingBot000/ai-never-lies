export type OverlayTone = 'info' | 'warning' | 'danger' | 'message' | 'muted'
export type OverlayVariant = 'box' | 'panel' | 'tag' | 'screen'

export interface OverlayRect {
  x: number
  y: number
  width: number
  height: number
}

export interface EvidenceOverlay {
  id: string
  regionId?: string
  rect?: OverlayRect
  lines: string[]
  tone: OverlayTone
  variant: OverlayVariant
}

interface EvidenceOverlayDefinition {
  assetId: string
  overlays: EvidenceOverlay[]
}

/**
 * 이미지에 굽지 않기로 한 사건 값의 런타임 표현.
 * 같은 원본 이미지를 쓰는 ev03/ev06은 획득 시점에 따라 공개 정보가 다르다.
 */
const EVIDENCE_OVERLAYS: Record<string, EvidenceOverlayDefinition> = {
  ev01: {
    assetId: 'ev_vehicle_side',
    overlays: [
      {
        id: 'vehicle-identity',
        regionId: 'unit_label',
        lines: ['NV-017', '2032-10-18 23:42:17.221', '강우 · 승객 1명'],
        tone: 'info',
        variant: 'tag',
      },
    ],
  },
  ev03: {
    assetId: 'ev_front_base',
    overlays: [
      {
        id: 'reference-frame',
        rect: { x: 0.025, y: 0.03, width: 0.34, height: 0.1 },
        lines: ['선정된 기준 프레임', '시간별 기록은 오른쪽 자료에서 확인'],
        tone: 'muted',
        variant: 'panel',
      },
      {
        id: 'pedestrian-location',
        regionId: 'pedestrian',
        lines: ['보행자 위치'],
        tone: 'info',
        variant: 'box',
      },
      {
        id: 'crosswalk-location',
        regionId: 'crosswalk',
        lines: ['횡단보도'],
        tone: 'muted',
        variant: 'tag',
      },
    ],
  },
  ev05: {
    assetId: 'ev_cabin_base',
    overlays: [
      {
        id: 'cabin-warning',
        regionId: 'warning_display',
        lines: ['EMERGENCY MANEUVER', '동기화 T-1.79'],
        tone: 'warning',
        variant: 'screen',
      },
    ],
  },
  ev06: {
    assetId: 'ev_front_base',
    overlays: [
      {
        id: 'live-decision-frame',
        rect: { x: 0.025, y: 0.03, width: 0.4, height: 0.14 },
        lines: ['차량 당시 모델 P3.8', 'T-1.84 · P-127 · 보행자 0.947', 'T-1.82 · 전방위험 91.2/100', '요청 BR-884 · -8.1 m/s²'],
        tone: 'info',
        variant: 'panel',
      },
      {
        id: 'pedestrian-detection',
        regionId: 'pedestrian',
        lines: ['P-127', 'PEDESTRIAN · 0.947'],
        tone: 'info',
        variant: 'box',
      },
    ],
  },
  ev12: {
    assetId: 'ev_rear_empty',
    overlays: [
      {
        id: 'rear-lane-observation',
        regionId: 'rear_lane',
        lines: ['T-2.0 ~ T-0.3', '후방 0~15 m', '접근 차량 미관측'],
        tone: 'info',
        variant: 'box',
      },
    ],
  },
  ev13: {
    assetId: 'ev_junction_wide',
    overlays: [
      {
        id: 'cctv-taxi',
        regionId: 'taxi',
        lines: ['NV-017'],
        tone: 'info',
        variant: 'box',
      },
      {
        id: 'cctv-rear-lane',
        regionId: 'rear_lane',
        lines: ['교차 검증 구간', '접근 차량 없음', '시간 오차 ±40 ms'],
        tone: 'muted',
        variant: 'tag',
      },
    ],
  },
  ev14: {
    assetId: 'ev_radar_module',
    overlays: [
      {
        id: 'radar-packet',
        regionId: 'label',
        lines: ['RR-02 · R-09', '거리 4.7 m · 접근 11 km/h', '위협 71/100 · INVALID'],
        tone: 'danger',
        variant: 'screen',
      },
    ],
  },
  ev15: {
    assetId: 'ev_sensor_mount',
    overlays: [
      {
        id: 'maintenance-warning',
        regionId: 'sensor',
        lines: ['RR-02', 'D-11 08:57', 'INTERMITTENT GHOST OBJECT', 'OUT_OF_SERVICE'],
        tone: 'danger',
        variant: 'tag',
      },
    ],
  },
  ev16: {
    assetId: 'ev_service_workbench',
    overlays: [
      {
        id: 'replacement-inventory',
        regionId: 'inventory_label',
        lines: ['교체 부품 입고 예정 D+2', '주간·건조 한정 승인', '야간 강우 배차 금지'],
        tone: 'warning',
        variant: 'screen',
      },
    ],
  },
  ev35_message: {
    assetId: 'ev_phone_base',
    overlays: [
      {
        id: 'last-message',
        regionId: 'screen',
        lines: ['거의 다 왔어.', '전송되지 않음'],
        tone: 'message',
        variant: 'screen',
      },
    ],
  },
}

const REGION_LABELS: Record<string, Record<string, string>> = {
  bg_crosswalk_dawn: {
    crosswalk: '횡단보도',
    signal: '보행자 신호',
    vehicle: '사고 차량 위치',
  },
  ev_front_base: {
    pedestrian: '보행자 판독 영역',
    crosswalk: '횡단보도 영역',
  },
  ev_rear_empty: { rear_lane: '후방 0~15미터 차로' },
  ev_junction_wide: { taxi: '사고 차량 NV-017', rear_lane: '사고 차량 뒤 차로' },
  ev_cabin_base: { warning_display: '차내 경고 표시창' },
  ev_radar_module: { label: '레이더 모듈 식별 라벨' },
  ev_sensor_mount: { sensor: '후방 센서 장착부' },
  ev_vehicle_side: { unit_label: '사고 차량 식별 영역' },
  ev_phone_base: { screen: '휴대전화 메시지 화면' },
  ev_service_workbench: { inventory_label: '교체 부품 재고 라벨' },
}

export function overlaysForEvidence(evidenceId: string): EvidenceOverlay[] {
  return EVIDENCE_OVERLAYS[evidenceId]?.overlays ?? []
}

export function overlayAssetForEvidence(evidenceId: string): string | null {
  return EVIDENCE_OVERLAYS[evidenceId]?.assetId ?? null
}

export function regionLabel(assetId: string, regionId: string): string {
  return REGION_LABELS[assetId]?.[regionId] ?? regionId
}

export function investigationOverlays(assetId: string): EvidenceOverlay[] {
  if (assetId !== 'bg_crosswalk_dawn') return []
  return [
    { id: 'crosswalk-hotspot', regionId: 'crosswalk', lines: ['횡단보도'], tone: 'info', variant: 'tag' },
    { id: 'signal-hotspot', regionId: 'signal', lines: ['보행자 신호'], tone: 'info', variant: 'tag' },
    { id: 'vehicle-hotspot', regionId: 'vehicle', lines: ['사고 차량'], tone: 'info', variant: 'tag' },
  ]
}

export function overlayDefinitionIds(): string[] {
  return Object.keys(EVIDENCE_OVERLAYS)
}
