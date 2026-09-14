import type {
  BranchFileDto,
  DialogueFileDto,
  EpisodeFileDto,
  EvidenceFileDto,
  GraphFileDto,
  HintFileDto,
  MatrixFileDto,
  PuzzleFileDto,
  SceneFileDto,
  TestimonyFileDto,
  UiStringFileDto,
} from '../core/content/dto'
import { ContentIndex } from '../core/content/contentIndex'
import { CONTENT_FILES } from '../core/content/frozenIds'
import { validateContent } from '../core/content/contentValidator'
import { assetIds } from '../shared/assets/assetCatalog'
import { Diag } from '../shared/diagnostics'

export interface ContentLoadResult {
  index: ContentIndex | null
  errors: string[]
  ok: boolean
}

export type FetchLike = (input: string) => Promise<Response>

/**
 * 정적 콘텐츠 JSON 로드 + 스키마·참조 무결성 검사.
 * 시작 시 한 번만 호출하고 결과를 앱 상태로 재사용한다.
 */
export async function loadContent(
  fetchFn: FetchLike = (input) => fetch(input),
  baseUrl = 'content/ep01',
): Promise<ContentLoadResult> {
  const errors: string[] = []
  const base = baseUrl.replace(/\/$/, '')

  const texts = new Map<string, string>()
  await Promise.all(
    CONTENT_FILES.map(async (name) => {
      try {
        const response = await fetchFn(`${base}/${name}`)
        if (!response.ok) {
          errors.push(`content fetch failed: ${name} (${response.status})`)
          return
        }
        texts.set(name, await response.text())
      } catch (error) {
        errors.push(`content fetch failed: ${name}: ${(error as Error).message}`)
      }
    }),
  )
  if (errors.length > 0) return { index: null, errors, ok: false }

  const parse = <T>(name: string): T | null => {
    const text = texts.get(name)
    if (text === undefined) {
      errors.push(`content missing: ${name}`)
      return null
    }
    try {
      const parsed = JSON.parse(text) as T
      if (parsed === null || parsed === undefined) {
        errors.push(`content parse failed: ${name}: empty result`)
        return null
      }
      return parsed
    } catch (error) {
      errors.push(`content parse failed: ${name}: ${(error as Error).message}`)
      return null
    }
  }

  const episodeFile = parse<EpisodeFileDto>('episode.json')
  const scenesFile = parse<SceneFileDto>('scenes.json')
  const dialoguesFile = parse<DialogueFileDto>('dialogues.ko.json')
  const evidenceFile = parse<EvidenceFileDto>('evidence.json')
  const testimoniesFile = parse<TestimonyFileDto>('testimonies.json')
  const branchesFile = parse<BranchFileDto>('branches.json')
  const puzzlesFile = parse<PuzzleFileDto>('puzzles.json')
  const hintsFile = parse<HintFileDto>('hints.ko.json')
  const uiFile = parse<UiStringFileDto>('ui.ko.json')
  const graphFile = parse<GraphFileDto>('graph.json')
  const matrixFile = parse<MatrixFileDto>('matrix.json')

  if (
    !episodeFile ||
    !scenesFile ||
    !dialoguesFile ||
    !evidenceFile ||
    !testimoniesFile ||
    !branchesFile ||
    !puzzlesFile ||
    !hintsFile ||
    !uiFile ||
    !graphFile ||
    !matrixFile
  ) {
    return { index: null, errors, ok: false }
  }

  const index = new ContentIndex({
    episode: episodeFile.episode,
    scenes: scenesFile.items,
    dialogues: dialoguesFile.items,
    evidence: evidenceFile.items,
    testimonies: testimoniesFile.items,
    branches: branchesFile.items,
    puzzles: puzzlesFile.items,
    hints: hintsFile.items,
    uiStrings: uiFile.items,
    graph: graphFile,
    matrix: matrixFile,
  })

  const validationErrors = validateContent(index, assetIds())
  for (const error of validationErrors) {
    errors.push(`content validation: ${error}`)
  }
  if (errors.length > 0) {
    for (const error of errors) Diag.error(error)
    return { index, errors, ok: false }
  }
  return { index, errors, ok: true }
}
