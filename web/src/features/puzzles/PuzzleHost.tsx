import { useState } from 'react'
import type { ScreenProps } from '../types'
import type { EvidenceDefinitionDto, PuzzleDefinitionDto, PuzzleDraftDto, RegionDto } from '../../core/content/dto'
import type { ContentIndex } from '../../core/content/contentIndex'
import { EvidenceImage } from '../../shared/assets/EvidenceImage'
import { assetRegions } from '../../shared/assets/assetCatalog'
import { Modal } from '../../shared/components/Modal'
import './puzzle.css'

export function PuzzleHost(props: ScreenProps) {
  const { api } = props
  const { session, index } = api
  if (!session || !index) return null
  const state = session.state
  const puzzle = index.puzzle(state.activePuzzleId)
  if (!puzzle) return null

  switch (puzzle.type) {
    case 'hotspot_set':
      return <HotspotWidget {...props} puzzle={puzzle} />
    case 'statement_refutation':
      return <StatementWidget {...props} puzzle={puzzle} index={index} />
    case 'source_mapping':
    case 'trace_mapping':
      return <SlotAssignWidget {...props} puzzle={puzzle} />
    case 'timeline_order':
    case 'chronology':
    case 'deployment_chain':
      return <OrderingWidget {...props} puzzle={puzzle} index={index} />
    case 'cross_camera':
      return <CrossCameraWidget {...props} puzzle={puzzle} index={index} />
    case 'argument_choice':
    case 'diff_select':
    case 'assumption_link':
    case 'matrix_gap':
      return <ChoiceWidget {...props} puzzle={puzzle} />
    case 'counterfactual':
      return <CounterfactualWidget {...props} puzzle={puzzle} />
    case 'causal_graph':
      return <CausalWidget {...props} puzzle={puzzle} index={index} />
    case 'responsibility_matrix':
      return <ReportWidget {...props} puzzle={puzzle} index={index} />
    default:
      return <p>지원하지 않는 퍼즐 형식입니다: {puzzle.type}</p>
  }
}

interface WidgetProps extends ScreenProps {
  puzzle: PuzzleDefinitionDto
}

// ---------------------------------------------------------------- hotspot

function HotspotWidget({ api, puzzle, feedback }: WidgetProps) {
  const { session, index, run } = api
  const [showRegions, setShowRegions] = useState(true)
  const [message, setMessage] = useState('')
  if (!session || !index) return null
  const scene = index.scene(session.state.currentSceneId)
  const bg = scene?.backgroundAssetId ?? ''
  const backgroundRegions = assetRegions(bg)
  const draft = session.state.getDraft(puzzle.id, false)
  const visited = (draft?.placedCards ?? []).filter((card) => card.slot === 'region').map((card) => card.value)
  const regions = puzzle.regions
    .map((region: RegionDto) => {
      const found = backgroundRegions.find((candidate) => candidate.regionId === region.regionId)
      return found ? { ...found } : null
    })
    .filter((region): region is NonNullable<typeof region> => region !== null)

  return (
    <div className="anl-puzzle anl-puzzle--investigation">
      <div className="anl-puzzle__canvas" onClick={() => setMessage(index.ui('ui.region_not_related'))}>
        <EvidenceImage
          assetId={bg}
          alt={puzzle.id}
          regions={regions}
          activeRegionIds={visited}
          showRegions={showRegions}
          onRegionClick={(regionId) => {
            const result = run('InspectRegion', { targetId: regionId })
            if (result?.message) {
              setMessage(result.message)
              feedback(result.message)
            } else {
              setMessage('')
            }
          }}
        />
      </div>
      <div className="anl-puzzle__side">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <label className="anl-check">
          <input type="checkbox" checked={showRegions} onChange={(event) => setShowRegions(event.target.checked)} />
          <span>{index.ui('ui.inspect_toggle')}</span>
        </label>
        <p className="anl-muted">{index.ui('ui.inspect_hint')}</p>
        <ul className="anl-visit-list">
          {regions.map((region) => (
            <li key={region.regionId} className={visited.includes(region.regionId) ? 'is-done' : ''}>
              {region.regionId} {visited.includes(region.regionId) ? '· 확인' : ''}
            </li>
          ))}
        </ul>
        {message ? <p className="anl-feedback">{message}</p> : null}
        <HubReturnButton api={api} />
      </div>
    </div>
  )
}

// ---------------------------------------------------------------- statement

