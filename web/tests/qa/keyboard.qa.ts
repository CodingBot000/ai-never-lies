import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test, type Locator, type Page } from '@playwright/test'
import type { EvidenceSetDto, PuzzleDefinitionDto } from '../../src/core/content/dto'

const puzzles = (
  JSON.parse(readFileSync(join(process.cwd(), '../content/ep01/puzzles.json'), 'utf8')) as {
    items: PuzzleDefinitionDto[]
  }
).items
const puzzleById = new Map(puzzles.map((puzzle) => [puzzle.id, puzzle]))

async function activate(page: Page, locator: Locator, key = 'Enter'): Promise<void> {
  await expect(locator).toBeVisible()
  await locator.focus()
  await page.keyboard.press(key)
}

async function chooseEvidence(page: Page, evidenceIds: string[]): Promise<void> {
  const dialog = page.getByRole('dialog', { name: '제시할 증거' })
  await expect(dialog).toBeVisible()
  for (const evidenceId of evidenceIds) {
    await activate(page, dialog.locator(`input[data-evidence-id="${evidenceId}"]`), 'Space')
  }
  await activate(page, dialog.locator('[data-action="confirm-evidence"]'))
  await expect(dialog).toHaveCount(0)
}

function firstEvidenceSet(sets: EvidenceSetDto[]): string[] {
  return sets.find((set) => set.requiredIds.length > 0)?.requiredIds ?? []
}

async function solvePuzzle(page: Page, puzzle: PuzzleDefinitionDto): Promise<void> {
  const root = page.locator(`[data-puzzle-id="${puzzle.id}"]`)
  await expect(root).toBeVisible()

  switch (puzzle.type) {
    case 'hotspot_set':
      for (const region of puzzle.regions) {
        await activate(page, root.locator(`[data-region-id="${region.regionId}"]`))
      }
      break
    case 'statement_refutation':
      await activate(page, root.locator(`[data-statement-id="${puzzle.targetStatementId}"]`))
      await activate(page, root.locator('[data-action="open-evidence-picker"]'))
      await chooseEvidence(page, firstEvidenceSet(puzzle.sufficientEvidenceSets))
      if (puzzle.correctChoiceIds[0]) {
        await activate(page, root.locator(`[data-choice-id="${puzzle.correctChoiceIds[0]}"]`))
      }
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'source_mapping':
    case 'trace_mapping':
      for (const pair of puzzle.valuePairs) {
        await activate(page, root.locator(`[data-slot="${pair.key}"]`))
        await activate(page, root.locator(`[data-value="${pair.value}"]`))
      }
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'timeline_order':
    case 'chronology':
    case 'deployment_chain':
      for (const value of puzzle.orderedIds) {
        await activate(page, root.locator(`[data-value="${value}"]`))
      }
      if (puzzle.correctChoiceIds[0]) {
        await activate(page, root.locator(`[data-choice-id="${puzzle.correctChoiceIds[0]}"]`))
      }
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'cross_camera':
      await activate(page, root.locator(`[data-window-id="${puzzle.correctChoiceIds[0]}"]`))
      await activate(page, root.locator('[data-action="open-evidence-picker"]'))
      await chooseEvidence(page, firstEvidenceSet(puzzle.sufficientEvidenceSets))
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'argument_choice':
    case 'diff_select':
      for (const choice of puzzle.correctChoiceIds) {
        await activate(page, root.locator(`[data-choice-id="${choice}"]`))
      }
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'assumption_link':
      await activate(page, root.locator(`[data-choice-id="${puzzle.correctChoiceIds[0]}"]`))
      await activate(page, root.locator('[data-action="open-evidence-picker"]'))
      await chooseEvidence(page, firstEvidenceSet(puzzle.sufficientEvidenceSets))
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'matrix_gap': {
      const slots = ['sensor', 'overrideState', 'version', 'condition']
      for (let index = 0; index < slots.length; index++) {
        await activate(
          page,
          root.locator(`[data-slot="${slots[index]}"][data-value="${puzzle.orderedIds[index]}"]`),
        )
      }
      await activate(page, root.locator(`[data-choice-id="${puzzle.correctChoiceIds[0]}"]`))
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    }
    case 'counterfactual':
      for (const caseId of puzzle.requiredCaseIds) {
        await activate(page, root.locator(`[data-case-id="${caseId}"]`))
      }
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'causal_graph':
      for (const edge of puzzle.requiredEdges) {
        await activate(page, root.locator(`[data-edge-kind="${edge.kind}"]`))
        await activate(page, root.locator(`[data-node-id="${edge.from}"]`))
        await activate(page, root.locator(`[data-node-id="${edge.to}"]`))
      }
      for (const proof of puzzle.edgeProofRequirements) {
        const key = `${proof.from}|${proof.to}|${proof.kind}`
        await activate(page, root.locator(`[data-edge-key="${key}"]`))
        await activate(page, root.locator('[data-action="open-edge-proof-picker"]'))
        await chooseEvidence(page, firstEvidenceSet(proof.sufficientEvidenceSets))
      }
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    case 'responsibility_matrix':
      for (const proof of puzzle.reportProofRequirements) {
        await activate(page, root.locator(`.anl-report__palette [data-report-item="${proof.item}"]`))
        await activate(page, root.locator(`[data-report-section="${proof.section}"]`))
        await activate(page, root.locator(`[data-proof-item="${proof.item}"]`))
        await chooseEvidence(page, firstEvidenceSet(proof.sufficientEvidenceSets))
      }
      await activate(page, root.locator('.anl-report__palette [data-report-item="no_intent_or_criminal_verdict"]'))
      await activate(page, root.locator('[data-report-section="limit"]'))
      await activate(page, root.locator('[data-action="submit-puzzle"]'))
      break
    default:
      throw new Error(`unsupported puzzle type: ${puzzle.type}`)
  }

  await expect(root).toHaveCount(0)
}

