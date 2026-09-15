import { expect, test, type Page } from '@playwright/test'

async function drainDialogue(page: Page, limit = 100): Promise<void> {
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

async function dismissToast(page: Page): Promise<void> {
  const close = page.locator('.anl-toast .anl-button')
  if ((await close.count()) > 0) await close.first().click().catch(() => undefined)
}

async function pressContinue(page: Page): Promise<void> {
  for (let i = 0; i < 4; i++) {
    const primary = page.locator('.anl-transition .anl-button--primary')
    if ((await primary.count()) === 0 || !(await primary.isVisible())) return
    await primary.click()
    await page.waitForTimeout(250)
  }
}

async function jumpTo(page: Page, sceneId: string, advance = false): Promise<void> {
  await page.getByRole('button', { name: '메뉴' }).click()
  await page.getByRole('button', { name: '개발 패널' }).click()
  const dialog = page.getByRole('dialog', { name: '개발 패널' })
  await dialog.getByLabel('씬 ID').fill(sceneId)
  await dialog.getByRole('button', { name: '픽스처 불러오기' }).click()
  if (advance) {
    await dialog.getByRole('button', { name: '씬 자동 진행' }).click()
  }
  await dialog.getByRole('button', { name: '닫기' }).click()
}

async function evidencePanelText(page: Page): Promise<string> {
  await dismissToast(page)
  await page.locator('.anl-hud').getByRole('button', { name: '증거' }).click()
  const dialog = page.getByRole('dialog', { name: '증거책' })
  await expect(dialog).toBeVisible()
  const text = (await dialog.textContent()) ?? ''
  await page.getByRole('button', { name: '닫기' }).last().click()
  return text
}

test('선택 조사 s520·s530(자료 수신)을 클릭으로 완주한다', async ({ page }) => {
  test.setTimeout(180_000)
  page.on('dialog', (dialog) => dialog.accept())
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  // s520/s530 진입 조건(SceneCompleted s460) 충족
  await jumpTo(page, 's460', true)
  await jumpTo(page, 's500')
  await drainDialogue(page)
  await expect(page.locator('.anl-hub')).toBeVisible()

  // s520: 방문하면 dlg_s520_007에서 ev34를 지급하고 s500으로 복귀
  await page.locator('.anl-hub__card[data-scene-id="s520"]').click()
  await drainDialogue(page)
  await pressContinue(page)
  expect(await evidencePanelText(page)).toContain('승객 보충 진술')

  // s530: 유족 자료 수신(ev35)
  await expect(page.locator('.anl-hub')).toBeVisible()
  await page.locator('.anl-hub__card[data-scene-id="s530"]').click()
  await drainDialogue(page)
  await page.locator('.anl-choice', { hasText: '유족이 제공한 자료만 수신' }).click()
  await drainDialogue(page)
  await pressContinue(page)
  expect(await evidencePanelText(page)).toContain('임도현의 마지막 일상')
  await expect(page.locator('.anl-hub__card--done')).toHaveCount(2)
})

test('s530에서 개인 자료를 거절하면 ev35가 지급되지 않는다', async ({ page }) => {
  test.setTimeout(180_000)
  page.on('dialog', (dialog) => dialog.accept())
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  // s520/s530 진입 조건(SceneCompleted s460) 충족
  await jumpTo(page, 's460', true)
  await jumpTo(page, 's500')
  await drainDialogue(page)
  await expect(page.locator('.anl-hub')).toBeVisible()

  await page.locator('.anl-hub__card[data-scene-id="s530"]').click()
  await drainDialogue(page)
  await page.locator('.anl-choice', { hasText: '개인 자료를 받지 않고 이야기만 듣기' }).click()
  await drainDialogue(page)
  await pressContinue(page)
  const text = await evidencePanelText(page)
  expect(text).not.toContain('임도현의 마지막 일상')
  expect(text).not.toContain('승객 보충 진술')
})
