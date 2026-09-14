import { expect, test } from '@playwright/test'

test('타이틀이 콘솔 오류 없이 렌더링된다', async ({ page }) => {
  const errors: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })
  page.on('pageerror', (error) => errors.push(String(error)))

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeVisible()
  await expect(page.getByRole('button', { name: /새 게임/ })).toBeEnabled()
  await expect(page.getByRole('button', { name: /이어하기/ })).toBeDisabled()
  expect(errors).toEqual([])
})

test('새 게임을 시작하면 경고 대사가 표시된다', async ({ page }) => {
  page.on('dialog', (dialog) => dialog.accept())
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await expect(page.getByText('이 작품의 인물·회사·사건·조사기관은 허구입니다.')).toBeVisible()
})

test('모바일 폭에서 가로 오버플로가 없다', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/')
  await expect(page.getByRole('button', { name: /새 게임/ })).toBeVisible()
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )
  expect(overflow).toBeLessThanOrEqual(1)
})
