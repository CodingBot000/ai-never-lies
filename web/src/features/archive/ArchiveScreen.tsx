import type { ScreenProps } from '../types'

export function ArchiveScreen({ api }: ScreenProps) {
  const { session, index } = api
  if (!session || !index) return null
  const state = session.state
  const report = state.getDraft('p20', false)
  const completed = state.isSceneCompleted('s710')

  if (!completed) {
    return (
      <div className="anl-archive">
        <h1>{index.ui('ui.archive_title')}</h1>
        <p>{index.ui('ui.archive_locked')}</p>
      </div>
    )
  }

  const placements = report?.reportPlacements ?? []
  const evidence = report?.reportEvidence ?? []

  return (
    <div className="anl-archive">
      <h1>{index.ui('ui.archive_title')}</h1>
      <p>{index.ui('ui.report_final')}</p>
      <section>
        <h2>{index.ui('ui.report_direct')} · {index.ui('ui.report_contributions')} · {index.ui('ui.report_organizational')} · {index.ui('ui.report_post')}</h2>
        <ul>
          {placements
            .filter((item) => item.section !== 'limit')
            .map((item) => (
              <li key={item.item}>
                [{sectionLabel(index, item.section)}] {item.item}
                {evidence.find((entry) => entry.item === item.item) ? (
                  <span className="anl-muted">
                    {' '}
                    · 근거{' '}
                    {(evidence.find((entry) => entry.item === item.item)?.evidenceIds ?? [])
                      .map((id) => index.evidenceDef(id)?.title ?? id)
                      .join(', ')}
                  </span>
                ) : null}
              </li>
            ))}
        </ul>
      </section>
      <section>
        <h2>{index.ui('ui.archive_responsibility')}</h2>
        <ul>
          {index.graphNodes().map((node) => (
            <li key={node.id}>
              {node.label}
              {node.evidenceIds.length > 0 ? (
                <span className="anl-muted"> · {node.evidenceIds.map((id) => index.evidenceDef(id)?.title ?? id).join(', ')}</span>
              ) : null}
            </li>
          ))}
        </ul>
      </section>
      <section>
        <h2>{index.ui('ui.archive_prevention')}</h2>
        <p>{state.getBranchValue('ending_tone') === 'prevention_first' ? '예방 우선 결론으로 마무리됨' : '사실 확정 우선 결론으로 마무리됨'}</p>
      </section>
      <section>
        <h2>{index.ui('ui.archive_unresolved')}</h2>
        <ul>
          {placements
            .filter((item) => item.section === 'limit')
            .map((item) => (
              <li key={item.item}>{item.item}</li>
            ))}
        </ul>
      </section>
      <section>
        <h2>{index.ui('ui.archive_sources')}</h2>
        <ul>
          {state.ownedEvidence.map((id) => {
            const evidenceDef = index.evidenceDef(id)
            return <li key={id}>{evidenceDef?.title ?? id} — {evidenceDef?.source ?? ''}</li>
          })}
        </ul>
      </section>
    </div>
  )
}

function sectionLabel(index: NonNullable<ScreenProps['api']['index']>, section: string): string {
  const labels: Record<string, string> = {
    direct: index.ui('ui.report_direct'),
    contribution: index.ui('ui.report_contributions'),
    organizational: index.ui('ui.report_organizational'),
    post: index.ui('ui.report_post'),
    limit: index.ui('ui.report_limit'),
  }
  return labels[section] ?? section
}
