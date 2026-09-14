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

test('기본 내비게이션이 동작한다', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await expect(page.getByRole('heading', { name: '새 사건' })).toBeVisible()
  await page.getByRole('button', { name: '타이틀로 돌아가기' }).click()
  await expect(page.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeVisible()
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