function StatementWidget({ api, puzzle, index, feedback, onTransient }: WidgetProps & { index: ContentIndex }) {
  const { session, run } = api
  const [picker, setPicker] = useState(false)
  if (!session) return null
  const draft = session.state.getDraft(puzzle.id, false)
  const testimonies = index.sceneTestimonies(puzzle.sceneId)

  return (
    <div className="anl-puzzle anl-puzzle--three">
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">{index.ui('ui.statement_select')}</h2>
        <ul className="anl-statement-list">
          {testimonies.map((statement) => {
            const selected = draft?.selectedStatementId === statement.id
            return (
              <li key={statement.id}>
                <button
                  type="button"
                  className={`anl-statement${selected ? ' anl-statement--active' : ''}`}
                  onClick={() => run('SelectStatement', { targetId: statement.id })}
                >
                  <strong>{statement.speaker}</strong>
                  <span>{statement.text}</span>
                </button>
                {selected ? (
                  <button
                    type="button"
                    className="anl-button anl-button--small"
                    onClick={() => {
                      const result = run('PressStatement', { targetId: statement.id })
                      const lines = result?.events.flatMap((event) => event.lines ?? []) ?? []
                      if (lines.length > 0) onTransient(lines)
                    }}
                  >
                    {index.ui('ui.statement_press')}
                  </button>
                ) : null}
              </li>
            )
          })}
        </ul>
      </div>
      <div className="anl-puzzle__col">
        <SelectedSummary api={api} puzzle={puzzle} index={index} />
      </div>
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">{index.ui('ui.evidence')}</h2>
        <SelectedEvidence api={api} puzzle={puzzle} index={index} />
        {puzzle.correctChoiceIds.length > 0 ? (
          <ChoiceButtons api={api} choices={puzzle.correctChoiceIds} />
        ) : null}
        <button
          type="button"
          className="anl-button anl-button--small"
          onClick={() => setPicker(true)}
          disabled={(draft?.selectedEvidenceIds.length ?? 0) >= 3}
        >
          증거 선택
        </button>
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
      {picker ? (
        <EvidencePickerModal
          api={api}
          index={index}
          max={3}
          onClose={() => setPicker(false)}
          onConfirm={(ids) => {
            run('SelectEvidence', { ids })
            setPicker(false)
          }}
        />
      ) : null}
    </div>
  )
}

// ---------------------------------------------------------------- slot assign

function SlotAssignWidget({ api, puzzle, feedback }: WidgetProps) {
  const { session, index, run } = api
  const [selectedSlot, setSelectedSlot] = useState('')
  const [message, setMessage] = useState('')
  if (!session || !index) return null
  const draft = session.state.getDraft(puzzle.id, false)
  const placed = (key: string): string => draft?.placedCards.find((card) => card.slot === key)?.value ?? ''

  const candidates = puzzle.valuePairs.map((pair) => pair.value).filter(Boolean)
  if (puzzle.type === 'trace_mapping') {
    for (const moduleId of ['perception', 'planner', 'brake_supervisor', 'vehicle_controller']) {
      if (!candidates.includes(moduleId)) candidates.push(moduleId)
    }
  }

  return (
    <div className="anl-puzzle anl-puzzle--two">
      <div className="anl-puzzle__col">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <ul className="anl-slot-list">
          {puzzle.valuePairs.map((pair) => (
            <li key={pair.key}>
              <button
                type="button"
                className={`anl-slot${selectedSlot === pair.key ? ' anl-slot--active' : ''}`}
                onClick={() => setSelectedSlot(pair.key)}
              >
                <span className="anl-slot__key">{slotLabel(pair.key)}</span>
                <span className="anl-slot__value">{placed(pair.key) || '-'}</span>
              </button>
            </li>
          ))}
        </ul>
      </div>
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">값 선택</h2>
        <div className="anl-candidate-list">
          {candidates.map((value) => (
            <button
              key={value}
              type="button"
              className={`anl-candidate${Object.values(placedValues(draft)).includes(value) ? ' anl-candidate--placed' : ''}`}
              onClick={() => {
                if (!selectedSlot) {
                  setMessage('슬롯을 먼저 선택하십시오.')
                  return
                }
                setMessage('')
                run('PlaceCard', { slot: selectedSlot, value })
              }}
            >
              {valueLabel(value)}
            </button>
          ))}
        </div>
        {message ? <p className="anl-feedback">{message}</p> : null}
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
    </div>
  )
}

function placedValues(draft: PuzzleDraftDto | null): Record<string, string> {
  const result: Record<string, string> = {}
  for (const card of draft?.placedCards ?? []) result[card.slot] = card.value
  return result
}

// ---------------------------------------------------------------- ordering

