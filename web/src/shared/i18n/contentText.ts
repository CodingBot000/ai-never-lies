/**
 * 콘텐츠 정본 문자열(장면 팁 등)에 섞인 내부 ID를 표시용 이름으로 치환한다.
 * p01→퍼즐 이름, ev06→증거 제목, s150→장면 이름, st…→증언 N, dlg_…→삭제.
 */
import type { ContentIndex } from '../../core/content/contentIndex'
import { sceneLabel } from './sceneLabels'
import { puzzleLabel } from './puzzleLabels'

export function formatContentText(text: string | null | undefined, index: ContentIndex): string {
  if (!text) return ''
  const testimonyNumber = (id: string): number => {
    const statement = index.testimony(id)
    if (!statement) return 0
    const siblings = index.sceneTestimonies(statement.sceneId)
    return siblings.findIndex((item) => item.id === id) + 1
  }
  return text
    .replace(/\bp\d{2}\b/g, (id) => puzzleLabel(id))
    .replace(/\bev\d{2}\b/g, (id) => index.evidenceDef(id)?.title ?? id)
    .replace(/\bs\d{3}\b/g, (id) => sceneLabel(id))
    .replace(/\bst\d+_\d+\b/g, (id) => {
      const number = testimonyNumber(id)
      return number > 0 ? `증언 ${number}` : id
    })
    .replace(/\bdlg_[a-z0-9_]+\b/g, '')
    .replace(/\s{2,}/g, ' ')
    .trim()
}
