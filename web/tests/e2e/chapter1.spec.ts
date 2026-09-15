import { expect, test, type Page } from '@playwright/test'

async function drainDialogue(page: Page, limit = 200): Promise<void> {
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

async function chooseAllBranches(page: Page): Promise<void> {
  for (let i = 0; i < 12; i++) {
    const choice = page.locator('.anl-choice:not([disabled])').first()
    if ((await choice.count()) === 0) break
    await choice.click()
    await drainDialogue(page, 30)
  }
  const continueButton = page.locator('.anl-dialogue__continue')
  await expect(continueButton).toBeVisible()
  await continueButton.click()
}

async function pressContinue(page: Page): Promise<void> {
  for (let i = 0; i < 4; i++) {
    const primary = page.locator('.anl-transition .anl-button--primary')
    if ((await primary.count()) === 0 || !(await primary.isVisible())) return
    await primary.click()
    await page.waitForTimeout(250)
  }
}


async function dismissToast(page: Page): Promise<void> {
  const close = page.locator('.anl-toast .anl-button')
  if ((await close.count()) > 0) {
    await close.first().click().catch(() => undefined)
  }
}

async function readAllOwnedEvidence(page: Page): Promise<void> {
  await page.locator('.anl-hud').getByRole('button', { name: '증거' }).click()
  const dialog = page.getByRole('dialog', { name: '증거책' })
  if ((await dialog.count()) === 0) return
  const items = dialog.locator('.anl-evidence__item')
  const count = await items.count()
  for (let i = 0; i < count; i++) {
    await items.nth(i).click().catch(() => undefined)
  }
  await page.getByRole('button', { name: '닫기' }).last().click()
}

async function progressToPuzzle(page: Page): Promise<void> {
  for (let i = 0; i < 60; i++) {
    if ((await page.locator('.anl-puzzle').count()) > 0) return
    await dismissToast(page)
    const next = page.locator('.anl-dialogue__next')
    if ((await next.count()) > 0) {
      await drainDialogue(page, 1)
      continue
    }
    const choice = page.locator('.anl-choice:not([disabled])')
    if ((await choice.count()) > 0) {
      await chooseAllBranches(page)
      continue
    }
    const primary = page.locator('.anl-transition .anl-button--primary')
    if ((await primary.count()) > 0 && (await primary.isEnabled())) {
      await primary.click()
      await page.waitForTimeout(200)
      continue
    }
    if ((await page.locator('.anl-transition').count()) > 0) {
      await readAllOwnedEvidence(page)
      continue
    }
    await page.waitForTimeout(200)
  }
  throw new Error('puzzle에 도달하지 못했습니다')
}

test('실제 입력으로 1장(경고→조사→심문)을 완주한다', async ({ page }) => {
  test.setTimeout(180_000)
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(String(error)))
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })
  page.on('dialog', (dialog) => dialog.accept())
  const external: string[] = []
  page.on('request', (request) => {
    const url = request.url()
    if (!url.startsWith('http://localhost:5173') && !url.startsWith('data:') && !url.startsWith('blob:')) {
      external.push(url)
    }
  })

  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  // 경고 4줄 완료 후 타이틀 메뉴로 복귀 → s010 프롤로그 시작
  await page.getByRole('button', { name: /새 게임/ }).click()
  await drainDialogue(page)
  // s010 완료 → s020으로 이동
  await pressContinue(page)
  await expect(page.locator('.anl-dialogue')).toBeVisible()
  await drainDialogue(page)

  // s020 분기: 비진행 분기 2개를 모두 확인한 뒤 계속
  await chooseAllBranches(page)

  // s020 필수 열람(ev01, ev02) 후 진행
  await page.locator('.anl-hud').getByRole('button', { name: '증거' }).click()
  const evidenceDialog = page.getByRole('dialog', { name: '증거책' })
  await expect(evidenceDialog).toBeVisible()
  await evidenceDialog.getByRole('button', { name: '사고 기초 기록' }).click()
  await evidenceDialog.getByRole('button', { name: 'NEOVIA 발표용 분석표' }).click()
  await page.getByRole('button', { name: '닫기' }).last().click()
  await pressContinue(page)

  // s100 허브 → s110 조사
  await expect(page.locator('.anl-hub')).toBeVisible()
  await drainDialogue(page)
  await page.locator('.anl-hub__card[data-scene-id="s110"]').click()
  await progressToPuzzle(page)
  const regions = page.locator('.anl-region')
  await expect(regions.first()).toBeVisible()
  const regionCount = await regions.count()
  for (let i = 0; i < regionCount; i++) {
    await regions.nth(i).click()
  }
  await drainDialogue(page)
  await pressContinue(page)

  // s120 심문 → p02 해결
  await expect(page.locator('.anl-hub')).toBeVisible()
  await drainDialogue(page)
  await page.locator('.anl-hub__card[data-scene-id="s120"]').click()
  await progressToPuzzle(page)
  await expect(page.locator('.anl-puzzle')).toBeVisible()

  // 오답: 다른 증언으로 제시 → 피드백 → 증언만 수정해 재제출
  await page.locator('.anl-statement', { hasText: '사람이 바로 앞에 나타났어요' }).click()
  await page.getByRole('button', { name: '증거 선택' }).click()
  const picker = page.getByRole('dialog', { name: '제시할 증거' })
  await expect(picker).toBeVisible()
  await picker.getByText('승객 휴대전화 영상').click()
  await picker.getByRole('button', { name: '확인' }).click()
  await page.getByRole('button', { name: '증거 제시' }).click()
  await expect(page.locator('.anl-toast')).toContainText('직접 충돌하지 않습니다')
  await dismissToast(page)
  await expect(page.locator('.anl-puzzle')).toBeVisible()

  await page.locator('.anl-statement', { hasText: '아무런 반응도 없었어요' }).click()
  await page.getByRole('button', { name: '증거 제시' }).click()
  await drainDialogue(page)
  await pressContinue(page)

  // 허브에서 s110·s120 완료 확인
  await expect(page.locator('.anl-hub')).toBeVisible()
  await drainDialogue(page)
  const doneCards = page.locator('.anl-hub__card--done')
  expect(await doneCards.count()).toBeGreaterThanOrEqual(2)
  expect(errors).toEqual([])
  expect(external, external.join('\n')).toEqual([])
})