function OrderingWidget({ api, puzzle, index, feedback }: WidgetProps & { index: ContentIndex }) {
  const { session, run } = api
  if (!session || !index) return null
  const draft = session.state.getDraft(puzzle.id, false)
  const placed = (draft?.placedCards ?? []).filter((card) => card.slot === 'order').map((card) => card.value)
  const pool = [...puzzle.orderedIds].sort((a, b) => stableOrder(a, b))

  const choiceTitle =
    puzzle.type === 'timeline_order' ? 'ui.timeline_interpret' : puzzle.type === 'chronology' ? '최종 상태' : '결론'

  return (
    <div className="anl-puzzle anl-puzzle--three">
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">{index.ui('ui.causal_cards')}</h2>
        <div className="anl-candidate-list">
          {pool.map((id) => (
            <button
              key={id}
              type="button"
              className={`anl-candidate${placed.includes(id) ? ' anl-candidate--placed' : ''}`}
              onClick={() => run('PlaceCard', { slot: 'order', value: id })}
            >
              {cardLabel(index, id)}
            </button>
          ))}
        </div>
      </div>
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">배치 순서</h2>
        <ol className="anl-order-list">
          {placed.map((id) => (
            <li key={id}>
              <button
                type="button"
                className="anl-order-item"
                onClick={() => run('PlaceCard', { slot: 'order', value: id })}
              >
                {cardLabel(index, id)}
              </button>
            </li>
          ))}
          {placed.length === 0 ? <li className="anl-muted">카드를 클릭해 순서대로 배치하십시오.</li> : null}
        </ol>
        {puzzle.valuePairs.length > 0 ? (
          <dl className="anl-annotation">
            {puzzle.valuePairs.map((pair) => (
              <div key={pair.key}>
                <dt>{annotationLabel(pair.key)}</dt>
                <dd>{pair.value}</dd>
              </div>
            ))}
          </dl>
        ) : null}
      </div>
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">{index.ui(choiceTitle)}</h2>
        <ChoiceButtons api={api} choices={puzzle.correctChoiceIds} />
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
    </div>
  )
}

// ---------------------------------------------------------------- cross camera

function CrossCameraWidget({ api, puzzle, index, feedback }: WidgetProps & { index: ContentIndex }) {
  const { session, run } = api
  const [picker, setPicker] = useState(false)
  if (!session || !index) return null
  const draft = session.state.getDraft(puzzle.id, false)
  const windows = [
    { id: 't_minus_2_to_minus_0_3', label: '충돌 2.0초~0.3초 전' },
    { id: 'before_event', label: '충돌 8~6초 전' },
    { id: 'after_event', label: '충돌 1~3초 후' },
  ]

  return (
    <div className="anl-puzzle anl-puzzle--two">
      <div className="anl-puzzle__col">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <h2 className="anl-puzzle__hint">{index.ui('ui.select_time')}</h2>
        <div className="anl-candidate-list">
          {windows.map((window) => (
            <button
              key={window.id}
              type="button"
              className={`anl-candidate${draft?.comparisonWindowId === window.id ? ' anl-candidate--placed' : ''}`}
              onClick={() => run('SelectComparisonWindow', { targetId: window.id })}
            >
              {window.label}
            </button>
          ))}
        </div>
      </div>
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">{index.ui('ui.compare_window')}</h2>
        <SelectedEvidence api={api} puzzle={puzzle} index={index} />
        <button type="button" className="anl-button anl-button--small" onClick={() => setPicker(true)}>
          {index.ui('ui.compare_add')}
        </button>
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
      {picker ? (
        <EvidencePickerModal
          api={api}
          index={index}
          max={3}
          onClose={() => setPicker(false)}
          onConfirm={(ids) => {
            run('SelectEvidence', { ids })
            setPicker(false)
          }}
        />
      ) : null}
    </div>
  )
}

// ---------------------------------------------------------------- choice

const CHOICE_OPTIONS: Record<string, string[]> = {
  p10: ['safe_by_count', 'all_certainly_unsafe', 'exposure_not_equivalent'],
  p13: ['faulty_vehicle_not_dispatched', 'perfect_ai', 'never_rains', 'human_driver_present'],
  p14: ['all_tests_fake', 'validated_all', 'unvalidated_combination'],
}

