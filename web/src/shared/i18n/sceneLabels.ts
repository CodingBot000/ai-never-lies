/**
 * 장면 ID → 표시 이름. 언어 추가 시 같은 항목에 로케일 키를 추가한다.
 * 화면에는 항상 sceneLabel()을 사용하고 ID를 직접 노출하지 않는다.
 */
export type Locale = 'ko' | 'en'

export interface LocalizedText {
  ko: string
  en?: string
}

export const SCENE_LABELS: Record<string, LocalizedText> = {
  s000: { ko: '시작과 접근성 설정' },
  s010: { ko: '비 오는 귀갓길' },
  s020: { ko: '사건 인수' },
  s100: { ko: '첫 조사 허브' },
  s110: { ko: '현장 조사' },
  s120: { ko: '승객의 무반응 기억' },
  s130: { ko: '최초 회사 설명' },
  s140: { ko: '원본 레코더와 사후 보고' },
  s150: { ko: '첫 공개심리' },
  s160: { ko: '사라진 시간의 정체' },
  s170: { ko: '조사 범위 확대' },
  s200: { ko: '실행 경로 조사 허브' },
  s210: { ko: '제동을 제한한 프로그램' },
  s220: { ko: '뒤차를 찾는 두 카메라' },
  s230: { ko: '임시 수리의 유효기간' },
  s240: { ko: '닫혀 있어야 했던 배차' },
  s300: { ko: '운영팀 조사' },
  s310: { ko: '09시 31분의 승인' },
  s320: { ko: '사고가 없었던 46대' },
  s330: { ko: '행위의 인정' },
  s340: { ko: '범인 하나로 끝낼 것인가' },
  s400: { ko: '개발·출시 조사 허브' },
  s410: { ko: '21ms와 삭제된 확인' },
  s411: { ko: '조건부 승인' },
  s420: { ko: '중복이라고 생각한 검사' },
  s430: { ko: '시험 수의 그늘' },
  s440: { ko: '종이가 아니라 차량에 배포됐다' },
  s450: { ko: '이미 전달된 경고' },
  s460: { ko: '최종 검증 계획' },
  s500: { ko: '최종 준비 허브' },
  s510: { ko: '두 겹의 안전장치' },
  s520: { ko: '기억을 바로잡는 일' },
  s530: { ko: '평범한 귀갓길' },
  s540: { ko: '삭제된 한 줄' },
  s550: { ko: '최종 쟁점 정리' },
  s600: { ko: '최종 공개심리 개회' },
  s610: { ko: '사고 인과판' },
  s620: { ko: '알고도 유지한 결정' },
  s630: { ko: '사고 뒤의 설명' },
  s640: { ko: '최종 보고서 작성' },
  s650: { ko: '1.8초 전의 명령' },
  s700: { ko: '누구의 책임인가' },
  s710: { ko: '사건 기록과 종료' },
}

export function sceneLabel(id: string | null | undefined, locale: Locale = 'ko'): string {
  if (!id) return ''
  const entry = SCENE_LABELS[id]
  if (!entry) return id
  return entry[locale] ?? entry.ko
}