async function readAllEvidence(page: Page): Promise<void> {
  await activate(page, page.locator('.anl-hud').getByRole('button', { name: '증거' }))
  const dialog = page.getByRole('dialog', { name: '증거책' })
  await expect(dialog).toBeVisible()
  const items = dialog.locator('[data-evidence-id]')
  const count = await items.count()
  for (let index = 0; index < count; index++) {
    await activate(page, items.nth(index))
  }
  await activate(page, dialog.getByRole('button', { name: '닫기' }))
}

test('[qa:keyboard] 마우스 포인터 없이 시작부터 사건 기록까지 완주한다', async ({ page }) => {
  test.setTimeout(300_000)
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(String(error)))
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })
  page.on('dialog', (dialog) => dialog.accept())
  await page.addInitScript(() => {
    localStorage.setItem(
      'anl.ep01.settings',
      JSON.stringify({ textSize: '100', textSpeed: 'instant', autoAdvance: false, reducedMotion: true }),
    )
    ;(window as unknown as { anlPointerDowns: number }).anlPointerDowns = 0
    document.addEventListener('pointerdown', () => {
      ;(window as unknown as { anlPointerDowns: number }).anlPointerDowns++
    })
  })

  await page.goto('/')
  await activate(page, page.getByRole('button', { name: /새 게임/ }))

  let reachedArchive = false
  for (let step = 0; step < 2500; step++) {
    const shell = page.locator('.anl-shell')
    const sceneId = (await shell.getAttribute('data-current-scene')) ?? ''

    const toastClose = page.locator('.anl-toast .anl-button').first()
    if ((await toastClose.count()) > 0 && (await toastClose.isVisible())) {
      await activate(page, toastClose)
      continue
    }

    const next = page.locator('[data-action="advance-dialogue"]')
    if ((await next.count()) > 0 && (await next.isVisible())) {
      await page.keyboard.press('Space')
      continue
    }

    const puzzleRoot = page.locator('[data-puzzle-id]')
    if ((await puzzleRoot.count()) > 0 && (await puzzleRoot.isVisible())) {
      const puzzleId = (await puzzleRoot.getAttribute('data-puzzle-id')) ?? ''
      const puzzle = puzzleById.get(puzzleId)
      if (!puzzle) throw new Error(`unknown puzzle ${puzzleId}`)
      await solvePuzzle(page, puzzle)
      continue
    }

    const branch = page.locator('[data-branch-advances="true"]:not([disabled])').first()
    if ((await branch.count()) > 0 && (await branch.isVisible())) {
      await activate(page, branch)
      continue
    }
    const branchContinue = page.locator('[data-action="continue-branches"]')
    if ((await branchContinue.count()) > 0 && (await branchContinue.isEnabled())) {
      await activate(page, branchContinue)
      continue
    }

    if (sceneId === 's710' && (await page.locator('.anl-archive').count()) > 0) {
      reachedArchive = true
      break
    }

    const requiredHubCard = page
      .locator('[data-scene-id][data-optional="false"][data-completed="false"]:not([disabled])')
      .first()
    if ((await requiredHubCard.count()) > 0 && (await requiredHubCard.isVisible())) {
      await activate(page, requiredHubCard)
      continue
    }
    const hubContinue = page.locator('[data-action="continue-hub"]')
    if ((await hubContinue.count()) > 0 && (await hubContinue.isEnabled())) {
      await activate(page, hubContinue)
      continue
    }

    const transition = page.locator('[data-action="continue-scene"]')
    if ((await transition.count()) > 0) {
      if (!(await transition.isEnabled())) {
        await readAllEvidence(page)
      } else {
        await activate(page, transition)
      }
      continue
    }

    const titleNewGame = page.getByRole('button', { name: /새 게임/ })
    if ((await titleNewGame.count()) > 0 && (await titleNewGame.isEnabled())) {
      await activate(page, titleNewGame)
      continue
    }

    throw new Error(`keyboard flow stuck at step=${step} scene=${sceneId} phase=${await shell.getAttribute('data-phase')}`)
  }

  expect(reachedArchive).toBe(true)
  expect(await page.evaluate(() => (window as unknown as { anlPointerDowns: number }).anlPointerDowns)).toBe(0)
  expect(errors).toEqual([])
})

test('[qa:keyboard] 입력 필드에서는 전역 Space·Esc 단축키를 실행하지 않는다', async ({ page }) => {
  page.on('dialog', (dialog) => dialog.accept())
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).press('Enter')
  await page.getByRole('button', { name: '메뉴' }).press('Enter')
  await page.getByRole('button', { name: '개발 패널' }).press('Enter')
  const dialog = page.getByRole('dialog', { name: '개발 패널' })
  const sceneInput = dialog.getByLabel('씬 ID')
  await sceneInput.fill('s150')
  await sceneInput.press('Space')
  await expect(sceneInput).toHaveValue('s150 ')
  await sceneInput.press('Escape')
  await expect(dialog).toBeVisible()
  await expect(page.locator('.anl-shell')).toHaveAttribute('data-current-scene', 's000')
})
