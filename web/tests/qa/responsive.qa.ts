import { mkdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test, type Page } from '@playwright/test'
import type { PuzzleDefinitionDto, SceneDefinitionDto } from '../../src/core/content/dto'

const rootDir = join(process.cwd(), '..')
const screenshotDir = join(rootDir, 'reports', 'screenshots', 'qa-responsive')
mkdirSync(screenshotDir, { recursive: true })

const scenes = (
  JSON.parse(readFileSync(join(rootDir, 'content/ep01/scenes.json'), 'utf8')) as { items: SceneDefinitionDto[] }
).items
const puzzles = (
  JSON.parse(readFileSync(join(rootDir, 'content/ep01/puzzles.json'), 'utf8')) as { items: PuzzleDefinitionDto[] }
).items

async function dismissToast(page: Page): Promise<void> {
  const close = page.locator('.anl-toast .anl-button').first()
  if ((await close.count()) > 0) await close.click()
}

async function jumpTo(page: Page, sceneId: string): Promise<void> {
  await dismissToast(page)
  await page.locator('.anl-hud').getByRole('button', { name: '메뉴' }).click()
  await page.getByRole('button', { name: '개발 패널' }).click()
  const dialog = page.getByRole('dialog', { name: '개발 패널' })
  await dialog.getByLabel('씬 ID').fill(sceneId)
  await dialog.getByRole('button', { name: '픽스처 불러오기' }).click()
  await dialog.getByRole('button', { name: '닫기' }).click()
  await expect(page.locator('.anl-shell')).toHaveAttribute('data-current-scene', sceneId)
}

async function reachPuzzle(page: Page, puzzle: PuzzleDefinitionDto): Promise<void> {
  await jumpTo(page, puzzle.sceneId)
  const root = page.locator(`[data-puzzle-id="${puzzle.id}"]`)
  for (let step = 0; step < 250; step++) {
    if ((await root.count()) > 0) break
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
    break
  }
  await expect(root).toBeVisible()
}

async function expectNoViewportOverflow(page: Page, label: string): Promise<void> {
  const result = await page.evaluate(() => {
    const horizontalOverflow = document.documentElement.scrollWidth - window.innerWidth
    const selectors = 'button, [role="dialog"], .anl-dialogue__box, .anl-transition__panel, .anl-puzzle__header'
    const offenders = [...document.querySelectorAll<HTMLElement>(selectors)]
      .filter((element) => {
        const style = getComputedStyle(element)
        if (style.display === 'none' || style.visibility === 'hidden') return false
        const rect = element.getBoundingClientRect()
        if (rect.width === 0 || rect.height === 0) return false
        return rect.left < -1 || rect.right > window.innerWidth + 1
      })
      .map((element) => ({
        tag: element.tagName,
        className: element.className,
        text: element.innerText.slice(0, 80),
        rect: element.getBoundingClientRect().toJSON(),
      }))
    return { horizontalOverflow, offenders }
  })
  expect(result.horizontalOverflow, `${label} document overflow`).toBeLessThanOrEqual(1)
  expect(result.offenders, `${label} clipped controls`).toEqual([])
}

test('[qa:responsive] 43개 장면과 20개 퍼즐이 150% 데스크톱·모바일 폭에서 잘리지 않는다', async ({ page }) => {
  test.setTimeout(300_000)
  page.on('dialog', (dialog) => dialog.accept())
  await page.addInitScript(() => {
    localStorage.setItem(
      'anl.ep01.settings',
      JSON.stringify({ textSize: '150', textSpeed: 'instant', autoAdvance: false, reducedMotion: true }),
    )
  })
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()

  const viewports = [
    { name: 'desktop-150', width: 1920, height: 1080 },
    { name: 'mobile-150', width: 390, height: 844 },
  ]

  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    for (const scene of scenes) {
      await jumpTo(page, scene.id)
      await expectNoViewportOverflow(page, `${viewport.name}/${scene.id}`)
      await page.screenshot({
        path: join(screenshotDir, `${viewport.name}-scene-${scene.id}.png`),
        animations: 'disabled',
      })
    }

    for (const puzzle of puzzles) {
      await reachPuzzle(page, puzzle)
      await expectNoViewportOverflow(page, `${viewport.name}/${puzzle.id}`)
      await page.screenshot({
        path: join(screenshotDir, `${viewport.name}-puzzle-${puzzle.id}.png`),
        animations: 'disabled',
      })
    }
  }

  expect(scenes).toHaveLength(43)
  expect(puzzles).toHaveLength(20)
})
