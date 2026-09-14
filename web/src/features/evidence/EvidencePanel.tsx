import { useMemo, useState } from 'react'
import type { GameSessionApi } from '../../app/useGameSession'
import type { EvidenceDefinitionDto } from '../../core/content/dto'
import { EvidenceImage } from '../../shared/assets/EvidenceImage'
import { assetRegions } from '../../shared/assets/assetCatalog'
import { Modal } from '../../shared/components/Modal'
import './evidence.css'

interface EvidencePanelProps {
  api: GameSessionApi
  onClose: () => void
}

const KIND_TABS = ['all', 'document', 'video', 'log', 'personal'] as const

const KIND_LABEL: Record<string, string> = {
  all: 'ui.filter_all',
  document: 'ui.evidence_kind_document',
  video: 'ui.evidence_kind_video',
  log: 'ui.evidence_kind_log',
  personal: 'ui.evidence_kind_personal',
}

export function EvidencePanel({ api, onClose }: EvidencePanelProps) {
  const { session, index, run } = api
  const [filter, setFilter] = useState<(typeof KIND_TABS)[number]>('all')
  const [selectedId, setSelectedId] = useState<string>('')
  const [tab, setTab] = useState<'summary' | 'original'>('summary')
  if (!session || !index) return null
  const state = session.state

  const owned = useMemo(() => {
    return state.ownedEvidence
      .map((id) => index.evidenceDef(id))
      .filter((item): item is EvidenceDefinitionDto => item !== null)
      .filter((item) => filter === 'all' || item.kind === filter)
  }, [state.ownedEvidence, index, filter, api.revision])

  const selected = selectedId
    ? index.evidenceDef(selectedId)
    : owned.length > 0
      ? (owned[0] ?? null)
      : null

  const selectEvidence = (evidence: EvidenceDefinitionDto): void => {
    setSelectedId(evidence.id)
    setTab('summary')
    if (!state.hasRead(evidence.id)) {
      run('OpenEvidence', { targetId: evidence.id })
    }
  }

  return (
    <Modal title={index.ui('ui.evidence_book')} onClose={onClose} wide>
      <div className="anl-evidence">
        <div className="anl-evidence__list">
          <p className="anl-evidence__meta">{index.ui('ui.evidence_owned_only')}</p>
          <div className="anl-evidence__filters" role="group" aria-label="증거 종류">
            {KIND_TABS.map((kind) => (
              <button
                key={kind}
                type="button"
                className={`anl-tab${filter === kind ? ' anl-tab--active' : ''}`}
                onClick={() => setFilter(kind)}
              >
                {index.ui(KIND_LABEL[kind] ?? kind)}
              </button>
            ))}
          </div>
          {owned.length === 0 ? <p>{index.ui('ui.evidence_none')}</p> : null}
          <ul className="anl-evidence__items">
            {owned.map((evidence) => (
              <li key={evidence.id}>
                <button
                  type="button"
                  className={`anl-evidence__item${selected?.id === evidence.id ? ' anl-evidence__item--active' : ''}`}
                  onClick={() => selectEvidence(evidence)}
                >
                  <span>{evidence.title}</span>
                  <span className="anl-evidence__item-tags">
                    {!state.hasRead(evidence.id) ? <em>{index.ui('ui.evidence_unread')}</em> : null}
                    {evidence.kind === 'video' ? <span>{index.ui('ui.evidence_kind_video')}</span> : null}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
        <div className="anl-evidence__detail">
          {selected ? (
            <>
              <h3 className="anl-evidence__title">{selected.title}</h3>
              <div className="anl-evidence__tabs" role="group" aria-label="보기 전환">
                <button
                  type="button"
                  className={`anl-tab${tab === 'summary' ? ' anl-tab--active' : ''}`}
                  onClick={() => setTab('summary')}
                >
                  {index.ui('ui.evidence_summary')}
                </button>
                <button
                  type="button"
                  className={`anl-tab${tab === 'original' ? ' anl-tab--active' : ''}`}
                  onClick={() => setTab('original')}
                >
                  {index.ui('ui.evidence_original')}
                </button>
              </div>
              {selected.imageAssetId ? (
                <div className="anl-evidence__image">
                  <EvidenceImage
                    assetId={selected.imageAssetId}
                    alt={selected.title}
                    regions={assetRegions(selected.imageAssetId)}
                  />
                  {assetRegions(selected.imageAssetId).length > 0 ? (
                    <p className="anl-evidence__note">{index.ui('ui.frames_note')}</p>
                  ) : null}
                </div>
              ) : null}
              {selected.id === 'ev35' ? (
                <div className="anl-evidence__image">
                  <p className="anl-evidence__meta">메시지</p>
                  <EvidenceImage assetId="ev_phone_base" alt="임도현의 마지막 메시지" showRegions={false} />
                </div>
              ) : null}
              <dl className="anl-evidence__facts">
                <dt>{index.ui('ui.evidence_source')}</dt>
                <dd>{selected.source}</dd>
                {tab === 'original' ? (
                  <>
                    <dt>{index.ui('ui.evidence_original')}</dt>
                    <dd className="anl-evidence__body">{selected.body}</dd>
                  </>
                ) : (
                  <>
                    <dt>{index.ui('ui.evidence_proves')}</dt>
                    <dd>{selected.proves}</dd>
                  </>
                )}
                <dt>{index.ui('ui.evidence_limits')}</dt>
                <dd>{selected.limits}</dd>
              </dl>
              <p className="anl-evidence__meta">
                {state.hasRead(selected.id) ? index.ui('ui.evidence_read') : index.ui('ui.evidence_unread')} ·{' '}
                {selected.sourceMetadata}
              </p>
            </>
          ) : (
            <p>{index.ui('ui.evidence_none')}</p>
          )}
        </div>
      </div>
    </Modal>
  )
}