function ChoiceWidget({ api, puzzle, feedback }: WidgetProps) {
  const { session, index, run } = api
  const [picker, setPicker] = useState(false)
  if (!session || !index) return null
  const draft = session.state.getDraft(puzzle.id, false)

  if (puzzle.type === 'diff_select') {
    return (
      <div className="anl-puzzle anl-puzzle--diff">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <div className="anl-diff">
          <div>
            <h3>변경 전 v3.4</h3>
            <pre>{`[imminent_override]
if independent_imminent_collision:
    return emergency_request

[health_snapshot_validation]
health = await sensor_health_snapshot
if health is missing or health.valid == false or health.age_ms > 100:
    return emergency_request

[rear_risk_arbitration]
if rear_risk_score > 70:
    return limited_request(-2.0)
return emergency_request`}</pre>
          </div>
          <div>
            <h3>변경 후 v3.5</h3>
            <pre>{`[imminent_override]
if independent_imminent_collision:
    return emergency_request

[health_snapshot_validation]
// 제거됨: 운행 진단이 결함차 배차를 막는다는 전제로 대체

[rear_risk_arbitration]
if rear_risk_score > 70:
    return limited_request(-2.0)
return emergency_request`}</pre>
          </div>
        </div>
        <div className="anl-diff__actions">
          <button
            type="button"
            className={`anl-candidate${(draft?.choiceIds ?? []).includes('health_snapshot_validation') ? ' anl-candidate--placed' : ''}`}
            onClick={() => run('SelectChoice', { targetId: 'health_snapshot_validation' })}
          >
            {choiceLabel('health_snapshot_validation')}
          </button>
          <button
            type="button"
            className={`anl-candidate${(draft?.choiceIds ?? []).includes('scalar_compare_cost_21ms') ? ' anl-candidate--placed' : ''}`}
            onClick={() => run('SelectChoice', { targetId: 'scalar_compare_cost_21ms' })}
          >
            {choiceLabel('scalar_compare_cost_21ms')}
          </button>
          <button
            type="button"
            className={`anl-candidate${(draft?.choiceIds ?? []).includes('snapshot_sync_cost') ? ' anl-candidate--placed' : ''}`}
            onClick={() => run('SelectChoice', { targetId: 'snapshot_sync_cost' })}
          >
            {choiceLabel('snapshot_sync_cost')}
          </button>
        </div>
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
    )
  }

  if (puzzle.type === 'matrix_gap') {
    const matrix = index.matrix
    const dims: { key: string; options: string[] }[] = [
      { key: 'sensor', options: matrix.dimensions.sensor },
      { key: 'overrideState', options: matrix.dimensions.overrideState },
      { key: 'version', options: matrix.dimensions.version },
      { key: 'condition', options: matrix.dimensions.condition },
    ]
    return (
      <div className="anl-puzzle anl-puzzle--matrix">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <div className="anl-matrix__dims">
          {dims.map((dim) => (
            <div key={dim.key} className="anl-matrix__row">
              <span className="anl-matrix__label">{optionLabel(dim.key)}</span>
              <div className="anl-matrix__options">
                {dim.options.map((option) => (
                  <button
                    key={option}
                    type="button"
                    className={`anl-candidate${placedValues(draft)[dim.key] === option ? ' anl-candidate--placed' : ''}`}
                    onClick={() => run('PlaceCard', { slot: dim.key, value: option })}
                  >
                    {optionLabel(option)}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
        <div className="anl-matrix__table">
          <table>
            <thead>
              <tr>
                <th>센서</th>
                <th>수동 해제</th>
                <th>버전</th>
                <th>조건</th>
                <th>검증</th>
              </tr>
            </thead>
            <tbody>
              {matrix.rows.map((row) => {
                const match =
                  placedValues(draft).sensor === row.sensor &&
                  placedValues(draft).overrideState === row.overrideState &&
                  placedValues(draft).version === row.version &&
                  placedValues(draft).condition === row.condition
                return (
                  <tr key={row.index} className={match ? 'is-match' : ''}>
                    <td>{optionLabel(row.sensor)}</td>
                    <td>{optionLabel(row.overrideState)}</td>
                    <td>{optionLabel(row.version)}</td>
                    <td>{optionLabel(row.condition)}</td>
                    <td>{row.verification} · {row.note}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          <p className="anl-muted">
            {index.ui('ui.matrix_total')}: {puzzle.valuePairs[0]?.value ?? '-'}
          </p>
        </div>
        <ChoiceButtons api={api} choices={CHOICE_OPTIONS.p14 ?? []} />
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
    )
  }

  const options = CHOICE_OPTIONS[puzzle.id] ?? puzzle.correctChoiceIds
  return (
    <div className="anl-puzzle anl-puzzle--two">
      <div className="anl-puzzle__col">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <ChoiceButtons api={api} choices={options} />
        {puzzle.type === 'assumption_link' ? (
          <>
            <SelectedEvidence api={api} puzzle={puzzle} index={index} />
            <button type="button" className="anl-button anl-button--small" onClick={() => setPicker(true)}>
              반증 자료 선택
            </button>
          </>
        ) : null}
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
      {picker ? (
        <EvidencePickerModal
          api={api}
          index={index}
          max={3}
          onClose={() => setPicker(false)}
          onConfirm={(ids) => {
            run('SelectEvidence', { ids })
            setPicker(false)
          }}
        />
      ) : null}
    </div>
  )
}

// ---------------------------------------------------------------- counterfactual

function CounterfactualWidget({ api, puzzle, feedback }: WidgetProps) {
  const { session, index, run } = api
  if (!session || !index) return null
  const draft = session.state.getDraft(puzzle.id, false)
  const ran = draft?.ranCaseIds ?? []
  const caseIds = puzzle.requiredCaseIds
  const results = puzzle.valuePairs

  return (
    <div className="anl-puzzle anl-puzzle--two">
      <div className="anl-puzzle__col">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <h2 className="anl-puzzle__hint">재구성 사례</h2>
        <div className="anl-candidate-list">
          {caseIds.map((caseId) => (
            <button
              key={caseId}
              type="button"
              className={`anl-candidate${ran.includes(caseId) ? ' anl-candidate--placed' : ''}`}
              onClick={() => run('RunCounterfactualCase', { targetId: caseId })}
            >
              {caseLabel(caseId)} {ran.includes(caseId) ? '· 실행됨' : ''}
            </button>
          ))}
        </div>
      </div>
      <div className="anl-puzzle__col">
        <h2 className="anl-puzzle__hint">결과</h2>
        <table className="anl-result-table">
          <tbody>
            {results.map((pair) => (
              <tr key={pair.key}>
                <th>{caseLabel(pair.key)}</th>
                <td>
                  {ran.includes(pair.key) ? masterLabel(pair.value) : '미실행'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className="anl-muted">{index.ui('ui.counterfactual_note')}</p>
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
    </div>
  )
}

// ---------------------------------------------------------------- causal

function CausalWidget({ api, puzzle, index, feedback }: WidgetProps & { index: ContentIndex }) {
  const { session, run } = api
  const [connectFrom, setConnectFrom] = useState('')
  const [mode, setMode] = useState<'causal' | 'signal'>('causal')
  const [proofEdge, setProofEdge] = useState('')
  const [picker, setPicker] = useState(false)
  if (!session || !index) return null
  const draft = session.state.getDraft(puzzle.id, false)
  const edges = draft?.edges ?? []
  const nodes = index.graphNodes()
  const nodeLabel = (id: string): string => nodes.find((node) => node.id === id)?.label ?? id

  return (
    <div className="anl-puzzle anl-puzzle--causal">
      <div className="anl-puzzle__col anl-puzzle__col--wide">
        <PuzzleHeader api={api} puzzle={puzzle} />
        <h2 className="anl-puzzle__hint">{index.ui('ui.causal_slots')}</h2>
        <div className="anl-edge-actions" role="group" aria-label="연결 종류">
          <button
            type="button"
            className={`anl-candidate${mode === 'causal' ? ' anl-candidate--placed' : ''}`}
            onClick={() => setMode('causal')}
          >
            인과선
          </button>
          <button
            type="button"
            className={`anl-candidate${mode === 'signal' ? ' anl-candidate--placed' : ''}`}
            onClick={() => setMode('signal')}
          >
            {index.ui('ui.causal_signal')}
          </button>
        </div>
        <p className="anl-muted">
          {connectFrom ? `${nodeLabel(connectFrom)} → 다음 노드를 선택하십시오.` : '시작 노드를 선택하십시오.'}
        </p>
        <div className="anl-node-grid">
          {nodes.map((node) => (
            <button
              key={node.id}
              type="button"
              className={`anl-node${connectFrom === node.id ? ' anl-node--active' : ''}`}
              onClick={() => {
                if (!connectFrom) {
                  setConnectFrom(node.id)
                  return
                }
                if (connectFrom === node.id) {
                  setConnectFrom('')
                  return
                }
                run('ConnectNodes', { targetId: mode, ids: [connectFrom, node.id] })
                setConnectFrom('')
              }}
            >
              <span>{node.label}</span>
              <small>{node.kind}</small>
            </button>
          ))}
        </div>
        {connectFrom ? (
          <button type="button" className="anl-button anl-button--small" onClick={() => setConnectFrom('')}>
            선택 해제
          </button>
        ) : null}
        <ul className="anl-edge-list">
          {edges.map((edge) => {
            const key = `${edge.from}|${edge.to}|${edge.kind}`
            return (
              <li key={key} className={proofEdge === key ? 'is-active' : ''}>
                <button type="button" className="anl-edge" onClick={() => setProofEdge(key)}>
                  <span>
                    {nodeLabel(edge.from)} → {nodeLabel(edge.to)} ({edge.kind})
                  </span>
                  <small>
                    근거{' '}
                    {draft?.edgeProofs.find((proof) => `${proof.from}|${proof.to}|${proof.kind}` === key)?.evidenceIds
                      .length ?? 0}
                    개
                  </small>
                </button>
                <button
                  type="button"
                  className="anl-button anl-button--small"
                  onClick={() => run('RemoveEdge', { ids: [edge.from, edge.to] })}
                >
                  {index.ui('ui.causal_remove')}
                </button>
              </li>
            )
          })}
        </ul>
        <div className="anl-edge-actions">
          <button
            type="button"
            className="anl-button anl-button--small"
            disabled={!proofEdge}
            onClick={() => setPicker(true)}
          >
            {index.ui('ui.causal_proof')}
          </button>
        </div>
        <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      </div>
      {picker && proofEdge ? (
        <EvidencePickerModal
          api={api}
          index={index}
          max={3}
          onClose={() => setPicker(false)}
          onConfirm={(ids) => {
            run('SelectEvidence', { targetId: proofEdge, ids })
            setPicker(false)
          }}
        />
      ) : null}
    </div>
  )
}

// ---------------------------------------------------------------- report

function ReportWidget({ api, puzzle, index, feedback }: WidgetProps & { index: ContentIndex }) {
  const { session, run } = api
  const [assignItem, setAssignItem] = useState('')
  const [proofItem, setProofItem] = useState('')
  if (!session || !index) return null
  const draft = session.state.getDraft(puzzle.id, false)
  const placements = new Map((draft?.reportPlacements ?? []).map((item) => [item.item, item.section]))
  const sections = [
    { id: 'direct', label: index.ui('ui.report_direct') },
    { id: 'contribution', label: index.ui('ui.report_contributions') },
    { id: 'organizational', label: index.ui('ui.report_organizational') },
    { id: 'post', label: index.ui('ui.report_post') },
    { id: 'limit', label: index.ui('ui.report_limit') },
  ]
  const items = [
    ...puzzle.reportProofRequirements.map((proof) => proof.item),
    'no_intent_or_criminal_verdict',
    'no_one_responsible',
    'all_ai_innocent',
    'intentional_murder_proven',
    'publicity_caused_collision',
  ]

  return (
    <div className="anl-puzzle anl-puzzle--report">
      <PuzzleHeader api={api} puzzle={puzzle} />
      <div className="anl-report__sections">
        {sections.map((section) => (
          <div key={section.id} className="anl-report__section">
            <h3>{section.label}</h3>
            {items
              .filter((item) => placements.get(item) === section.id)
              .map((item) => (
                <button
                  key={item}
                  type="button"
                  className="anl-candidate anl-candidate--placed"
                  onClick={() => run('PlaceCard', { slot: section.id, value: item })}
                >
                  {itemLabel(item)}
                </button>
              ))}
          </div>
        ))}
      </div>
      <div className="anl-report__palette">
        <h3>{index.ui('ui.report_palette')}</h3>
        <div className="anl-candidate-list">
          {items.map((item) => (
            <button
              key={item}
              type="button"
              className={`anl-candidate${placements.has(item) ? ' anl-candidate--placed' : ''}`}
              onClick={() => setAssignItem(item)}
            >
              {itemLabel(item)}
            </button>
          ))}
        </div>
      </div>
      {assignItem ? (
        <div className="anl-report__assign" role="group" aria-label="구역 선택">
          <p>{itemLabel(assignItem)}</p>
          <div className="anl-candidate-list">
            {sections.map((section) => (
              <button
                key={section.id}
                type="button"
                className="anl-candidate"
                onClick={() => {
                  run('PlaceCard', { slot: section.id, value: assignItem })
                  setAssignItem('')
                }}
              >
                {section.label}
              </button>
            ))}
            <button type="button" className="anl-button anl-button--small" onClick={() => setAssignItem('')}>
              닫기
            </button>
          </div>
        </div>
      ) : null}
      <div className="anl-report__proofs">
        <h3>{index.ui('ui.report_attach')}</h3>
        <p className="anl-muted">{index.ui('ui.report_evidence_max3')}</p>
        {puzzle.reportProofRequirements.map((proof) => {
          const ids = draft?.reportEvidence.find((entry) => entry.item === proof.item)?.evidenceIds ?? []
          return (
            <div key={proof.item} className="anl-report__proof">
              <span>
                {itemLabel(proof.item)} · 근거 {ids.length}/3
              </span>
              <button type="button" className="anl-button anl-button--small" onClick={() => setProofItem(proof.item)}>
                근거 선택
              </button>
            </div>
          )
        })}
      </div>
      <SubmitButton api={api} feedback={feedback} puzzle={puzzle} />
      {proofItem ? (
        <EvidencePickerModal
          api={api}
          index={index}
          max={3}
          onClose={() => setProofItem('')}
          onConfirm={(ids) => {
            run('SelectEvidence', { targetId: proofItem, ids })
            setProofItem('')
          }}
        />
      ) : null}
    </div>
  )
}

// ---------------------------------------------------------------- shared

export function PuzzleHeader({ api, puzzle }: { api: ScreenProps['api']; puzzle: PuzzleDefinitionDto }) {
  const { session, index } = api
  if (!session || !index) return null
  const scene = index.scene(session.state.currentSceneId)
  return (
    <header className="anl-puzzle__header">
      <h1>
        {scene?.id} · {typeLabel(puzzle.type)}
      </h1>
      {scene?.tipText ? <p className="anl-puzzle__tip">{scene.tipText}</p> : null}
    </header>
  )
}

function ChoiceButtons({ api, choices }: { api: ScreenProps['api']; choices: string[] }) {
  const { session, run } = api
  if (!session) return null
  const draft = session.state.activePuzzleId
    ? session.state.getDraft(session.state.activePuzzleId, false)
    : null
  return (
    <div className="anl-candidate-list">
      {choices.map((choiceId) => {
        const selected = (draft?.choiceIds ?? []).includes(choiceId)
        return (
          <button
            key={choiceId}
            type="button"
            className={`anl-candidate${selected ? ' anl-candidate--placed' : ''}`}
            onClick={() => run('SelectChoice', { targetId: choiceId })}
          >
            {choiceLabel(choiceId)}
          </button>
        )
      })}
    </div>
  )
}

function SelectedEvidence({ api, puzzle, index }: { api: ScreenProps['api']; puzzle: PuzzleDefinitionDto; index: ContentIndex }) {
  const { session } = api
  const draft = session?.state.getDraft(puzzle.id, false)
  const ids = draft?.selectedEvidenceIds ?? []
  return (
    <ul className="anl-selected-evidence">
      {ids.length === 0 ? <li className="anl-muted">{index.ui('ui.statement_need_selection')}</li> : null}
      {ids.map((id) => (
        <li key={id}>{index.evidenceDef(id)?.title ?? id}</li>
      ))}
    </ul>
  )
}

function SelectedSummary({ api, puzzle, index }: { api: ScreenProps['api']; puzzle: PuzzleDefinitionDto; index: ContentIndex }) {
  const { session } = api
  const statementId = session?.state.getDraft(puzzle.id, false)?.selectedStatementId ?? ''
  const statement = index.testimony(statementId)
  return (
    <div className="anl-selected-summary">
      {statement ? (
        <>
          <strong>{statement.speaker}</strong>
          <p>{statement.text}</p>
        </>
      ) : (
        <p className="anl-muted">{index.ui('ui.statement_select')}</p>
      )}
    </div>
  )
}

function SubmitButton({
  api,
  feedback,
  puzzle,
}: {
  api: ScreenProps['api']
  feedback: (message: string) => void
  puzzle: PuzzleDefinitionDto
}) {
  const { session, index, run } = api
  if (!session || !index) return null
  const command = puzzle.type === 'statement_refutation' ? 'PresentEvidence' : 'SubmitPuzzle'
  return (
    <div className="anl-puzzle__submit">
      <button
        type="button"
        className="anl-button anl-button--primary"
        onClick={() => {
          const result = run(command)
          if (result) {
            const message = result.message || result.reasonCode
            feedback(message)
          }
        }}
      >
        {index.ui(puzzle.type === 'statement_refutation' ? 'ui.statement_present' : 'ui.submit')}
      </button>
    </div>
  )
}

function HubReturnButton({ api }: { api: ScreenProps['api'] }) {
  const { session, index, run } = api
  if (!session || !index) return null
  const state = session.state
  const scene = index.scene(state.currentSceneId)
  const parent = scene
    ? [...index.scenes.values()].find((candidate) => candidate.childSceneIds?.includes(scene.id))?.id
    : undefined
  if (!parent) return null
  return (
    <button type="button" className="anl-button anl-button--small" onClick={() => run('ReturnToHub')}>
      {index.ui('ui.return_to_hub')}
    </button>
  )
}

export function EvidencePickerModal({
  api,
  index,
  max,
  onClose,
  onConfirm,
}: {
  api: ScreenProps['api']
  index: ContentIndex
  max: number
  onClose: () => void
  onConfirm: (ids: string[]) => void
}) {
  const { session } = api
  const [selected, setSelected] = useState<string[]>([])
  if (!session) return null
  const owned = session.state.ownedEvidence
    .map((id) => index.evidenceDef(id))
    .filter((item): item is EvidenceDefinitionDto => item !== null)

  return (
    <Modal title="제시할 증거" onClose={onClose}>
      <p className="anl-muted">{index.ui('ui.select_up_to_3')}</p>
      <ul className="anl-picker-list">
        {owned.map((evidence) => {
          const checked = selected.includes(evidence.id)
          return (
            <li key={evidence.id}>
              <label className="anl-check">
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={() => {
                    setSelected((previous) => {
                      if (previous.includes(evidence.id)) return previous.filter((id) => id !== evidence.id)
                      if (previous.length >= max) return previous
                      return [...previous, evidence.id]
                    })
                  }}
                />
                <span>{evidence.title}</span>
              </label>
            </li>
          )
        })}
      </ul>
      <div className="anl-picker-actions">
        <button type="button" className="anl-button anl-button--primary" onClick={() => onConfirm(selected)}>
          확인
        </button>
        <button type="button" className="anl-button" onClick={() => setSelected([])}>
          {index.ui('ui.clear_selection')}
        </button>
      </div>
    </Modal>
  )
}

// ---------------------------------------------------------------- labels

export function typeLabel(type: string): string {
  const labels: Record<string, string> = {
    hotspot_set: '현장 조사',
    statement_refutation: '증언 반박',
    source_mapping: '출처 매핑',
    timeline_order: '시간 순서',
    trace_mapping: '실행 경로',
    cross_camera: '교차 카메라',
    chronology: '연표',
    argument_choice: '논증 선택',
    diff_select: '변경 비교',
    assumption_link: '전제 연결',
    matrix_gap: '조합 공백',
    deployment_chain: '배포 경로',
    counterfactual: '반사실',
    causal_graph: '인과 그래프',
    responsibility_matrix: '책임 보고서',
  }
  return labels[type] ?? type
}

export function choiceLabel(id: string): string {
  const labels: Record<string, string> = {
    unconditional_approval: '어떤 조건도 없이 승인',
    conditional_approval_unverified: '조건부 승인했지만 이행 확인 미완료',
    never_approved: '끝까지 승인하지 않음',
    none_until_impact: '충돌 때까지 제동이 전혀 없었다',
    network_gap: '1.36초간 통신이 끊겼다',
    weak_then_strong: '약한 제동 뒤 강한 제동이 실행됐다',
    permanent: '영구 정상 판정',
    conditional_active: '제한 승인 유지',
    withdrawn: '승인 철회·운행중지',
    safe_by_count: '46대가 무사했으니 해제는 안전했다',
    all_certainly_unsafe: '47대 모두 같은 사고가 날 상태였다',
    exposure_not_equivalent: '동일 위험 조건에 노출되었다는 근거가 없다',
    scalar_compare_cost_21ms: '부등식 계산 한 번에 21ms',
    snapshot_sync_cost: '상태 동기화 대기·검증 경로에서 21ms',
    faulty_vehicle_not_dispatched: '결함 차량은 배차되지 않는다',
    perfect_ai: '인식 정확도가 100%다',
    never_rains: '비가 오지 않는다',
    human_driver_present: '항상 운전자가 있다',
    all_tests_fake: '시험 전체가 허위',
    validated_all: '사고 조합까지 검증됨',
    unvalidated_combination: '사고 조건 조합은 미검증',
    merely_proposed: '문서로만 제안',
    deployed_with_open_conditions: '실제 배포됐고 조건 미완료',
    unknown_build: '차량 버전을 확인 못 함',
  }
  return labels[id] ?? id
}

function itemLabel(id: string): string {
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
  return labels[id] ?? id
}

function eventLabel(id: string): string {
  const labels: Record<string, string> = {
    detect: '보행자 판정',
    request: '긴급제동 요청',
    limit: '제동 강도 제한',
    warning: '차내 경고 표시',
    weak_apply: '약한 제동 시작',
    bypass: '독립 충돌임박 경로가 제한 해제',
    strong_apply: '강한 제동 시작',
    impact: '충돌',
  }
  return labels[id] ?? ''
}

function moduleLabel(id: string): string {
  const labels: Record<string, string> = {
    perception: '대상 인식',
    planner: '제동 계획',
    brake_supervisor: '제동 조정기',
    vehicle_controller: '차량 제어기',
  }
  return labels[id] ?? ''
}

function slotLabel(key: string): string {
  const labels: Record<string, string> = {
    live: '사고 당시 판단',
    reanalysis: '사후 재분석',
    provenance: '출처 확인',
    inputRequest: '-8.1 요청',
    modifiedBy: '변경 모듈',
    allowedRequest: '-2.0 허용',
    actualStrong: '-6.2 실제 강한 감속',
  }
  return labels[key] ?? key
}

function valueLabel(value: string): string {
  return moduleLabel(value) || optionLabel(value) || value
}

function optionLabel(id: string): string {
  const labels: Record<string, string> = {
    sensor: '센서',
    overrideState: '수동 해제',
    version: '버전',
    condition: '조건',
    normal: '정상',
    faulty_sensor: '결함 센서',
    missing_state: '상태 누락',
    no_override: '수동 해제 없음',
    manual_override: '수동 해제',
    supervisor_3_4: '제동 조정기 3.4',
    supervisor_3_5: '제동 조정기 3.5',
    dry_day: '건조/주간',
    rain_night: '강우/야간',
  }
  return labels[id] ?? id
}

function caseLabel(id: string): string {
  const labels: Record<string, string> = {
    historical: '사고 당시 조건',
    gate_restored: '건강검증 복원',
    dispatch_blocked: '운행중지 유지',
  }
  return labels[id] ?? id
}

function masterLabel(id: string): string {
  const labels: Record<string, string> = {
    collision: '충돌',
    avoidable_under_assumptions: '보고서 가정에서 회피 가능',
    no_trip: '운행 없음',
  }
  return labels[id] ?? id
}

function annotationLabel(key: string): string {
  const labels: Record<string, string> = {
    requestToStrongMs: '요청-강한 제동 간격',
    detect_classScore: '판정 신뢰도',
    request_requestId: '요청 ID',
    request_frontRiskScore: '전방 위험',
    limit_rearRiskScore: '후방 위험',
  }
  return labels[key] ?? key
}

function cardLabel(index: ContentIndex, id: string): string {
  const event = eventLabel(id)
  if (event) {
    const eventIndex = index.eventIndex(id)
    const entry = index.eventAt(eventIndex)
    const time = entry ? ` (T${entry.relativeMs >= 0 ? '+' : ''}${(entry.relativeMs / 1000).toFixed(2)}s)` : ''
    return event + time
  }
  return index.evidenceDef(id)?.title ?? id
}

function stableHash(value: string): number {
  let result = 0
  for (let i = 0; i < value.length; i++) {
    result = (result * 31 + value.charCodeAt(i)) | 0
  }
  return result
}

function stableOrder(a: string, b: string): number {
  const hashA = stableHash(a)
  const hashB = stableHash(b)
  if (hashA !== hashB) return hashA < hashB ? -1 : 1
  return a < b ? -1 : a > b ? 1 : 0
}
