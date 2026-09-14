import { mkdirSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test, type Page } from '@playwright/test'

const screenshotDir = join(process.cwd(), '..', 'reports', 'screenshots', 'qa-overlays')
mkdirSync(screenshotDir, { recursive: true })

const expectations: Record<string, string[]> = {
  ev01: ['vehicle-identity'],
  ev03: ['reference-frame', 'pedestrian-location', 'crosswalk-location'],
  ev05: ['cabin-warning'],
  ev06: ['live-decision-frame', 'pedestrian-detection'],
  ev12: ['rear-lane-observation'],
  ev13: ['cctv-taxi', 'cctv-rear-lane'],
  ev14: ['radar-packet'],
  ev15: ['maintenance-warning'],
  ev16: ['replacement-inventory'],
  ev35: ['last-message'],
}

async function drainDialogue(page: Page, limit = 200): Promise<void> {
  for (let index = 0; index < limit; index++) {
    const next = page.locator('[data-action="advance-dialogue"]')
    if ((await next.count()) === 0) return
    await next.click()
  }
}

async function autoCompleteRequiredPath(page: Page): Promise<void> {
  for (let step = 0; step < 140; step++) {
    await page.getByRole('button', { name: '메뉴' }).click()
    await page.getByRole('button', { name: '개발 패널' }).click()
    const dialog = page.getByRole('dialog', { name: '개발 패널' })
    await dialog.getByRole('button', { name: '씬 자동 진행' }).click()
    const message = (await dialog.locator('.anl-feedback').textContent()) ?? ''
    expect(message).not.toMatch(/blocked|stuck|unhandled|limit/)
    await dialog.getByRole('button', { name: '닫기' }).click()
    if (message.includes('s710')) return
  }
  throw new Error('required path did not reach s710')
}

async function grantOptionalMessage(page: Page): Promise<void> {
  await page.getByRole('button', { name: '메뉴' }).click()
  await page.getByRole('button', { name: '개발 패널' }).click()
  const dialog = page.getByRole('dialog', { name: '개발 패널' })
  await dialog.getByLabel('씬 ID').fill('s530')
  await dialog.getByRole('button', { name: '픽스처 불러오기' }).click()
  await dialog.getByRole('button', { name: '닫기' }).click()
  await drainDialogue(page)
  await page.locator('[data-branch-id="b_s530_accept"]').click()
  await drainDialogue(page)
}

test('[qa:overlays] 10개 증거 context의 코드 오버레이가 데스크톱·모바일에 렌더링된다', async ({ page }) => {
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
  await autoCompleteRequiredPath(page)
  await grantOptionalMessage(page)

  for (const viewport of [
    { name: 'desktop', width: 1920, height: 1080 },
    { name: 'mobile', width: 390, height: 844 },
  ]) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    await page.locator('.anl-hud').getByRole('button', { name: '증거' }).click()
    const dialog = page.getByRole('dialog', { name: '증거책' })
    await expect(dialog).toBeVisible()

    for (const [evidenceId, annotationIds] of Object.entries(expectations)) {
      await dialog.locator(`[data-evidence-id="${evidenceId}"]`).click()
      for (const annotationId of annotationIds) {
        const annotation = dialog.locator(`[data-annotation-id="${annotationId}"]`)
        await expect(annotation).toBeVisible()
        await annotation.scrollIntoViewIfNeeded()
      }
      await page.screenshot({
        path: join(screenshotDir, `${viewport.name}-${evidenceId}.png`),
        animations: 'disabled',
      })
    }

    await dialog.getByRole('button', { name: '닫기' }).click()
  }
})
