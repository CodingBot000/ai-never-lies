import { expect, test } from '@playwright/test'

test('개발 도구로 시작부터 엔딩까지 전체 콘텐츠가 진행된다', async ({ page }) => {
  test.setTimeout(300_000)
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(String(error)))
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })
  page.on('dialog', (dialog) => dialog.accept())

  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await expect(page.getByText('이 작품의 인물·회사·사건·조사기관은 허구입니다.')).toBeVisible()

  let finished = false
  for (let step = 0; step < 120 && !finished; step++) {
    await page.getByRole('button', { name: '메뉴' }).click()
    await page.getByRole('button', { name: '개발 패널' }).click()
    const dialog = page.getByRole('dialog', { name: '개발 패널' })
    await dialog.getByRole('button', { name: '씬 자동 진행' }).click()
    const message = (await dialog.locator('.anl-feedback').textContent()) ?? ''
    expect(message, `step ${step} must not be blocked`).not.toMatch(/blocked|stuck|unhandled|limit/)
    if (message.includes('s710')) finished = true
    await dialog.getByRole('button', { name: '닫기' }).click()
    await expect(page.getByRole('dialog', { name: '개발 패널' })).toHaveCount(0)
  }

  expect(finished, '자동 진행이 s710에 도달해야 한다').toBe(true)

  await page.getByRole('button', { name: '메뉴' }).click()
  await page.getByRole('button', { name: '개발 패널' }).click()
  const devPanel = page.getByRole('dialog', { name: '개발 패널' })
  const summary = (await devPanel.locator('dd').nth(5).textContent()) ?? ''
  expect(summary).toContain('해결 퍼즐 20')
  expect(errors).toEqual([])
})
