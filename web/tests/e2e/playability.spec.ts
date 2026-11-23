import { expect, test, type Page } from '@playwright/test'

async function jumpTo(page: Page, sceneId: string) {
  await page.getByRole('button', { name: '메뉴', exact: true }).click()
  await page.getByRole('button', { name: '개발 패널' }).click()
  const dialog = page.getByRole('dialog', { name: '개발 패널' })
  await dialog.getByLabel('씬 ID').fill(sceneId)
  await dialog.getByRole('button', { name: '픽스처 불러오기' }).click()
  await dialog.getByRole('button', { name: '닫기' }).click()
}

async function drain(page: Page) {
  const next = page.locator('[data-action="advance-dialogue"]')
  for (let i = 0; i < 60 && await next.isVisible(); i++) await next.click()
  await expect(next).toHaveCount(0)
}

function branch(page: Page, id: string) {
  return page.locator(`[data-branch-id="${id}"]`)
}

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('anl.ep01.settings', JSON.stringify({ textSpeed: 'instant', reducedMotion: true }))
  })
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
})

test('중간 결론 오답 두 개를 확인해도 필수 선택이 남는다', async ({ page }) => {
  await jumpTo(page, 's340')
  await drain(page)
  for (const id of ['b_s340_single', 'b_s340_ai_innocent']) {
    await branch(page, id).click()
    await expect(page.locator('.anl-dialogue__text')).not.toBeEmpty()
    await drain(page)
    await expect(page.locator('[data-action="continue-branches"]')).toHaveCount(0)
    await expect(branch(page, 'b_s340_continue')).toBeEnabled()
  }
  await branch(page, 'b_s340_continue').click()
  await drain(page)
  await page.locator('[data-action="continue-scene"]').click()
  await expect(page.locator('.anl-shell')).toHaveAttribute('data-current-scene', 's400')
})

for (const passengerFirst of [false, true]) {
  test(`회사 면담과 수첩이 방문 순서에 반응한다 · 승객 먼저 ${passengerFirst}`, async ({ page }) => {
    if (passengerFirst) {
      await jumpTo(page, 's120')
      await drain(page)
    }
    await jumpTo(page, 's130')
    await drain(page)
    const warning = branch(page, 'b_s130_warning')
    await expect(warning).toHaveCount(passengerFirst ? 1 : 0)
    if (passengerFirst) {
      await warning.click()
      await expect(page.locator('.anl-dialogue__text')).toContainText('경고 표시만으로는')
      await drain(page)
      await expect(page.locator('[data-action="continue-branches"]')).toHaveCount(0)
    }
    await branch(page, 'b_s130_origin').click()
    await drain(page)
    await page.getByRole('button', { name: '수첩', exact: true }).click()
    const notebook = page.getByRole('dialog', { name: '수첩' })
    await expect(notebook).toContainText(passengerFirst ? '차내 경고를 먼저 제시함' : '원본과 출처부터 요청함')
    if (!passengerFirst) {
      await notebook.getByRole('button', { name: '닫기' }).click()
      await jumpTo(page, 's120')
      await drain(page)
      await page.getByRole('button', { name: '수첩', exact: true }).click()
      await expect(notebook).toContainText('면담 뒤 휴대전화 영상을 확보했다')
    }
  })
}

test('가로 844×390에서 세 증거 중 한 개를 교체하고 제출한다', async ({ page }) => {
  await page.setViewportSize({ width: 844, height: 390 })
  await jumpTo(page, 's020')
  await jumpTo(page, 's140')
  await drain(page)
  await jumpTo(page, 's150')
  await drain(page)
  const puzzle = page.locator('[data-puzzle-id="p04"]')
  await expect(puzzle.getByText('사고 당시 차량의 판단에 관한 회사 주장을 검증하세요.')).toBeVisible()
  await page.getByRole('button', { name: '팁', exact: true }).click()
  const hint = page.getByRole('dialog', { name: '팁' })
  await expect(hint.getByRole('button', { name: '힌트 3' })).toBeDisabled()
  await expect(hint).not.toContainText(/st150_02|실시간 판단 기록/)
  await hint.getByRole('button', { name: '닫기' }).click()
  await puzzle.locator('[data-statement-id="st150_02"]').click()
  const open = puzzle.locator('[data-action="open-evidence-picker"]')
  const picker = page.getByRole('dialog', { name: '제시할 증거' })
  const evidence = (id: string) => picker.locator(`input[data-evidence-id="${id}"]`)
  await open.click()
  for (const id of ['ev01', 'ev02', 'ev06']) await evidence(id).check()
  await picker.getByRole('button', { name: '확인', exact: true }).click()
  await open.click()
  await expect(evidence('ev06')).toBeChecked()
  await evidence('ev01').uncheck()
  await evidence('ev07').check()
  await picker.getByRole('button', { name: '확인', exact: true }).click()
  await puzzle.locator('[data-action="submit-puzzle"]').click()
  await expect(page.locator('.anl-shell')).toHaveAttribute('data-last-result', 'Solved')
})

test('엔딩 응답 뒤 기록을 마치면 타이틀에서 다시 볼 수 있다', async ({ page }) => {
  await jumpTo(page, 's700')
  await drain(page)
  await expect(branch(page, 'b_s700_visited')).toHaveCount(0)
  await branch(page, 'b_s700_prevention').click()
  await expect(page.locator('.anl-dialogue__text')).toContainText('결함차 운행')
  await drain(page)
  await page.locator('[data-action="continue-scene"]').click()
  await drain(page)
  await page.getByRole('button', { name: '타이틀로 돌아가기' }).click()
  await page.getByRole('button', { name: /사건 기록/ }).click()
  await expect(page.getByRole('dialog', { name: '사건 기록' })).toContainText('예방 우선 결론으로 마무리됨')
})
