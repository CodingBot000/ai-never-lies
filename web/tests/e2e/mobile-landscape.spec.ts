import { expect, test, type Page } from '@playwright/test'

async function drainDialogue(page: Page, limit = 60): Promise<void> {
  for (let i = 0; i < limit; i++) {
    const next = page.locator('.anl-dialogue__next')
    if ((await next.count()) === 0) return
    try {
      await next.click({ timeout: 1500 })
    } catch {
      return
    }
  }
}

async function jumpTo(page: Page, sceneId: string): Promise<void> {
  await page.getByRole('button', { name: '메뉴' }).click()
  await page.getByRole('button', { name: '개발 패널' }).click()
  const dialog = page.getByRole('dialog', { name: '개발 패널' })
  await dialog.getByLabel('씬 ID').fill(sceneId)
  await dialog.getByRole('button', { name: '픽스처 불러오기' }).click()
  await dialog.getByRole('button', { name: '닫기' }).click()
}

async function noOverflow(page: Page, label: string): Promise<void> {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )
  expect(overflow, `${label} horizontal overflow`).toBeLessThanOrEqual(1)
}

test('가로 모바일에서 HUD와 본문이 겹치지 않고 진행된다', async ({ page }) => {
  test.setTimeout(180_000)
  page.on('dialog', (dialog) => dialog.accept())
  await page.goto('/')

  // 세로 안내 → 가로 전환 시 해제
  await page.setViewportSize({ width: 412, height: 915 })
  await expect(page.locator('.anl-portrait-notice')).toBeVisible()
  await expect(page.getByRole('button', { name: '가로로 시작' })).toBeVisible()
  await page.setViewportSize({ width: 915, height: 412 })
  await expect(page.locator('.anl-portrait-notice')).toHaveCount(0)

  await expect(page.getByRole('button', { name: /새 게임/ })).toBeVisible()
  await noOverflow(page, 'title')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  await noOverflow(page, 's010')

  // HUD가 본문 위에 보이고 가려지지 않는다
  const hud = page.locator('.anl-hud')
  await expect(hud).toBeVisible()
  const hudBox = await hud.boundingBox()
  expect(hudBox?.width ?? 0).toBeGreaterThan(0)

  await jumpTo(page, 's110')
  await noOverflow(page, 's110')
  await expect(page.locator('.anl-puzzle')).toBeVisible()
  const header = await page.locator('.anl-puzzle__header h1').boundingBox()
  expect(header?.y ?? 0, 'puzzle header starts below HUD').toBeGreaterThanOrEqual((hudBox?.height ?? 0) - 4)

  await jumpTo(page, 's150')
  await drainDialogue(page)
  await noOverflow(page, 's150')
  await expect(page.locator('.anl-puzzle')).toBeVisible()

  await jumpTo(page, 's640')
  await drainDialogue(page)
  await noOverflow(page, 's640')
})
