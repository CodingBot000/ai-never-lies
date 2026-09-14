import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test, type Page } from '@playwright/test'
import type { PuzzleDefinitionDto } from '../../src/core/content/dto'

const puzzles = (
  JSON.parse(readFileSync(join(process.cwd(), '../content/ep01/puzzles.json'), 'utf8')) as {
    items: PuzzleDefinitionDto[]
  }
).items
const puzzleById = new Map(puzzles.map((puzzle) => [puzzle.id, puzzle]))
const DECLARED_PARTIAL_IDS = new Set(['p04', 'p07', 'p09', 'p15', 'p18', 'p19'])

async function dismissToast(page: Page): Promise<void> {
  const close = page.locator('.anl-toast .anl-button').first()
  if ((await close.count()) > 0) await close.click()
}

async function chooseEvidence(page: Page, ids: string[]): Promise<void> {
  const dialog = page.getByRole('dialog', { name: '제시할 증거' })
  for (const id of ids) await dialog.locator(`input[data-evidence-id="${id}"]`).check()
  await dialog.locator('[data-action="confirm-evidence"]').click()
}

async function submitDeclaredPartial(page: Page, puzzle: PuzzleDefinitionDto): Promise<void> {
  const root = page.locator(`[data-puzzle-id="${puzzle.id}"]`)
  switch (puzzle.id) {
    case 'p04':
    case 'p09':
    case 'p18':
    case 'p19':
      await root.locator(`[data-statement-id="${puzzle.targetStatementId}"]`).click()
      await root.locator('[data-action="open-evidence-picker"]').click()
      await chooseEvidence(page, [puzzle.sufficientEvidenceSets[0]?.requiredIds[0] ?? ''])
      break
    case 'p07':
      await root.locator(`[data-window-id="${puzzle.correctChoiceIds[0]}"]`).click()
      await root.locator('[data-action="open-evidence-picker"]').click()
      await chooseEvidence(page, [puzzle.sufficientEvidenceSets[0]?.requiredIds[0] ?? ''])
      break
    case 'p15':
      await root.locator(`[data-value="${puzzle.orderedIds[0]}"]`).click()
      break
    default:
      throw new Error(`no declared partial path for ${puzzle.id}`)
  }

  await root.locator('[data-action="submit-puzzle"]').click()
  await expect(page.locator('.anl-shell')).toHaveAttribute('data-last-result', 'Partial')
  await expect(page.locator('.anl-toast')).toContainText(puzzle.partialText)
  await expect(root).toBeVisible()

  if (puzzle.id === 'p15') await root.locator('.anl-order-item').click()
}

async function reachPuzzleOrStableScene(page: Page): Promise<void> {
  for (let step = 0; step < 300; step++) {
    if ((await page.locator('[data-puzzle-id]').count()) > 0) return
    const next = page.locator('[data-action="advance-dialogue"]')
    if ((await next.count()) > 0) {
      await next.click()
      continue
    }
    const advancing = page.locator('[data-branch-advances="true"]:not([disabled])').first()
    if ((await advancing.count()) > 0) {
      await advancing.click()
      continue
    }
    const branchContinue = page.locator('[data-action="continue-branches"]')
    if ((await branchContinue.count()) > 0 && (await branchContinue.isEnabled())) {
      await branchContinue.click()
      continue
    }
    return
  }
  throw new Error('interaction preparation limit')
}

async function autoProgressCurrentScene(page: Page): Promise<string> {
  await dismissToast(page)
  await page.getByRole('button', { name: '메뉴' }).click()
  await page.getByRole('button', { name: '개발 패널' }).click()
  const dialog = page.getByRole('dialog', { name: '개발 패널' })
  await dialog.getByRole('button', { name: '씬 자동 진행' }).click()
  const message = (await dialog.locator('.anl-feedback').textContent()) ?? ''
  expect(message).not.toMatch(/blocked|stuck|unhandled|limit/)
  await dialog.getByRole('button', { name: '닫기' }).click()
  return message
}

test('[qa:puzzles] 20개 퍼즐이 브라우저에서 오답·부분답을 거부하고 수정 상태를 유지한다', async ({ page }) => {
  test.setTimeout(300_000)
  page.on('dialog', (dialog) => dialog.accept())
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()

  const negativeChecked = new Set<string>()
  const partialChecked = new Set<string>()
  let reachedArchive = false

  for (let sceneStep = 0; sceneStep < 160; sceneStep++) {
    await reachPuzzleOrStableScene(page)
    const root = page.locator('[data-puzzle-id]')
    if ((await root.count()) > 0) {
      const puzzleId = (await root.getAttribute('data-puzzle-id')) ?? ''
      const puzzle = puzzleById.get(puzzleId)
      if (!puzzle) throw new Error(`unknown puzzle ${puzzleId}`)

      await test.step(`${puzzle.id} negative`, async () => {
        if (puzzle.type === 'hotspot_set') {
          await root.locator('.anl-puzzle__canvas').click({ position: { x: 4, y: 4 } })
          await expect(root.locator('.anl-feedback')).toContainText('직접 연결되는 기록을 찾지 못했습니다')
          await root.locator('[data-region-id]').first().click()
          await expect(root).toBeVisible()
        } else {
          await root.locator('[data-action="submit-puzzle"]').click()
          await expect(page.locator('.anl-shell')).toHaveAttribute('data-last-result', /Rejected|Partial/)
          await expect(page.locator('.anl-toast')).not.toBeEmpty()
          await expect(root).toBeVisible()
        }
        negativeChecked.add(puzzle.id)
      })

      if (DECLARED_PARTIAL_IDS.has(puzzle.id)) {
        await dismissToast(page)
        await test.step(`${puzzle.id} declared partial`, async () => {
          await submitDeclaredPartial(page, puzzle)
          partialChecked.add(puzzle.id)
        })
      }
    }

    const message = await autoProgressCurrentScene(page)
    if (message.includes('s710')) {
      reachedArchive = true
      break
    }
  }

  expect([...negativeChecked].sort()).toEqual(puzzles.map((puzzle) => puzzle.id).sort())
  expect([...partialChecked].sort()).toEqual([...DECLARED_PARTIAL_IDS].sort())
  expect(reachedArchive).toBe(true)
})
