import type { ScreenProps } from '../types'
import { allConditionsMet } from '../../core/conditions/conditionEvaluator'
import { assetUrl, getAsset } from '../../shared/assets/assetCatalog'
import { sceneLabel, screenTypeLabel } from '../../shared/i18n/sceneLabels'
import './hub.css'

export function HubScreen({ api, feedback }: ScreenProps) {
  const { session, index, run } = api
  if (!session || !index) return null
  const state = session.state
  const scene = index.scene(state.currentSceneId)
  if (!scene) return null

  const required = scene.childSceneIds ?? []
  const optional = scene.optionalChildSceneIds ?? []
  const done = required.filter((id) => state.isSceneCompleted(id)).length
  const transition = session.dispatcher.transitionAvailable()

  const cards = [
    ...required.map((id) => ({ id, optional: false })),
    ...optional.map((id) => ({ id, optional: true })),
  ]

  const reasonText = transition.available
    ? ''
    : done >= required.length && required.length > 0
      ? index.ui('ui.hub_all_required_done')
      : transition.reason.startsWith('remaining:')
        ? index.ui('ui.hub_locked_reason')
        : transition.reason

  return (
    <section className="anl-hub" aria-label={`조사 허브 ${scene.id}`}>
      <header className="anl-hub__header">
        <div>
          <h1 className="anl-hub__title">{sceneLabel(scene.id)} · Act {scene.act}</h1>
          <p className="anl-hub__progress">
            필수 {done}/{required.length} 완료
          </p>
        </div>
        <div className="anl-hub__next">
          <button
            type="button"
            className="anl-button anl-button--primary"
            data-action="continue-hub"
            disabled={!transition.available}
            onClick={() => run('ContinueScene')}
          >
            {index.ui('ui.continue_scene')}
          </button>
          {reasonText ? <p className="anl-hub__reason">{reasonText}</p> : null}
        </div>
      </header>
      <ul className="anl-hub__cards">
        {cards.map(({ id, optional: isOptional }) => {
          const child = index.scene(id)
          if (!child) return null
          const completed = state.isSceneCompleted(id)
          const visited = state.isSceneVisited(id)
          const locked = !completed && !allConditionsMet(child.entryConditions, state, index)
          const thumb = assetUrl(child.backgroundAssetId)
          const thumbEntry = getAsset(child.backgroundAssetId)
          return (
            <li key={id}>
              <button
                type="button"
                className={`anl-hub__card${completed ? ' anl-hub__card--done' : ''}`}
                data-scene-id={id}
                data-optional={isOptional ? 'true' : 'false'}
                data-completed={completed ? 'true' : 'false'}
                disabled={locked}
                onClick={() => {
                  const result = run('EnterScene', { targetId: id })
                  if (result?.status === 'Rejected') feedback(result.message || index.ui('ui.hub_locked_reason'))
                }}
              >
                <span className="anl-hub__thumb">
                  {thumb ? <img src={thumb} alt="" /> : <span className="anl-hub__thumb-empty" />}
                </span>
                <span className="anl-hub__card-body">
                  <strong>{sceneLabel(id)}</strong>
                  <span className="anl-hub__card-tags">
                    {completed ? (
                      <em className="anl-tag anl-tag--done">{index.ui('ui.hub_done')}</em>
                    ) : locked ? (
                      <em className="anl-tag anl-tag--locked">{index.ui('ui.hub_locked')}</em>
                    ) : (
                      <em className="anl-tag">{index.ui('ui.hub_visit')}</em>
                    )}
                    {isOptional ? <em className="anl-tag anl-tag--optional">{index.ui('ui.hub_optional')}</em> : null}
                    {visited && !completed ? <em className="anl-tag">{index.ui('ui.hub_visit')}</em> : null}
                  </span>
                  <span className="anl-hub__card-note">{investigationQuestion(id, state.ownedEvidence) ||
                    (thumbEntry ? screenTypeLabel(child.screenType) : '')}</span>
                </span>
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}

function investigationQuestion(id: string, evidence: string[]): string {
  switch (id) {
    case 's110': return '사고 지점에서 어떤 기록을 찾을 수 있을까?'
    case 's120': return '승객은 어떤 반응을 기억하는가?'
    case 's130': return evidence.includes('ev05') ? '회사는 차내 경고를 어떻게 설명하는가?' : '회사의 발표 수치는 어디서 나왔는가?'
    case 's210': return evidence.includes('ev13') ? '영상과 다른 후방 입력이 어디에서 쓰였는가?' : '제동 요청 강도는 어디에서 바뀌었는가?'
    case 's220': return evidence.includes('ev10') ? '제동을 제한한 후방 입력이 영상에서도 보이는가?' : '독립 기록에서도 뒤차가 보이는가?'
    case 's230': return '어떤 조건에서 운행이 허용됐는가?'
    case 's310': return '담당자는 무엇을 알고 운행을 허용했는가?'
    case 's320': return '다른 차량도 같은 위험 조건에 있었는가?'
    case 's330': return '진술에서 어떤 행위를 확인할 수 있는가?'
    case 's410': return '두 버전 사이에 어떤 검사가 달라졌는가?'
    case 's411': return '승인에는 어떤 조건이 붙어 있었는가?'
    case 's420': return '변경 당시의 가정은 실제로 지켜졌는가?'
    case 's430': return '시험은 사고 당시의 조합을 포함했는가?'
    default: return ''
  }
}
