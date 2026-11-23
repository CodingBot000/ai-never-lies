export function itemLabel(id: string): string {
  const labels: Record<string, string> = {
    unsafe_brake_limiting: '결함 후방 입력이 긴급제동 제한에 사용됨',
    radar_fault: '없는 객체를 보고한 센서 결함',
    override_after_warning: '위험 보고 확인 후 차량 운행중지 해제',
    health_check_removed: '제동 경계에서 건강정보 확인 경로 제거',
    release_conditions_open: '조건부 승인 사항을 완료하지 않은 배포',
    conflicting_kpi: '안전 운휴와 충돌한 가동률·비용 관리',
    known_risk_continued: '결합 위험과 중지 권고 인지 후 운영 유지',
    misleading_public_summary: '사후 재분석을 당시 판단처럼 제시한 공개',
    no_intent_or_criminal_verdict: '고의·형사 죄명을 확정하지 않고 사실과 증거를 이관',
    no_one_responsible: '시스템 문제이므로 아무의 책임도 없음',
    all_ai_innocent: '모든 AI가 항상 올바르고 안전함',
    intentional_murder_proven: '회사가 피해자를 의도적으로 살해함',
    publicity_caused_collision: '사후 발표 편집이 사고의 물리 원인임',
  }
  return labels[id] ?? '보고서 항목'
}
