/** 퍼즐 ID → 표시 이름. 다국어는 SCENE_LABELS와 동일한 방식으로 en 키를 추가한다. */
import type { Locale, LocalizedText } from './sceneLabels'

export const PUZZLE_LABELS: Record<string, LocalizedText> = {
  p01: { ko: '현장 기준 세우기' },
  p02: { ko: '무반응이라는 기억' },
  p03: { ko: '같은 41%와 94.7%인가' },
  p04: { ko: '당시 AI는 보행자를 보지 못했다' },
  p05: { ko: '1.36초의 의미' },
  p06: { ko: '명령이 바뀐 곳' },
  p07: { ko: '존재하지 않는 뒤차' },
  p08: { ko: '수리 승인과 운행중지' },
  p09: { ko: '위험 보고를 못 받았다' },
  p10: { ko: '46대는 멀쩡했다' },
  p11: { ko: '없어진 검증 경로' },
  p12: { ko: '승인하지 않았다는 개발자' },
  p13: { ko: '지켜지지 않은 전제' },
  p14: { ko: '1,240만 번의 빈칸' },
  p15: { ko: '그 코드가 그 차에 있었는가' },
  p16: { ko: '두 안전장치의 반사실 시험' },
  p17: { ko: '사고 인과관계 재구성' },
  p18: { ko: '그 위험을 보고받지 않았다' },
  p19: { ko: '불가피했던 초기 설명인가' },
  p20: { ko: '사실에 맞는 최종 보고' },
}

export function puzzleLabel(id: string | null | undefined, locale: Locale = 'ko'): string {
  if (!id) return ''
  const entry = PUZZLE_LABELS[id]
  if (!entry) return id
  return entry[locale] ?? entry.ko
}
