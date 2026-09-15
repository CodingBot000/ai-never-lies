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
                  {thumbEntry ? <span className="anl-hub__card-note">{screenTypeLabel(child.screenType)}</span> : null}
                </span>
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
